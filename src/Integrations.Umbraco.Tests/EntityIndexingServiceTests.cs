using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Relewise.Integrations.Umbraco.Tests;

public class EntityIndexingServiceTests
{
    [Fact]
    public async Task RawDraftRemovalIsReconciledEvenWhenThePublishedStrategyIgnoresIt()
    {
        await using var fixture = new Fixture();
        Guid removed = Guid.NewGuid(), draft = Guid.NewGuid();
        await fixture.Writer.ApplyChangesAsync([
            ContentChange.Document(removed, ChangeImpact.Remove, ContentState.Draft),
            ContentChange.Document(draft, ChangeImpact.Refresh, ContentState.Draft)], CancellationToken.None);
        var cleanup = Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
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
        await fixture.Writer.DeleteAsync([new DeletedEntity(product ? typeof(Product) : typeof(Relewise.Client.DataTypes.Content), "external", Guid.NewGuid(), false)], CancellationToken.None);
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
        var handler = new DeletedEntityIndexingHandler(fixture.Services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            Mock.Of<IContentService>(), scopes.Object,
            new EntityIndexingQueue(fixture.Writer, queue.Object, Mock.Of<IHostApplicationLifetime>(), NullLogger<EntityIndexingQueue>.Instance),
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
        else Assert.Null(work);
    }

    [Fact]
    public async Task TerminalDeletionUsesTheCapturedCanonicalIdentityWithoutARead()
    {
        await using var fixture = new Fixture();
        await fixture.Writer.DeleteAsync([new DeletedEntity(typeof(Relewise.Client.DataTypes.Content), "external-id", Guid.NewGuid(), true)], CancellationToken.None);
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, action.Kind);
        Assert.Contains("external-id", JsonConvert.SerializeObject(action.Filters));
        fixture.Reader.Verify(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TerminalDeletionDoesNotDeleteAnExternallyManagedProduct()
    {
        await using var fixture = new Fixture();
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([], null));
        await fixture.Writer.DeleteAsync([new DeletedEntity(typeof(Product), "external-product", Guid.NewGuid(), false)], CancellationToken.None);
        Assert.Empty(fixture.Tracked);
        fixture.Reader.Verify(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RebuildWithoutLocalRecordsScopesReconciliationToItsSiteAndSource()
    {
        await using var fixture = new Fixture();
        await fixture.Writer.RebuildAsync();
        var cleanup = Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Disable, cleanup.Kind);
        string json = JsonConvert.SerializeObject(cleanup.Filters);
        Assert.Contains("test-installation", json);
        Assert.Contains("article", json);
        Assert.Contains("rwUmbracoExportGeneration", json);
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
        fixture.Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await fixture.Writer.RebuildAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(fixture.Tracked);
    }

    [Fact]
    public async Task ConcurrentRebuildsAreSerializedWithoutDatabaseLeases()
    {
        await using var fixture = new Fixture();
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
        Task first = fixture.Writer.RebuildAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task second = fixture.Writer.RebuildAsync();
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task DeletedSourceIsTargetedByNodeKeyRatherThanItsCurrentCustomId()
    {
        await using var fixture = new Fixture();
        Guid key = Guid.NewGuid();
        await fixture.Writer.ExportAsync([key]);
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(Assert.Single(fixture.Tracked)));
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Delete, action.Kind);
        Assert.Contains(key.ToString("D"), JsonConvert.SerializeObject(action.Filters));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
        public int CreatedThreadId { get; } = Environment.CurrentManagedThreadId;
        private readonly Dictionary<string, object?> _items = new();
        private readonly Dictionary<string, Action<bool>> _actions = new();
        public void Enlist(string key, Action<bool> action, int priority = 100) => _actions.TryAdd(key, action);
        public T? Enlist<T>(string key, Func<T> creator, Action<bool, T?>? action = null, int priority = 100)
        {
            if (!_items.ContainsKey(key))
            {
                T value = creator();
                _items.Add(key, value);
                if (action != null) _actions.Add(key, completed => action(completed, value));
            }
            return (T?)_items[key];
        }
        public T? GetEnlisted<T>(string key) => _items.TryGetValue(key, out object? value) ? (T?)value : default;
        public void ScopeExit(bool completed) { foreach (var action in _actions.Values) action(completed); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ServiceProvider Services { get; }
        public EntityIndexingService Writer { get; }
        public Mock<IPublishedContentChangeStrategy> Strategy { get; } = new();
        public Mock<IDataAccessor> Reader { get; } = new();
        public List<Trackable[]> Tracked { get; } = [];

        public Fixture()
        {
            var tracker = new Mock<ITracker>();
            tracker.Setup(value => value.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => Tracked.Add(items.ToArray())).Returns(Task.CompletedTask);
            var reader = Reader;
            reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], 0));
            var clients = new Mock<IRelewiseClientFactory>();
            clients.Setup(value => value.GetClient<ITracker>(It.IsAny<string>())).Returns(tracker.Object);
            clients.Setup(value => value.GetClient<IDataAccessor>(It.IsAny<string>())).Returns(reader.Object);
            var contexts = new Mock<IUmbracoContextFactory>();
            contexts.Setup(value => value.EnsureUmbracoContext()).Returns(() =>
                new UmbracoContextReference(Mock.Of<IUmbracoContext>(), false, Mock.Of<IUmbracoContextAccessor>()));
            var languages = new Mock<ILanguageService>();
            languages.Setup(value => value.GetAllAsync()).ReturnsAsync(Array.Empty<ILanguage>());
            Strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Strategy.Setup(value => value.HandleAsync(It.IsAny<IEnumerable<ContentIndexInfo>>(), It.IsAny<IEnumerable<ContentChange>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<global::Umbraco.Cms.Core.Configuration.Models.GlobalSettings>(options => options.Id = "test-installation");
            services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.MapContent("article", mapping => mapping.AutoMap()));
            services.AddSingleton<RelewiseUmbracoConfiguration>();
            services.AddSingleton(clients.Object);
            services.AddSingleton(contexts.Object);
            services.AddSingleton(languages.Object);
            services.AddSingleton(Strategy.Object);
            services.AddSingleton(Mock.Of<IContentService>());
            services.AddSingleton<EntityIndexingService>();
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Writer = Services.GetRequiredService<EntityIndexingService>();
        }

        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }
}
