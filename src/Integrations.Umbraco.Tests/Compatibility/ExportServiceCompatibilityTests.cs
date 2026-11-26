#pragma warning disable CS0618 // Exercise the retained public API and custom implementations written against it.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Tests.Compatibility;

public class ExportServiceCompatibilityTests
{
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
        var key = Guid.NewGuid();
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
        var exports = new Mock<IExportSystem>();
        exports.Setup(value => value.ExportAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<ContentUpdate.UpdateKind?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Guid> keys, ContentUpdate.UpdateKind? kind, long? version, CancellationToken token) =>
                queue.ExportAsync(keys, kind, version, token));
        exports.Setup(value => value.RebuildAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((bool permanent, CancellationToken token) => queue.RebuildAsync(permanent, token));
        IExportContentService service = new ExportContentService(exports.Object, Mock.Of<IServiceProvider>());
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

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportAllAsync(new ExportAllContent(false), default));

        Assert.Contains(nameof(IExportContentService.ExportAllAsync), error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyExportImplementationsCanBeInheritedOrExplicit(bool explicitImplementation)
    {
        LegacyExportService implementation = explicitImplementation
            ? new ExplicitLegacyExportService()
            : new InheritedLegacyExportService();
        IExportContentService service = implementation;
        using var cancellation = new CancellationTokenSource();
        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(true);

        Assert.Same(implementation.Result, await service.ExportAsync(content, cancellation.Token));
        Assert.Same(implementation.AllResult, await service.ExportAllAsync(rebuild, cancellation.Token));
        Assert.Same(content, implementation.Request);
        Assert.Same(rebuild, implementation.AllRequest);
        Assert.Equal(cancellation.Token, implementation.Token);
        Assert.Equal(2, implementation.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncExportImplementationsCanBeInheritedOrExplicit(bool explicitImplementation)
    {
        AsyncOnlyExportService implementation = explicitImplementation
            ? new ExplicitAsyncExportService()
            : new InheritedAsyncExportService();
        IExportContentService service = implementation;
        using var cancellation = new CancellationTokenSource();
        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(true);

        Assert.Same(implementation.Result, await service.Export(content, cancellation.Token));
        Assert.Same(implementation.AllResult, await service.ExportAll(rebuild, cancellation.Token));
        Assert.Same(content, implementation.Request);
        Assert.Same(rebuild, implementation.AllRequest);
        Assert.Equal(cancellation.Token, implementation.Token);
        Assert.Equal(2, implementation.Calls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ExportDefaultsPreserveFailureAndCancellation(bool all, bool asyncOnly, bool cancelled)
    {
        var failure = new InvalidOperationException("Export failed.");
        IExportContentService service = asyncOnly
            ? new FailingAsyncExportService(failure, cancelled)
            : new FailingLegacyExportService(failure, cancelled);
        using var cancellation = new CancellationTokenSource();

        if (cancelled)
        {
            cancellation.Cancel();
        }

        var content = new ExportContent([], ContentUpdate.UpdateKind.ReplaceProvidedProperties);
        var rebuild = new ExportAllContent(false);
        Task pending = (all, asyncOnly) switch
        {
            (true, true) => service.ExportAll(rebuild, cancellation.Token),
            (true, false) => service.ExportAllAsync(rebuild, cancellation.Token),
            (false, true) => service.Export(content, cancellation.Token),
            (false, false) => service.ExportAsync(content, cancellation.Token)
        };

        if (cancelled)
        {
            var error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.True(pending.IsCanceled);
        }
        else
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => pending));
            Assert.True(pending.IsFaulted);
        }
    }

    private class LegacyExportService : IExportContentService
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

    private class AsyncOnlyExportService : IExportContentService
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

    private sealed class InheritedLegacyExportService : LegacyExportService
    {
    }

    private sealed class ExplicitLegacyExportService : LegacyExportService, IExportContentService
    {
        Task<ExportContentResult> IExportContentService.Export(ExportContent content, CancellationToken token)
        {
            return Export(content, token);
        }

        Task<ExportAllContentResult> IExportContentService.ExportAll(ExportAllContent content, CancellationToken token)
        {
            return ExportAll(content, token);
        }
    }

    private sealed class InheritedAsyncExportService : AsyncOnlyExportService
    {
    }

    private sealed class ExplicitAsyncExportService : AsyncOnlyExportService, IExportContentService
    {
        Task<ExportContentResult> IExportContentService.ExportAsync(ExportContent content, CancellationToken token)
        {
            return ExportAsync(content, token);
        }

        Task<ExportAllContentResult> IExportContentService.ExportAllAsync(
            ExportAllContent content,
            CancellationToken token)
        {
            return ExportAllAsync(content, token);
        }
    }

    private sealed class FailingLegacyExportService(Exception failure, bool cancelled) : IExportContentService
    {
        public Task<ExportContentResult> Export(ExportContent content, CancellationToken token)
        {
            return cancelled
                ? Task.FromCanceled<ExportContentResult>(token)
                : Task.FromException<ExportContentResult>(failure);
        }

        public Task<ExportAllContentResult> ExportAll(ExportAllContent content, CancellationToken token)
        {
            return cancelled
                ? Task.FromCanceled<ExportAllContentResult>(token)
                : Task.FromException<ExportAllContentResult>(failure);
        }

        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }

    private sealed class FailingAsyncExportService(Exception failure, bool cancelled) : IExportContentService
    {
        public Task<ExportContentResult> ExportAsync(ExportContent content, CancellationToken cancellationToken)
        {
            return cancelled
                ? Task.FromCanceled<ExportContentResult>(cancellationToken)
                : Task.FromException<ExportContentResult>(failure);
        }

        public Task<ExportAllContentResult> ExportAllAsync(
            ExportAllContent content,
            CancellationToken cancellationToken)
        {
            return cancelled
                ? Task.FromCanceled<ExportAllContentResult>(cancellationToken)
                : Task.FromException<ExportAllContentResult>(failure);
        }

        public ITracker? GetTrackerOrNull()
        {
            return null;
        }
    }
}
