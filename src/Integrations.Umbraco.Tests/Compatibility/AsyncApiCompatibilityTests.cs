#pragma warning disable CS0618 // Exercise the retained public API and custom implementations written against it.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Tests.Compatibility;

public class AsyncApiCompatibilityTests
{
    [Fact]
    public async Task ExistingUserLocatorSupportsTheAsyncEntryPointWithoutResolvingTwice()
    {
        var completion = new TaskCompletionSource<User>(TaskCreationOptions.RunContinuationsAsynchronously);
        var implementation = new LegacyUserLocator(completion.Task);
        IRelewiseUserLocator locator = implementation;

        Task<User> pending = locator.GetUserAsync();

        Assert.False(pending.IsCompleted);
        Assert.Equal(1, implementation.Calls);

        User expected = User.ByTemporaryId("existing-browser");
        completion.SetResult(expected);

        Assert.Same(expected, await pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingExportServiceSupportsAsyncEntryPointsAndPreservesRequestAndCancellation(bool all)
    {
        var implementation = new LegacyExportService();
        IExportContentService service = implementation;
        using var cancellation = new CancellationTokenSource();
        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(true);

        if (all)
        {
            ExportAllContentResult result = await service.ExportAllAsync(rebuild, cancellation.Token);

            Assert.Same(implementation.AllResult, result);
            Assert.Same(rebuild, implementation.AllRequest);
            Assert.Null(implementation.Request);
        }
        else
        {
            ExportContentResult result = await service.ExportAsync(content, cancellation.Token);

            Assert.Same(implementation.Result, result);
            Assert.Same(content, implementation.Request);
            Assert.Null(implementation.AllRequest);
        }

        Assert.Equal(cancellation.Token, implementation.Token);
        Assert.Equal(1, implementation.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BuiltInExportEntryPointsAwaitTheSameSingleQueueOperation(bool all, bool legacy)
    {
        var delivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = new Mock<IEntityIndexingOperations>(MockBehavior.Strict);
        Guid key = Guid.NewGuid();
        var page = new Mock<IContent>();

        page.SetupGet(content => content.Key).Returns(key);

        using var cancellation = new CancellationTokenSource();

        if (all)
        {
            operations.Setup(value => value.RebuildAsync(true, It.IsAny<CancellationToken>())).Returns(delivery.Task);
        }
        else
        {
            operations.Setup(value => value.ExportAsync(
                    It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { key })),
                    ContentUpdate.UpdateKind.ReplaceProvidedProperties,
                    42,
                    It.IsAny<CancellationToken>()))
                .Returns(delivery.Task);
        }

        var queue = new EntityIndexingQueue(
            operations.Object,
            Mock.Of<IBackgroundTaskQueue>(),
            Mock.Of<IHostApplicationLifetime>(),
            NullLogger<EntityIndexingQueue>.Instance);
        IExportContentService service = new ExportContentService(queue, Mock.Of<IServiceProvider>());
        var content = new ExportContent([page.Object], ContentUpdate.UpdateKind.ReplaceProvidedProperties, 42);
        var rebuild = new ExportAllContent(true);

        Task pending = all
            ? legacy
                ? service.ExportAll(rebuild, cancellation.Token)
                : service.ExportAllAsync(rebuild, cancellation.Token)
            : legacy
                ? service.Export(content, cancellation.Token)
                : service.ExportAsync(content, cancellation.Token);

        Assert.False(pending.IsCompleted);
        Assert.Single(operations.Invocations);

        delivery.SetResult();
        await pending;

        Assert.Single(operations.Invocations);
    }

    [Fact]
    public async Task DefaultUserLocatorRetainsItsLegacyEntryPoint()
    {
        IRelewiseUserLocator locator = new DefaultAnonymousUserLocator();

        Assert.Equal(
            JsonConvert.SerializeObject(await locator.GetUserAsync()),
            JsonConvert.SerializeObject(await locator.GetUser()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncOnlyUserLocatorSupportsBothEntryPoints(bool legacy)
    {
        var completion = new TaskCompletionSource<User>(TaskCreationOptions.RunContinuationsAsynchronously);
        var implementation = new AsyncOnlyUserLocator(completion.Task);
        IRelewiseUserLocator locator = implementation;

        Task<User> pending = legacy ? locator.GetUser() : locator.GetUserAsync();

        Assert.Same(completion.Task, pending);
        Assert.Equal(1, implementation.Calls);

        User expected = User.ByTemporaryId("async-browser");
        completion.SetResult(expected);

        Assert.Same(expected, await pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitAndInheritedLegacyUserLocatorsSupportTheAsyncEntryPoint(bool explicitImplementation)
    {
        User expected = User.ByTemporaryId("existing-browser");
        Task<User> result = Task.FromResult(expected);
        IRelewiseUserLocator locator = explicitImplementation
            ? new ExplicitLegacyUserLocator(result)
            : new InheritedLegacyUserLocator(result);

        Assert.Same(expected, await locator.GetUserAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserLocatorCompatibilityPreservesFailureAndCancellation(bool cancelled)
    {
        var failure = new InvalidOperationException("Identity resolution failed.");
        Task<User> result = cancelled
            ? Task.FromCanceled<User>(new CancellationToken(true))
            : Task.FromException<User>(failure);
        IRelewiseUserLocator oldImplementation = new LegacyUserLocator(result);
        IRelewiseUserLocator newImplementation = new AsyncOnlyUserLocator(result);

        foreach (Task<User> pending in new[] { oldImplementation.GetUserAsync(), newImplementation.GetUser() })
        {
            Assert.Same(result, pending);

            if (cancelled)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => pending));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingUserLocatorImplementationFailsClearlyFromEitherEntryPoint(bool legacy)
    {
        IRelewiseUserLocator locator = new MissingUserLocator();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            legacy ? locator.GetUser() : locator.GetUserAsync());

        Assert.Contains(nameof(MissingUserLocator), error.Message);
        Assert.Contains(nameof(IRelewiseUserLocator.GetUserAsync), error.Message);
        Assert.Contains(nameof(IRelewiseUserLocator.GetUser), error.Message);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AsyncOnlyExportServiceSupportsBothEntryPoints(bool all, bool legacy)
    {
        var implementation = new AsyncOnlyExportService();
        IExportContentService service = implementation;
        using var cancellation = new CancellationTokenSource();
        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(true);

        if (all)
        {
            ExportAllContentResult result = legacy
                ? await service.ExportAll(rebuild, cancellation.Token)
                : await service.ExportAllAsync(rebuild, cancellation.Token);

            Assert.Same(implementation.AllResult, result);
            Assert.Same(rebuild, implementation.AllRequest);
            Assert.Null(implementation.Request);
        }
        else
        {
            ExportContentResult result = legacy
                ? await service.Export(content, cancellation.Token)
                : await service.ExportAsync(content, cancellation.Token);

            Assert.Same(implementation.Result, result);
            Assert.Same(content, implementation.Request);
            Assert.Null(implementation.AllRequest);
        }

        Assert.Equal(cancellation.Token, implementation.Token);
        Assert.Equal(1, implementation.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MissingExportImplementationsFailClearlyForEachOperationAndEntryPoint(bool all, bool legacy)
    {
        IExportContentService service = new MissingExportService();
        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(false);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            all
                ? legacy
                    ? service.ExportAll(rebuild, default)
                    : service.ExportAllAsync(rebuild, default)
                : legacy
                    ? service.Export(content, default)
                    : service.ExportAsync(content, default));

        Assert.Contains(nameof(MissingExportService), error.Message);
        Assert.Contains(
            all ? nameof(IExportContentService.ExportAllAsync) : nameof(IExportContentService.ExportAsync),
            error.Message);
    }

    [Fact]
    public async Task ImplementingOneExportOperationDoesNotSatisfyTheOther()
    {
        IExportContentService service = new SingleExportService();

        await service.ExportAsync(new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties), default);
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportAllAsync(new ExportAllContent(false), default));

        Assert.Contains(nameof(IExportContentService.ExportAllAsync), error.Message);
    }

    private class LegacyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        internal int Calls { get; private set; }

        public Task<User> GetUser()
        {
            Calls++;

            return result;
        }
    }

    private sealed class LegacyExportService : IExportContentService
    {
        internal ExportContentResult Result { get; } = new();
        internal ExportAllContentResult AllResult { get; } = new();
        internal ExportContent? Request { get; private set; }
        internal ExportAllContent? AllRequest { get; private set; }
        internal CancellationToken Token { get; private set; }
        internal int Calls { get; private set; }

        public Task<ExportContentResult> Export(ExportContent exportContent, CancellationToken token)
        {
            Request = exportContent;
            Token = token;
            Calls++;

            return Task.FromResult(Result);
        }

        public Task<ExportAllContentResult> ExportAll(ExportAllContent exportAllContent, CancellationToken token)
        {
            AllRequest = exportAllContent;
            Token = token;
            Calls++;

            return Task.FromResult(AllResult);
        }

        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }

    private sealed class AsyncOnlyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        internal int Calls { get; private set; }

        public Task<User> GetUserAsync()
        {
            Calls++;

            return result;
        }
    }

    private sealed class ExplicitLegacyUserLocator(Task<User> result) : IRelewiseUserLocator
    {
        Task<User> IRelewiseUserLocator.GetUser()
        {
            return result;
        }
    }

    private sealed class InheritedLegacyUserLocator(Task<User> result) : LegacyUserLocator(result)
    {
    }

    private sealed class MissingUserLocator : IRelewiseUserLocator
    {
    }

    private sealed class AsyncOnlyExportService : IExportContentService
    {
        internal ExportContentResult Result { get; } = new();
        internal ExportAllContentResult AllResult { get; } = new();
        internal ExportContent? Request { get; private set; }
        internal ExportAllContent? AllRequest { get; private set; }
        internal CancellationToken Token { get; private set; }
        internal int Calls { get; private set; }

        public Task<ExportContentResult> ExportAsync(ExportContent exportContent, CancellationToken cancellationToken)
        {
            Request = exportContent;
            Token = cancellationToken;
            Calls++;

            return Task.FromResult(Result);
        }

        public Task<ExportAllContentResult> ExportAllAsync(
            ExportAllContent exportAllContent,
            CancellationToken cancellationToken)
        {
            AllRequest = exportAllContent;
            Token = cancellationToken;
            Calls++;

            return Task.FromResult(AllResult);
        }

        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }

    private sealed class MissingExportService : IExportContentService
    {
        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }

    private sealed class SingleExportService : IExportContentService
    {
        public Task<ExportContentResult> ExportAsync(ExportContent exportContent, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExportContentResult());
        }

        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }

}
