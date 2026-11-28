using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class NotificationExportOperationsTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task OnlyCulturesWithAPublishedAncestorPathAreExported(
        bool rebuild,
        bool englishPublished,
        bool danishPublished)
    {
        await using var fixture = new Fixture(configureMapping: options => options.MapBrand("article", mapping =>
        {
            mapping.AdministrativeActions = AdministrativeActionPolicy.None();
            mapping.UseMapper<CultureMapper>();
        }));
        IPublishedContent node = fixture.PublishedArticle();
        var ancestor = new Mock<IPublishedContent>();
        ancestor.SetupGet(value => value.ContentType.Variations).Returns(ContentVariation.Culture);
        string[] available = [.. new[] { "en-US", "da-DK" }.Where(culture =>
            culture == "en-US" ? englishPublished : danishPublished)];
        ancestor.SetupGet(value => value.Cultures).Returns(available.ToDictionary(
            culture => culture,
            culture => new PublishedCultureInfo(culture, culture, culture, DateTime.UnixEpoch)));
        var ancestorKey = Guid.NewGuid();
        IEnumerable<Guid> ancestors = [ancestorKey];
        fixture.Navigation.Setup(value => value.TryGetAncestorsKeys(node.Key, out ancestors)).Returns(true);
        fixture.Cache.Setup(value => value.GetById(false, ancestorKey)).Returns(ancestor.Object);

        if (rebuild)
        {
            await fixture.Exports.RebuildAsync();
        }
        else
        {
            await fixture.Exports.ApplyChangesAsync(
                [new ExportChange(node.Key, ExportChangeImpact.Remove, true)],
                CancellationToken.None);
        }

        BrandUpdate[] updates = [.. fixture.Sent.OfType<BrandUpdate>()];
        if (englishPublished)
        {
            BrandUpdate update = Assert.Single(updates);
            string[] expected = danishPublished ? ["en-US", "da-DK"] : ["en-US"];
            Assert.NotNull(update.Brand.Data);
            Assert.Equal(expected, update.Brand.Data["mappedCultures"]!.ValueAs<List<string>>());
        }
        else
        {
            Assert.Empty(updates);
        }
    }

    [Fact]
    public async Task FullExportWorksWithoutUmbracoSearchAndReconcilesMappedEntities()
    {
        await using var fixture = new Fixture();
        await fixture.Exports.RebuildAsync();
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(fixture.Sent));
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Disable, action.Kind);
        fixture.Content.Verify(value => value.GetRootContent(), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingPublishedContentIsDisabledOrDeletedAccordingToSourceAvailability(bool deleted)
    {
        await using var fixture = new Fixture();
        var key = Guid.NewGuid();
        if (!deleted)
        {
            fixture.Content.Setup(value => value.GetById(key))
                .Returns(Mock.Of<IContent>(value => value.ContentType.Alias == "article"));
        }

        await fixture.Exports.ExportAsync([key]);
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(fixture.Sent));
        Assert.Equal(
            deleted ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable,
            action.Kind);
    }

    [Fact]
    public async Task CancelledExportDoesNotReadContentOrSendUpdates()
    {
        await using var fixture = new Fixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Exports.RebuildAsync(token: new CancellationToken(true)));
        fixture.Content.VerifyNoOtherCalls();
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentServicesAreResolvedAndAsynchronouslyDisposedPerOperation(bool fail)
    {
        var disposals = new List<bool>();
        await using var fixture = new Fixture(services => services.AddScoped<IContentService>(_ =>
        {
            int index = disposals.Count;
            disposals.Add(false);
            var content = new Mock<IContentService>();
            content.Setup(value => value.GetRootContent())
                .Returns(() =>
                {
                    Assert.False(disposals[index]);
                    if (fail)
                        throw new InvalidOperationException("Source failure");
                    return [];
                });
            content.As<IAsyncDisposable>()
                .Setup(value => value.DisposeAsync())
                .Returns(async () =>
                {
                    await Task.Yield();
                    disposals[index] = true;
                });
            return content.Object;
        }));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (fail)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Exports.RebuildAsync());
            }
            else
            {
                await fixture.Exports.RebuildAsync();
            }

            Assert.Equal(attempt + 1, disposals.Count);
            Assert.All(disposals, disposed => Assert.True(disposed));
        }

        if (fail)
            Assert.Empty(fixture.Sent);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public Mock<IContentService> Content { get; } = new();
        public Mock<IPublishedContentCache> Cache { get; } = new();
        public Mock<IDocumentNavigationQueryService> Navigation { get; } = new();
        public List<Trackable> Sent { get; } = [];
        public NotificationExportOperations Exports { get; }

        public Fixture(
            Action<IServiceCollection>? configure = null,
            Action<RelewiseUmbracoOptionsBuilder>? configureMapping = null)
        {
            Content.Setup(value => value.GetRootContent()).Returns([]);
            var languages = new Mock<ILanguageService>();
            languages.Setup(value => value.GetAllAsync()).ReturnsAsync(Array.Empty<ILanguage>());
            var tracker = new Mock<ITracker>();
            tracker.Setup(value => value.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => Sent.AddRange(items))
                .Returns(Task.CompletedTask);
            var reader = new Mock<IDataAccessor>();
            reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], 0));
            var clients = new Mock<IRelewiseClientFactory>();
            clients.Setup(value => value.GetClient<ITracker>(It.IsAny<string>())).Returns(tracker.Object);
            clients.Setup(value => value.GetClient<IDataAccessor>(It.IsAny<string>())).Returns(reader.Object);
            var contexts = new Mock<IUmbracoContextFactory>();
            var context = new Mock<IUmbracoContext>();
            context.SetupGet(value => value.Content).Returns(Cache.Object);
            contexts.Setup(value => value.EnsureUmbracoContext())
                .Returns(() =>
                    new UmbracoContextReference(context.Object, false, Mock.Of<IUmbracoContextAccessor>()));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
            {
                if (configureMapping == null)
                {
                    options.MapContent("article");
                }
                else
                {
                    configureMapping(options);
                }
            });
            services.AddSingleton<RelewiseUmbracoConfiguration>();
            services.AddSingleton(Content.Object);
            services.AddSingleton(languages.Object);
            services.AddSingleton(clients.Object);
            services.AddSingleton(contexts.Object);
            services.AddSingleton(Navigation.Object);
            services.AddSingleton(Mock.Of<IPublicAccessService>());
            services.AddSingleton<NotificationExportOperations>();
            configure?.Invoke(services);
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Exports = _services.GetRequiredService<NotificationExportOperations>();
        }

        public ValueTask DisposeAsync() => _services.DisposeAsync();

        public IPublishedContent PublishedArticle()
        {
            var type = new Mock<IPublishedContentType>();
            type.SetupGet(value => value.Alias).Returns("article");
            type.SetupGet(value => value.Variations).Returns(ContentVariation.Culture);
            var node = new Mock<IPublishedContent>();
            node.SetupGet(value => value.Id).Returns(42);
            node.SetupGet(value => value.Key).Returns(Guid.NewGuid());
            node.SetupGet(value => value.Path).Returns("-1,42");
            node.SetupGet(value => value.ContentType).Returns(type.Object);
            node.SetupGet(value => value.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>
            {
                ["en-US"] = new("en-US", "English", "english", DateTime.UnixEpoch),
                ["da-DK"] = new("da-DK", "Danish", "danish", DateTime.UnixEpoch)
            });
            Cache.Setup(value => value.GetById(node.Object.Key)).Returns(node.Object);

            var editable = Mock.Of<IContent>(value => value.Key == node.Object.Key && value.Id == 42);
            Content.Setup(value => value.GetById(node.Object.Key)).Returns(editable);
            Content.Setup(value => value.GetRootContent()).Returns([editable]);
            Content.Setup(value => value.GetPagedDescendants(
                    42, 0, 1000, out It.Ref<long>.IsAny, It.IsAny<IQuery<IContent>?>(), It.IsAny<Ordering?>()))
                .Returns([]);
            return node.Object;
        }
    }

    private sealed class CultureMapper : IEntityMapper<Brand>
    {
        public Task MapAsync(EntityMappingContext<Brand> context, CancellationToken token)
        {
            context.Entity.Data!["mappedCultures"] = context.CulturesToPublish.ToArray();
            return Task.CompletedTask;
        }
    }
}
