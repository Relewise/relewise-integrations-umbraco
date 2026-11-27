using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Indexing;
#endif
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
#endif

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class SearchExportOperationsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceIsResolvedAndAsynchronouslyDisposedPerExport(bool fail)
    {
        var sources = new List<ScopedPublishedStrategy>();
        await using var fixture = new Fixture(
            configureServices: services =>
                services.AddScoped<IPublishedContentChangeStrategy>(_ =>
                {
                    var source = new ScopedPublishedStrategy(fail);
                    sources.Add(source);
                    return source;
                }));

        for (var i = 0; i < 2; i++)
        {
            if (fail)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.ExportAsync([]));
            }
            else
            {
                await fixture.Writer.ExportAsync([]);
            }

            Assert.Equal(i + 1, sources.Count);
            Assert.All(sources, source => Assert.True(source.Disposed));
        }

        fixture.Strategy.VerifyNoOtherCalls();
        Assert.Empty(fixture.Tracked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOperationsDoNotReadOrWriteEntities(bool delete)
    {
        await using var fixture = new Fixture();
        var token = new CancellationToken(true);

        Task operation = delete
            ? fixture.Writer.DeleteAsync([new DeletedEntity(typeof(Product), "sku", Guid.NewGuid(), true)], token)
            : fixture.Writer.ExportAsync([Guid.NewGuid()], token: token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Empty(fixture.Tracked);
        fixture.Reader.VerifyNoOtherCalls();
        fixture.Strategy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RawDraftRemovalIsReconciledEvenWhenThePublishedStrategyIgnoresIt()
    {
        await using var fixture = new Fixture();
        Guid removed = Guid.NewGuid(), draft = Guid.NewGuid();

        await fixture.Writer.ApplyChangesAsync(
            [
                new ExportChange(removed, ExportChangeImpact.Remove, false),
                new ExportChange(draft, ExportChangeImpact.Refresh, false)
            ],
            CancellationToken.None);

        var cleanup =
            Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
        string filters = JsonConvert.SerializeObject(cleanup.Filters);

        Assert.Contains(removed.ToString("D"), filters);
        Assert.DoesNotContain(draft.ToString("D"), filters);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableMetadataNeverReplacesBusinessCollections(bool product)
    {
        await using var fixture = new Fixture();

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([new ProductResultDetails("external")], 1));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentDetailsCollectionResponse([new ContentResultDetails("external")], 1));
        await fixture.Writer.DeleteAsync(
            [
                new DeletedEntity(
                    product ? typeof(Product) : typeof(Client.DataTypes.Content),
                    "external",
                    Guid.NewGuid(),
                    false)
            ],
            CancellationToken.None);

        Trackable action = Assert.Single(Assert.Single(fixture.Tracked));

        if (product)
        {
            var update = Assert.IsType<ProductUpdate>(action);

            Assert.Null(update.Product.Assortments);
            Assert.Null(update.Product.CategoryPaths);
            Assert.False(update.ReplaceExistingVariants);
            Assert.Equal("rwUmbracoPublished", Assert.Single(update.Product.Data!).Key);
        }
        else
        {
            var update = Assert.IsType<ContentUpdate>(action);

            Assert.Null(update.Content.Assortments);
            Assert.Null(update.Content.CategoryPaths);
            Assert.Equal("rwUmbracoPublished", Assert.Single(update.Content.Data!).Key);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeletionIsQueuedOnlyAfterSuccessfulCommit(bool completed)
    {
        await using var fixture = new Fixture();
        var transaction = new TestScopeContext();
        var scopes = new Mock<ICoreScopeProvider>();

        scopes.SetupGet(value => value.Context).Returns(transaction);

        var queue = new Mock<IBackgroundTaskQueue>();
        Func<CancellationToken, Task>? work = null;

        queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(value => work = value);

        var node = new Mock<IContent>();

        node.SetupGet(value => value.ContentType.Alias).Returns("article");
        node.SetupGet(value => value.Id).Returns(42);
        node.SetupGet(value => value.Key).Returns(Guid.NewGuid());

        var writer = new EntityIndexingQueue(
            fixture.Writer,
            queue.Object,
            Mock.Of<IHostApplicationLifetime>(),
            NullLogger<EntityIndexingQueue>.Instance);
        var exports = new Mock<IExportSystem>();
        exports.Setup(value => value.EnqueueDeletion(It.IsAny<DeletedEntity[]>(), It.IsAny<Guid[]>()))
            .Callback<DeletedEntity[], Guid[]>(writer.EnqueueDeletion);
        var handler = new DeletedEntityIndexingHandler(
            fixture.Services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            Mock.Of<IContentService>(),
            scopes.Object,
            exports.Object,
            NullLogger<DeletedEntityIndexingHandler>.Instance);

        handler.Handle(new ContentDeletedNotification(node.Object, new EventMessages()));
        Assert.Null(work);
        transaction.ScopeExit(completed);

        if (completed)
        {
            Assert.NotNull(work);
            await work(CancellationToken.None);
            Assert.Contains("42", JsonConvert.SerializeObject(Assert.Single(Assert.Single(fixture.Tracked))));
        }
        else
        {
            Assert.Null(work);
        }
    }

    [Fact]
    public async Task TerminalDeletionUsesTheCapturedCanonicalIdentityWithoutARead()
    {
        await using var fixture = new Fixture();

        await fixture.Writer.DeleteAsync(
            [new DeletedEntity(typeof(Client.DataTypes.Content), "external-id", Guid.NewGuid(), true)],
            CancellationToken.None);

        var action =
            Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));

        Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, action.Kind);
        Assert.Contains("external-id", JsonConvert.SerializeObject(action.Filters));
        fixture.Reader.Verify(
            value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TerminalDeletionDoesNotDeleteAnExternallyManagedProduct()
    {
        await using var fixture = new Fixture();

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([], null));
        await fixture.Writer.DeleteAsync(
            [new DeletedEntity(typeof(Product), "external-product", Guid.NewGuid(), false)],
            CancellationToken.None);
        Assert.Empty(fixture.Tracked);
        fixture.Reader.Verify(
            value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RebuildWithoutLocalRecordsScopesReconciliationToMappedSources()
    {
        await using var fixture = new Fixture();

        await fixture.Writer.RebuildAsync();

        var cleanup =
            Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));

        Assert.Equal(ContentAdministrativeAction.UpdateKind.Disable, cleanup.Kind);

        string json = JsonConvert.SerializeObject(cleanup.Filters);

        Assert.DoesNotContain("rwUmbracoSite", json);
        Assert.Contains("rwUmbracoNodeKey", json);
        Assert.Contains("HasValueCondition", json);
        Assert.Contains("article", json);
        Assert.Contains("UmbracoVersionId", json);
        Assert.DoesNotContain("rwUmbracoExportGeneration", json);
        Assert.Null(fixture.Services.GetService<IKeyValueService>());
    }

    [Fact]
    public async Task FailedRebuildNeverReconcilesAndReleasesTheWriter()
    {
        await using var fixture = new Fixture();

        fixture.Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fixture export failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.RebuildAsync());
        Assert.Empty(fixture.Tracked);
        fixture.Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await fixture.Writer.RebuildAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(fixture.Tracked);
    }

    [Fact]
    public async Task FullExportsUseDistinctNumericVersionsWhenTheClockDoesNotAdvance()
    {
        DateTimeOffset time = DateTimeOffset.FromUnixTimeMilliseconds(1800000000000);
        await using var fixture = new Fixture(clock: new FrozenClock(time));

        await fixture.Writer.RebuildAsync();
        await fixture.Writer.RebuildAsync();

        var versions = new List<double>();

        foreach (Trackable[] batch in fixture.Tracked)
        {
            var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(batch));
            DataFilter version = action.Filters.Items!.OfType<DataFilter>()
                .Single(filter => filter.Key == "UmbracoVersionId");
            var condition = Assert.IsType<EqualsCondition>(Assert.Single(version.Conditions!.Items!));

            Assert.True(condition.Negated);
            Assert.False(version.FilterOutIfKeyIsNotFound);
            versions.Add(condition.Value.ValueAs<double>());
        }

        Assert.Equal(new[] { time.ToUnixTimeMilliseconds(), time.ToUnixTimeMilliseconds() + 1d }, versions);
    }

    [Fact]
    public async Task CancelledFullExportDoesNotRunVersionReconciliation()
    {
        await using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();

        fixture.Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
            .Callback(cancellation.Cancel)
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Writer.RebuildAsync(token: cancellation.Token));

        Assert.Empty(fixture.Tracked);
        fixture.Reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FullExportDoesNotReuseThePrecedingExplicitDeltaVersion()
    {
        DateTimeOffset time = DateTimeOffset.FromUnixTimeMilliseconds(1800000000000);
        await using var fixture = new Fixture(clock: new FrozenClock(time));

        await fixture.Writer.ExportAsync([], feedVersion: time.ToUnixTimeMilliseconds());
        await fixture.Writer.RebuildAsync();

        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
        DataFilter version = action.Filters.Items!.OfType<DataFilter>()
            .Single(filter => filter.Key == "UmbracoVersionId");
        var condition = Assert.IsType<EqualsCondition>(Assert.Single(version.Conditions!.Items!));

        Assert.Equal(time.ToUnixTimeMilliseconds() + 1d, condition.Value.ValueAs<double>());
    }

    [Fact]
    public async Task RebuildRespectsExportAndAdministrativePoliciesWithinMappingScope()
    {
        await using var fixture = new Fixture(options =>
        {
            options.MapContent("article");
            options.MapContent("excluded", mapping => mapping.Export = false);
            options.MapProduct("product", mapping => mapping.AdministrativeActions = AdministrativeActionPolicy.None());
        });

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([new ProductResultDetails("commerce-product")], 1));

        await fixture.Writer.RebuildAsync();

        Trackable[] actions = [.. fixture.Tracked.SelectMany(batch => batch)];
        ProductUpdate update = Assert.Single(actions.OfType<ProductUpdate>());

        Assert.Equal("commerce-product", update.Product.Id);
        Assert.False(update.Product.Data![RelewiseDataKeys.Published]!.ValueAs<bool>());
        Assert.Empty(actions.OfType<ProductAdministrativeAction>());
        Assert.Single(actions.OfType<ContentAdministrativeAction>());

        foreach (IInvocation invocation in fixture.Reader.Invocations)
        {
            string query = JsonConvert.SerializeObject(invocation.Arguments[0]);

            Assert.Contains("rwUmbracoNodeKey", query);
            Assert.Contains("HasValueCondition", query);
            Assert.Contains("contentTypeAlias", query);
            Assert.Contains("UmbracoVersionId", query);
            Assert.DoesNotContain("rwUmbracoExportGeneration", query);
            Assert.DoesNotContain("excluded", query);
            Assert.DoesNotContain("rwUmbracoSite", query);
        }
    }

    [Fact]
    public async Task ConcurrentRebuildsUseTheSharedQueueWithoutDatabaseLeases()
    {
        await using var fixture = new Fixture();
        var writer = new EntityIndexingQueue(
            fixture.Writer,
            Mock.Of<IBackgroundTaskQueue>(),
            Mock.Of<IHostApplicationLifetime>(),
            NullLogger<EntityIndexingQueue>.Instance);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, maximum = 0;

        fixture.Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                maximum = Math.Max(maximum, Interlocked.Increment(ref active));
                entered.TrySetResult();
                await release.Task;
                Interlocked.Decrement(ref active);
            });

        Task first = writer.RebuildAsync();

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Task second = writer.RebuildAsync();

        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task DeletedSourceIsTargetedByNodeKeyRatherThanItsCurrentCustomId()
    {
        await using var fixture = new Fixture();
        var key = Guid.NewGuid();

        await fixture.Writer.ExportAsync([key]);

        var action =
            Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));

        Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, action.Kind);
        Assert.Contains(key.ToString("D"), JsonConvert.SerializeObject(action.Filters));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ServiceProvider Services { get; }
        public SearchExportOperations Writer { get; }
        public Mock<IPublishedContentChangeStrategy> Strategy { get; } = new();
        public Mock<IDataAccessor> Reader { get; } = new();
        public List<Trackable[]> Tracked { get; } = [];

        public Fixture(
            Action<RelewiseUmbracoOptionsBuilder>? configure = null,
            TimeProvider? clock = null,
            Action<IServiceCollection>? configureServices = null)
        {
            var tracker = new Mock<ITracker>();

            tracker.Setup(value => value.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => Tracked.Add([.. items]))
                .Returns(Task.CompletedTask);

            Mock<IDataAccessor> reader = Reader;

            reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], 0));

            var clients = new Mock<IRelewiseClientFactory>();

            clients.Setup(value => value.GetClient<ITracker>(It.IsAny<string>())).Returns(tracker.Object);
            clients.Setup(value => value.GetClient<IDataAccessor>(It.IsAny<string>())).Returns(reader.Object);

            var contexts = new Mock<IUmbracoContextFactory>();

            contexts.Setup(value => value.EnsureUmbracoContext())
                .Returns(() =>
                    new UmbracoContextReference(Mock.Of<IUmbracoContext>(), false, Mock.Of<IUmbracoContextAccessor>()));

            var languages = new Mock<ILanguageService>();

            languages.Setup(value => value.GetAllAsync()).ReturnsAsync(Array.Empty<ILanguage>());
            Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Strategy.Setup(value => value.HandleAsync(
                    It.IsAny<IEnumerable<ContentIndexInfo>>(),
                    It.IsAny<IEnumerable<ContentChange>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton(clock ?? TimeProvider.System);
            services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
                (configure ?? (builder => builder.MapContent("article", mapping => mapping.AutoMap())))(options));
            services.AddSingleton<RelewiseUmbracoConfiguration>();
            services.AddSingleton(clients.Object);
            services.AddSingleton(contexts.Object);
            services.AddSingleton(languages.Object);
            services.AddSingleton(Strategy.Object);
            services.AddSingleton(Mock.Of<IDocumentNavigationQueryService>());
            services.AddScoped<UmbracoSearchExportSource>();
            services.AddSingleton(Mock.Of<IContentService>());
            services.AddSingleton<SearchExportOperations>();
            configureServices?.Invoke(services);
            Services = services.BuildServiceProvider(
                new ServiceProviderOptions
                { ValidateScopes = true, ValidateOnBuild = true });
            Writer = Services.GetRequiredService<SearchExportOperations>();
        }

        public ValueTask DisposeAsync()
        {
            return Services.DisposeAsync();
        }
    }

    private sealed class FrozenClock(DateTimeOffset time) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return time;
        }
    }

    private sealed class ScopedPublishedStrategy(bool fail) : IPublishedContentChangeStrategy, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public Task HandleAsync(
            IEnumerable<ContentIndexInfo> indexes,
            IEnumerable<ContentChange> changes,
            CancellationToken token) => RebuildAsync(Assert.Single(indexes), token);

        public Task RebuildAsync(ContentIndexInfo index, CancellationToken token)
        {
            Assert.False(Disposed);
            return fail ? Task.FromException(new InvalidOperationException("Source failed")) : Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Disposed = true;
        }
    }
}
