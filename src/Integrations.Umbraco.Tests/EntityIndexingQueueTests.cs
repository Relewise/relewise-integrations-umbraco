using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests;

public class EntityIndexingQueueTests
{
    [Fact]
    public async Task FailedEnumerationCannotLeaveAnUnscheduledPartialBatch()
    {
        var fixture = new Fixture();
        static IEnumerable<ContentChange> BrokenChanges()
        {
            for (int i = 0; i < 101; i++)
            {
                yield return Refresh(Guid.NewGuid());
            }

            throw new InvalidOperationException("Failed enumeration");
        }
        Assert.Throws<InvalidOperationException>(() => fixture.Writer.EnqueueChanges(BrokenChanges()));
        Assert.Empty(fixture.Scheduled);
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        await fixture.DrainAsync();
        Assert.Single(Assert.Single(fixture.Batches));
    }

    [Fact]
    public async Task AnAlreadyStoppedHostRejectsNewExplicitWorkBeforeItIsQueued()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();
        var fixture = new Fixture(stopping.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Writer.RebuildAsync());
        Assert.Empty(fixture.Scheduled);
        fixture.Operations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompletedExecutionDoesNotRejectADeferredFollowUpThroughAnInheritedContext()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? followUp = null;
        fixture.Operations.Setup(value => value.RebuildAsync(false, It.IsAny<CancellationToken>())).Returns(() =>
        {
            followUp = Task.Run(async () => { await release.Task; await fixture.Writer.ExportAsync([Guid.NewGuid()]); });
            return Task.CompletedTask;
        });
        await fixture.Writer.RebuildAsync();
        release.SetResult();
        await followUp!.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Once);
        await fixture.DrainAsync();
    }

    [Fact]
    public async Task RecursiveExportFromMappingFailsInsteadOfWaitingForItself()
    {
        var fixture = new Fixture();
        fixture.Operations.Setup(value => value.RebuildAsync(false, It.IsAny<CancellationToken>()))
            .Returns(() => fixture.Writer.ExportAsync([Guid.NewGuid()]));
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.RebuildAsync())
            .WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Contains("mapper", error.Message);
        fixture.Operations.Setup(value => value.RebuildAsync(false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await fixture.Writer.RebuildAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.DrainAsync();
    }

    [Fact]
    public async Task CancelledInlineCallerFinishesItsCurrentScopeButDoesNotDrainRemainingBatches()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool scopeDisposed = false;
        fixture.Operations.SetupSequence(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { try { await release.Task; } finally { scopeDisposed = true; } })
            .Returns(Task.CompletedTask);
        fixture.Writer.EnqueueChanges(Enumerable.Range(0, 200).Select(_ => Refresh(Guid.NewGuid())));
        using var cancellation = new CancellationTokenSource();
        Task export = fixture.Writer.ExportAsync([Guid.NewGuid()], cancellation.Token);
        cancellation.Cancel();
        Assert.False(export.IsCompleted); // Earlier work still owns its scope.
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(scopeDisposed);
        fixture.Operations.Verify(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()), Times.Once);
        await fixture.DrainAsync();
        fixture.Operations.Verify(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Never);
    }

    [Fact]
    public async Task ShutdownCancelsAnExplicitWaitWithoutCancellingEarlierWorkThroughItsCaller()
    {
        using var stopping = new CancellationTokenSource();
        var fixture = new Fixture(stopping.Token);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        Task earlier = fixture.Scheduled.Dequeue()(default);
        Task rebuild = fixture.Writer.RebuildAsync();
        stopping.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rebuild).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(earlier.IsCompleted);
        }
        finally
        {
            release.SetResult();
            await earlier;
        }
        fixture.Operations.Verify(value => value.RebuildAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExplicitDeliveryDoesNotDependOnSchedulingAnUnusedBackgroundFlush()
    {
        var fixture = new Fixture();
        fixture.Queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Throws(new InvalidOperationException("Queue unavailable"));
        await fixture.Writer.ExportAsync([Guid.NewGuid()]);
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Once);
        fixture.Queue.Verify(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
        fixture.Queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(fixture.Scheduled.Enqueue);
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        await fixture.DrainAsync();
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Once);
        Assert.Single(fixture.Batches);
    }

    [Fact]
    public async Task DeletionRetainsItsOwnIdentityAndParentSnapshots()
    {
        var fixture = new Fixture();
        Guid parent = Guid.NewGuid();
        DeletedEntity[] entities = [new(typeof(Product), "original", Guid.NewGuid(), true)];
        Guid[] parents = [parent];
        fixture.Operations.Setup(value => value.DeleteAsync(It.IsAny<IEnumerable<DeletedEntity>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DeletedEntity>, CancellationToken>((items, _) => Assert.Equal("original", Assert.Single(items).Id)).Returns(Task.CompletedTask);
        fixture.Operations.Setup(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null))
            .Callback<IEnumerable<Guid>, CancellationToken, ContentUpdate.UpdateKind?, long?>((items, _, _, _) => Assert.Equal(parent, Assert.Single(items))).Returns(Task.CompletedTask);
        fixture.Writer.EnqueueDeletion(entities, parents);
        entities[0] = new(typeof(Product), "changed", Guid.NewGuid(), true);
        parents[0] = Guid.NewGuid();
        await fixture.DrainAsync();
        fixture.Operations.Verify(value => value.DeleteAsync(It.IsAny<IEnumerable<DeletedEntity>>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Once);
        Assert.DoesNotContain(fixture.Logger.Invocations, invocation => invocation.Method.Name == "Log" && Equals(invocation.Arguments[0], LogLevel.Error));
    }

    [Fact]
    public async Task AnImmediatelyExecutingUmbracoTestQueueDoesNotHoldTheBufferLock()
    {
        var operations = new Mock<IEntityIndexingOperations>();
        operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(1));
        var queue = new Mock<IBackgroundTaskQueue>();
        queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(work => work(default).GetAwaiter().GetResult());
        var writer = new EntityIndexingQueue(operations.Object, queue.Object, Mock.Of<IHostApplicationLifetime>(), Mock.Of<ILogger<EntityIndexingQueue>>());
        await Task.Run(() => writer.EnqueueChanges([Refresh(Guid.NewGuid())])).WaitAsync(TimeSpan.FromSeconds(3));
        operations.Verify(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationReachesAnExecutingExplicitExportAndReleasesTheWriter()
    {
        var fixture = new Fixture();
        fixture.Operations.Setup(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null))
            .Returns(async (IEnumerable<Guid> _, CancellationToken token, ContentUpdate.UpdateKind? _, long? _) => await Task.Delay(Timeout.Infinite, token));
        using var cancellation = new CancellationTokenSource();
        Task export = fixture.Writer.ExportAsync([Guid.NewGuid()], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
        await fixture.Writer.RebuildAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.DrainAsync();
    }

    [Fact]
    public async Task ConsecutiveCallbacksBecomeOneExportWithoutAnAdditionalWorker()
    {
        var fixture = new Fixture();
        for (int i = 0; i < 100; i++)
        {
            fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        }

        Assert.Empty(fixture.Batches);
        Assert.Single(fixture.Scheduled);
        await fixture.DrainAsync();
        Assert.Equal(100, Assert.Single(fixture.Batches).Length);
    }

    [Fact]
    public async Task BoundedBatchesYieldToOtherUmbracoWork()
    {
        var fixture = new Fixture();
        fixture.Writer.EnqueueChanges(Enumerable.Range(0, 250).Select(_ => Refresh(Guid.NewGuid())));
        fixture.Scheduled.Enqueue(_ => { fixture.Calls.Add("other Umbraco work"); return Task.CompletedTask; });
        await fixture.DrainAsync();
        Assert.Equal(new[] { 100, 100, 50 }, fixture.Batches.Select(batch => batch.Length));
        Assert.Equal("other Umbraco work", fixture.Calls[1]);
    }

    [Fact]
    public async Task DeletionsSeparateBatchesAndRefreshTheirParentsInOrder()
    {
        var fixture = new Fixture();
        Guid key = Guid.NewGuid();
        fixture.Writer.EnqueueChanges([Refresh(key)]);
        fixture.Writer.EnqueueDeletion([new DeletedEntity(typeof(Product), "old-id", key, true)], [Guid.NewGuid()]);
        fixture.Writer.EnqueueChanges([Refresh(key)]);
        await fixture.DrainAsync();
        Assert.Equal(new[] { "changes", "delete", "export", "changes" }, fixture.Calls);
        Assert.Equal(2, fixture.Batches.Count);
    }

    [Fact]
    public async Task RebuildInsideUmbracosWorkerFlushesEarlierChangesWithoutDeadlocking()
    {
        var fixture = new Fixture();
        fixture.Scheduled.Enqueue(token => fixture.Writer.RebuildAsync(token: token));
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        await fixture.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "changes", "rebuild" }, fixture.Calls);
    }

    [Fact]
    public async Task ChangesArrivingAfterARebuildRequestCannotOvertakeIt()
    {
        var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { fixture.Calls.Add("first"); entered.SetResult(); await release.Task; });
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        Task first = fixture.Scheduled.Dequeue()(default);
        await entered.Task;
        Task rebuild = fixture.Writer.RebuildAsync();
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        release.SetResult();
        await Task.WhenAll(first, rebuild).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "first", "rebuild" }, fixture.Calls);
        fixture.Operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => fixture.Calls.Add("later")).Returns(Task.CompletedTask);
        await fixture.DrainAsync();
        Assert.Equal(new[] { "first", "rebuild", "later" }, fixture.Calls);
    }

    [Fact]
    public async Task ExplicitExportWaitsForDeliveryAndPreservesItsOptions()
    {
        var fixture = new Fixture();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var identities = new List<Guid> { Guid.NewGuid() };
        Guid expected = identities[0];
        fixture.Operations.Setup(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), ContentUpdate.UpdateKind.ClearAndReplace, 42))
            .Callback<IEnumerable<Guid>, CancellationToken, ContentUpdate.UpdateKind?, long?>((keys, _, _, _) => Assert.Equal(expected, Assert.Single(keys)))
            .Returns(delivered.Task);
        Task export = fixture.Writer.ExportAsync(identities, updateKind: ContentUpdate.UpdateKind.ClearAndReplace, version: 42);
        identities.Clear();
        Assert.False(export.IsCompleted);
        delivered.SetResult();
        await export.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.DrainAsync();
    }

    [Fact]
    public async Task CancelledWaitingExportDoesNotCancelEarlierWorkOrBlockLaterChanges()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Operations.SetupSequence(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Returns(release.Task).Returns(Task.CompletedTask);
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        Task first = fixture.Scheduled.Dequeue()(default);
        using var cancellation = new CancellationTokenSource();
        Task export = fixture.Writer.ExportAsync([Guid.NewGuid()], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        release.SetResult();
        await first;
        await fixture.DrainAsync().WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Operations.Verify(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.Operations.Verify(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), null, null), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBackgroundBatchIsLoggedAndDoesNotDiscardFollowingDeletion(bool timeout)
    {
        var fixture = new Fixture();
        fixture.Operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(timeout ? new OperationCanceledException("Transport timeout") : new InvalidOperationException("Failed export"));
        fixture.Writer.EnqueueChanges([Refresh(Guid.NewGuid())]);
        fixture.Writer.EnqueueDeletion([new DeletedEntity(typeof(Product), "product", Guid.NewGuid(), true)], []);
        await fixture.DrainAsync();
        Assert.Equal("delete", Assert.Single(fixture.Calls));
        Assert.Contains(fixture.Logger.Invocations, invocation => invocation.Method.Name == "Log" && Equals(invocation.Arguments[0], LogLevel.Error));
    }

    [Fact]
    public async Task TransportTimeoutIsLoggedAndPropagated()
    {
        var fixture = new Fixture();
        fixture.Operations.Setup(value => value.RebuildAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Transport timeout"));
        Task rebuild = fixture.Writer.RebuildAsync();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rebuild);
        Assert.Equal("Transport timeout", error.Message);
        Assert.Contains(fixture.Logger.Invocations, invocation => invocation.Method.Name == "Log" && Equals(invocation.Arguments[0], LogLevel.Error));
        await fixture.DrainAsync();
    }

    [Fact]
    public async Task ExplicitFailureIsReturnedToTheCallerAndReleasesTheQueue()
    {
        var fixture = new Fixture();
        fixture.Operations.SetupSequence(value => value.RebuildAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed rebuild")).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.RebuildAsync());
        await fixture.Writer.RebuildAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.DrainAsync();
    }

    private static ContentChange Refresh(Guid key) => ContentChange.Document(key, ChangeImpact.Refresh, ContentState.Published);

    private sealed class Fixture
    {
        internal Mock<IEntityIndexingOperations> Operations { get; } = new();
        internal Mock<IBackgroundTaskQueue> Queue { get; } = new();
        internal Mock<ILogger<EntityIndexingQueue>> Logger { get; } = new();
        internal Queue<Func<CancellationToken, Task>> Scheduled { get; } = new();
        internal List<string> Calls { get; } = [];
        internal List<ContentChange[]> Batches { get; } = [];
        internal EntityIndexingQueue Writer { get; }

        internal Fixture(CancellationToken stopping = default)
        {
            Mock<IBackgroundTaskQueue> queue = Queue;
            queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
                .Callback<Func<CancellationToken, Task>>(Scheduled.Enqueue);
            Operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
                .Callback<ContentChange[], CancellationToken>((changes, _) => { Calls.Add("changes"); Batches.Add(changes); }).Returns(Task.CompletedTask);
            Operations.Setup(value => value.DeleteAsync(It.IsAny<IEnumerable<DeletedEntity>>(), It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("delete")).Returns(Task.CompletedTask);
            Operations.Setup(value => value.ExportAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>(), It.IsAny<ContentUpdate.UpdateKind?>(), It.IsAny<long?>()))
                .Callback(() => Calls.Add("export")).Returns(Task.CompletedTask);
            Operations.Setup(value => value.RebuildAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("rebuild")).Returns(Task.CompletedTask);
            var lifetime = new Mock<IHostApplicationLifetime>();
            lifetime.SetupGet(value => value.ApplicationStopping).Returns(stopping);
            Writer = new EntityIndexingQueue(Operations.Object, queue.Object, lifetime.Object, Logger.Object);
        }

        internal async Task DrainAsync()
        {
            int count = 0;
            while (Scheduled.TryDequeue(out Func<CancellationToken, Task>? work))
            {
                Assert.True(++count < 100, "The queue did not become idle.");
                await work(CancellationToken.None);
            }
        }
    }
}
