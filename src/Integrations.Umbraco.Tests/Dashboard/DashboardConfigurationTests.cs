using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.Extensions;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Controllers.Messages;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Search;

namespace Relewise.Integrations.Umbraco.Tests.Dashboard;

public class DashboardConfigurationTests
{
    [Fact]
    public void SnapshotDescribesCanonicalIdsVariantsPoliciesAndRestrictedSearchWithoutExecutingCallbacks()
    {
        using ServiceProvider services = Services(options =>
        {
            options.UseContentKeysAsIds = true;
            options.MapContent(
                "article",
                mapping =>
                {
                    mapping.Map(entity => entity.DisplayName, "heading");
                    mapping.MapData("body", data => data.DataKey = "searchText");
                    mapping.UseMapper<DashboardContentMapper>();
                });
            options.MapProduct(
                "product",
                mapping =>
                {
                    mapping.IdFromProperty("sku");
                    mapping.Map(
                        entity => entity.ListPrice,
                        "prices",
                        property => property.UseConverter<DashboardPriceConverter>());
                    mapping.AdministrativeActions = AdministrativeActionPolicy.None();
                    mapping.BrandFrom("brandPicker");
                    mapping.VariantsFromChildren("variant", variant => variant.IdFromProperty("variantSku"));
                });
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider(
                    "editorial",
                    provider =>
                    {
                        provider.DocumentTypeAliases = ["article"];
                        provider.ConfigureRequest((_, _) =>
                            throw new InvalidOperationException("Must not run during inspection"));
                    });
                search.AddProductProvider("commerce", provider => provider.RequirePublishedPage = false);
                search.DefaultPublishedContentProviderAlias = "editorial";
            });
        });
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                false);

        Assert.Equal("editorial", snapshot.DefaultPublishedContentProviderAlias);

        DashboardMappingViewModel article =
            Assert.Single(snapshot.Mappings, mapping => mapping.DocumentTypeAlias == "article");

        Assert.Equal("Node key", article.IdSource);
        Assert.Equal(nameof(DashboardContentMapper), article.CustomMapperTypeName);

        DashboardFieldMappingViewModel heading =
            Assert.Single(article.Fields, field => field.SourcePropertyAlias == "heading");

        Assert.Equal("DisplayName", heading.Destination);
        Assert.Equal("Default", heading.Conversion);

        DashboardFieldMappingViewModel body = Assert.Single(
            article.Fields,
            field => field.SourcePropertyAlias == "body");

        Assert.Equal("Data[\"searchText\"]", body.Destination);
        Assert.Equal("Automatic selection", body.Conversion);

        DashboardMappingViewModel
            product = Assert.Single(snapshot.Mappings, mapping => mapping.EntityType == "Product");

        Assert.Equal("Property: sku", product.IdSource);
        Assert.False(product.SynchronizeLifecycle);

        DashboardFieldMappingViewModel price =
            Assert.Single(product.Fields, field => field.SourcePropertyAlias == "prices");

        Assert.Equal("ListPrice", price.Destination);
        Assert.Equal(nameof(DashboardPriceConverter), price.Conversion);
        Assert.Contains(
            product.Relationships,
            relationship => relationship.Contains("brandPicker") && relationship.Contains("configured ID"));

        DashboardMappingViewModel variant =
            Assert.Single(snapshot.Mappings, mapping => mapping.ParentDocumentTypeAlias == "product");

        Assert.Equal("Property: variantSku", variant.IdSource);
        Assert.False(variant.SynchronizeLifecycle);
        Assert.Contains(variant.Relationships, relationship => relationship.Contains("parent Product"));

        DashboardSearchProviderViewModel editorial =
            Assert.Single(snapshot.SearchProviders, provider => provider.Alias == "editorial");

        Assert.Equal(["article"], editorial.DocumentTypeAliases);
        Assert.True(editorial.CustomRequest);
        Assert.Equal("Umbraco search: editorial", editorial.DisplayedAtLocation);
        Assert.False(
            Assert.Single(snapshot.SearchProviders, provider => provider.Alias == "commerce")
                .RequirePublishedPage);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SnapshotReportsDeltaAndFullPoliciesIndependentlyForProductsAndVariants(bool delta, bool full)
    {
        static EntityUpdatePolicy Policy(bool clearDelta, bool clearFull)
        {
            return clearDelta || clearFull
                ? EntityUpdatePolicy.ClearAndReplace(options =>
                {
                    options.DeltaUpdates = clearDelta;
                    options.FullRebuilds = clearFull;
                })
                : EntityUpdatePolicy.ReplaceProvidedProperties();
        }

        using ServiceProvider services = Services(options => options.MapProduct(
            "product",
            mapping =>
            {
                mapping.UpdatePolicy = Policy(delta, full);
                mapping.VariantsFromChildren("variant", variant => variant.UpdatePolicy = Policy(!delta, !full));
            }));
        ConfigurationViewModel snapshot = Snapshot(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            true);
        DashboardMappingViewModel product =
            Assert.Single(snapshot.Mappings, mapping => mapping.EntityType == "Product");
        DashboardMappingViewModel variant =
            Assert.Single(snapshot.Mappings, mapping => mapping.EntityType == "ProductVariant");

        Assert.Equal(delta, product.ClearOnDeltaUpdates);
        Assert.Equal(full, product.ClearOnFullRebuilds);
        Assert.Equal(!delta, variant.ClearOnDeltaUpdates);
        Assert.Equal(!full, variant.ClearOnFullRebuilds);
        Assert.True(variant.SynchronizeLifecycle);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void VariantSnapshotReflectsBothMappingsLifecyclePolicies(bool parent, bool child)
    {
        using ServiceProvider services = Services(options => options.MapProduct(
            "product",
            mapping =>
            {
                mapping.AdministrativeActions = parent
                    ? AdministrativeActionPolicy.Synchronize()
                    : AdministrativeActionPolicy.None();
                mapping.VariantsFromChildren(
                    "variant",
                    variant => variant.AdministrativeActions = child
                        ? AdministrativeActionPolicy.Synchronize()
                        : AdministrativeActionPolicy.None());
            }));
        ConfigurationViewModel snapshot = Snapshot(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            true);
        DashboardMappingViewModel variant =
            Assert.Single(snapshot.Mappings, mapping => mapping.EntityType == "ProductVariant");

        Assert.Equal(parent && child, variant.SynchronizeLifecycle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SearchSectionIsAvailableOnlyWhenSearchExportIsConfigured(bool search)
    {
        using ServiceProvider services = Services(options =>
        {
            options.MapContent("article");
            if (search)
                options.ConfigureSearch();
        });
        ConfigurationViewModel snapshot = Snapshot(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            false);
        Assert.Equal(search, snapshot.SearchConfigured);
        Assert.Empty(snapshot.SearchProviders);
    }

    [Fact]
    public void EmptySnapshotDoesNotInventMappingsTrackingOrDefaultSearch()
    {
        using ServiceProvider services = Services(_ => { });
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                false);

        Assert.Empty(snapshot.Mappings);
        Assert.Empty(snapshot.SearchProviders);
        Assert.Null(snapshot.DefaultPublishedContentProviderAlias);
        Assert.False(snapshot.ContentMiddlewareEnabled);
    }

    [Fact]
    public void TablesOrderMainEntitiesBeforeSupportingEntitiesAndDefaultSearchBeforeNamedProviders()
    {
        using ServiceProvider services = Services(options =>
        {
            options.MapProductCategory("category");
            options.MapContent("zGuide");
            options.MapContentCategory("topic");
            options.MapBrand("brand");
            options.MapProduct("product", mapping => mapping.VariantsFromChildren("aaaVariant"));
            options.MapContent("aArticle");
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider("zAll");
                search.AddProductProvider("catalogue");
                search.AddContentProvider("articles", provider => provider.DocumentTypeAliases = ["aArticle"]);
                search.DefaultPublishedContentProviderAlias = "CATALOGUE";
            });
        });
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                true);

        Assert.Equal(
            new[] { "aArticle", "zGuide", "product", "aaaVariant", "brand", "category", "topic" },
            snapshot.Mappings.Select(mapping => mapping.DocumentTypeAlias));
        Assert.Equal(
            ["catalogue", "articles", "zAll"],
            snapshot.SearchProviders.Select(provider => provider.Alias));
    }

    [Fact]
    public void ConnectionsShowEffectiveIntegrationOverridesBeforeOtherSdkRegistrationsWithoutCredentials()
    {
        const string apiKey = "test-credential-must-not-appear";
        var dataset = Guid.NewGuid();
        using ServiceProvider services = ConnectionServices(options =>
        {
            options.DatasetId = dataset;
            options.ApiKey = apiKey;
            options.ServerUrl = new Uri("https://localhost:5000/");
            options.Timeout = TimeSpan.FromSeconds(5);
            options.Named.Add("zebra", named => named.Timeout = TimeSpan.FromSeconds(15));
            options.Named.Add(
                Constants.ClientNames.Integration,
                named =>
                {
                    named.Tracker.Timeout = TimeSpan.FromSeconds(120);
                    named.Searcher.Timeout = TimeSpan.FromSeconds(12);
                    named.Searcher.ServerUrl = new Uri("https://localhost:5001/");
                });
            options.Named.Add("Alpha", named => named.Timeout = TimeSpan.FromSeconds(10));
            options.Named.Add("Default", named => named.Timeout = TimeSpan.FromSeconds(40));
        });
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var snapshot =
            Assert.IsType<ConfigurationViewModel>(Assert.IsType<OkObjectResult>(controller.Configuration()).Value);

        Assert.Equal(
            ["Tracker", "Searcher", "Data accessor"],
            snapshot.IntegrationClients.Select(client => client.Name));
        Assert.Equal(120, snapshot.IntegrationClients.First().Options.Timeout);

        DashboardClientViewModel searcher =
            Assert.Single(snapshot.IntegrationClients, client => client.Name == "Searcher");

        Assert.Equal(12, searcher.Options.Timeout);
        Assert.Equal("https://localhost:5001/", searcher.Options.ServerUrl);
        Assert.All(snapshot.IntegrationClients, client => Assert.Equal(dataset, client.Options.DatasetId));
        Assert.Equal(
            ["Default", "Alpha", "Default", "zebra"],
            snapshot.Named.Select(registration => registration.Name));
        Assert.True(snapshot.Named[0].IsDefault);
        Assert.False(snapshot.Named[1].IsDefault);
        Assert.False(snapshot.Named[2].IsDefault);
        Assert.Equal(5, snapshot.Named[0].Tracker.Timeout);
        Assert.DoesNotContain(apiKey, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void IntegrationConnectionsRemainAvailableWithoutSdkDefaultsOrOtherRegistrations()
    {
        using ServiceProvider services = ConnectionServices(options => options.Named.Add(
            Constants.ClientNames.Integration,
            named =>
            {
                named.DatasetId = Guid.NewGuid();
                named.ApiKey = "local-test-credential";
                named.Timeout = TimeSpan.FromSeconds(120);
            }));
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var snapshot =
            Assert.IsType<ConfigurationViewModel>(Assert.IsType<OkObjectResult>(controller.Configuration()).Value);

        Assert.Equal(3, snapshot.IntegrationClients.Count);
        Assert.Empty(snapshot.Named);
        Assert.All(snapshot.IntegrationClients, client => Assert.Null(client.Options.ServerUrl));
    }

    [Fact]
    public void ProviderSnapshotResolvesSourcesFallbacksSegmentsAndCaseInsensitiveDefaultWithoutRunningCallbacks()
    {
        using ServiceProvider services = Services(options =>
        {
            options.MapContent("article");
            options.MapContent("guide");
            options.MapContent("viewOnly", mapping => mapping.Export = false);
            options.MapProduct("cataloguePage");
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider(
                    "allContent",
                    provider =>
                    {
                        provider.DisplayedAtLocation = "  ";
                        provider.UnsupportedSegmentBehavior = UnsupportedSegmentBehavior.Ignore();
                        provider.ConfigureRequest((_, _) =>
                            throw new InvalidOperationException("Must not run while inspecting settings"));
                    });
                search.AddContentProvider("articles", provider => provider.DocumentTypeAliases = ["article"]);
                search.AddProductProvider(
                    "catalogue",
                    provider =>
                    {
                        provider.RequirePublishedPage = false;
                        provider.DisplayedAtLocation = "Store catalogue";
                    });
                search.DefaultPublishedContentProviderAlias = "CATALOGUE";
            });
        });
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                true);

        Assert.Equal("catalogue", snapshot.DefaultPublishedContentProviderAlias);

        DashboardSearchProviderViewModel allContent =
            Assert.Single(snapshot.SearchProviders, provider => provider.Alias == "allContent");

        Assert.Empty(allContent.DocumentTypeAliases);
        Assert.Equal(["article", "guide"], allContent.ResolvedDocumentTypeAliases);
        Assert.True(allContent.UsesFallbackLocation);
        Assert.Equal("Umbraco search: allContent", allContent.DisplayedAtLocation);
        Assert.True(allContent.IgnoreSegments);
        Assert.True(allContent.CustomRequest);
        Assert.True(allContent.RequirePublishedPage);

        DashboardSearchProviderViewModel articles =
            Assert.Single(snapshot.SearchProviders, provider => provider.Alias == "articles");

        Assert.Equal(["article"], articles.ResolvedDocumentTypeAliases);
        Assert.False(articles.IgnoreSegments);
        Assert.False(articles.CustomRequest);

        DashboardSearchProviderViewModel catalogue =
            Assert.Single(snapshot.SearchProviders, provider => provider.Alias == "catalogue");

        Assert.Equal(["cataloguePage"], catalogue.ResolvedDocumentTypeAliases);
        Assert.False(catalogue.RequirePublishedPage);
        Assert.False(catalogue.UsesFallbackLocation);
        Assert.Equal("Store catalogue", catalogue.DisplayedAtLocation);
    }

    [Fact]
    public void SnapshotDistinguishesAutomaticDefaultLegacyAndExplicitDataMapping()
    {
#pragma warning disable CS0618 // Verify the retained legacy mapper is described accurately.
        var legacyMapper = new Mock<IContentTypeMapping>(MockBehavior.Strict);
#pragma warning restore CS0618
        using ServiceProvider services = Services(options =>
        {
            options.MapContent("automatic", mapping => mapping.AutoMap());
            options.MapContent(
                "explicit",
                mapping => mapping.MapData("blocks", data => data.UseConverter<DashboardDataConverter>()));
            options.MapBrand("brand");
#pragma warning disable CS0618 // Verify the retained legacy mapper is described accurately.
            options.AddContentType(
                "legacy",
                mapping =>
                {
                    mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace();
                    mapping.UseMapper(legacyMapper.Object);
                });
#pragma warning restore CS0618
        });
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                true);
        DashboardMappingViewModel automatic =
            Assert.Single(snapshot.Mappings, mapping => mapping.DocumentTypeAlias == "automatic");

        Assert.True(automatic.AutoMap);
        Assert.False(automatic.CustomMapper);
        Assert.Null(automatic.CustomMapperTypeName);
        Assert.Empty(automatic.Fields);

        DashboardMappingViewModel explicitMapping =
            Assert.Single(snapshot.Mappings, mapping => mapping.DocumentTypeAlias == "explicit");

        Assert.Equal(nameof(DashboardDataConverter), Assert.Single(explicitMapping.Fields).Conversion);

        DashboardMappingViewModel brand = Assert.Single(snapshot.Mappings, mapping => mapping.EntityType == "Brand");

        Assert.Empty(brand.Fields);
        Assert.True(brand.SynchronizeLifecycle);
        Assert.False(brand.CustomMapper);

        DashboardMappingViewModel legacy =
            Assert.Single(snapshot.Mappings, mapping => mapping.DocumentTypeAlias == "legacy");

        Assert.True(legacy.LegacyMapper);
        Assert.True(legacy.CustomMapper);
        Assert.True(legacy.ClearOnDeltaUpdates);
        Assert.True(legacy.ClearOnFullRebuilds);
        Assert.Equal(legacyMapper.Object.GetType().Name, legacy.CustomMapperTypeName);
        legacyMapper.VerifyNoOtherCalls();
    }

    [Fact]
    public void ViewOnlyMappingsAndDisabledParentSettingsRemainVisible()
    {
        using ServiceProvider services = Services(options => options.MapProduct(
            "product",
            mapping =>
            {
                mapping.Export = false;
                mapping.TrackViews = false;
                mapping.VariantsFromChildren("variant");
            }));
        ConfigurationViewModel snapshot =
            Snapshot(
                services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                [],
                true);

        Assert.All(
            snapshot.Mappings,
            mapping =>
            {
                Assert.False(mapping.Export);
                Assert.False(mapping.TrackViews);
                Assert.Equal("Node ID", mapping.IdSource);
            });
    }

    [Fact]
    public void ControllerCanBeActivatedWithoutIntegrationRegistration()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var error =
            Assert.IsType<ConfigurationErrorViewModel>(Assert.IsType<OkObjectResult>(controller.Configuration()).Value);

        Assert.False(error.IntegrationRegistered);
        Assert.True(error.FactoryFailed);
    }

    [Fact]
    public void MissingClientSettingsProduceActionableConfigurationError()
    {
        using ServiceProvider services = Services(options => options.MapContent("article"));
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var error =
            Assert.IsType<ConfigurationErrorViewModel>(Assert.IsType<OkObjectResult>(controller.Configuration()).Value);

        Assert.True(error.IntegrationRegistered);
        Assert.Contains("client settings", error.ErrorMessage);
    }

    [Fact]
    public void ConfigurationFailuresDoNotExposeExceptionDetails()
    {
        var collection = new ServiceCollection();

        collection.AddSingleton<RelewiseUmbracoConfiguration>(_ =>
            throw new InvalidOperationException("Private configuration details"));

        using ServiceProvider services = collection.BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var error =
            Assert.IsType<ConfigurationErrorViewModel>(Assert.IsType<OkObjectResult>(controller.Configuration()).Value);

        Assert.DoesNotContain("Private configuration details", error.ErrorMessage);
        Assert.Contains("logs", error.ErrorMessage);
    }

    [Fact]
    public async Task ExportWithoutRegistrationReturnsAConfigurationConflict()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<DashboardApiController>(services);
        var response = Assert.IsType<ObjectResult>(await controller.ContentExport(false, default));

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void CoreSnapshotDoesNotNeedASearchContributor()
    {
        using ServiceProvider services = Services(options => options.MapContent("article"));
        ConfigurationViewModel snapshot = DashboardConfigurationSnapshot.Create(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            middlewareEnabled: false);
        Assert.False(snapshot.SearchConfigured);
        Assert.Empty(snapshot.SearchProviders);
        Assert.Single(snapshot.Mappings);
    }

    [Fact]
    public void SnapshotAppliesAllContributorsInRegistrationOrder()
    {
        using ServiceProvider services = Services(options => options.MapContent("article"));
        var calls = new List<string>();
        var first = new Mock<IDashboardConfigurationContributor>();
        first.Setup(value => value.Apply(It.IsAny<RelewiseUmbracoConfiguration>(), It.IsAny<ConfigurationViewModel>()))
            .Callback<RelewiseUmbracoConfiguration, ConfigurationViewModel>((_, snapshot) =>
            {
                calls.Add("first");
                snapshot.SearchConfigured = true;
            });
        var second = new Mock<IDashboardConfigurationContributor>();
        second.Setup(value => value.Apply(It.IsAny<RelewiseUmbracoConfiguration>(), It.IsAny<ConfigurationViewModel>()))
            .Callback<RelewiseUmbracoConfiguration, ConfigurationViewModel>((_, snapshot) =>
            {
                Assert.True(snapshot.SearchConfigured);
                snapshot.SearchConfigured = false;
                calls.Add("second");
            });
        using ServiceProvider contributions = new ServiceCollection()
            .AddSingleton(first.Object)
            .AddSingleton(second.Object)
            .BuildServiceProvider();

        ConfigurationViewModel result = DashboardConfigurationSnapshot.Create(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            middlewareEnabled: false,
            contributions.GetServices<IDashboardConfigurationContributor>());

        Assert.Equal(["first", "second"], calls);
        Assert.False(result.SearchConfigured);
    }

    private static ConfigurationViewModel Snapshot(
        RelewiseUmbracoConfiguration configuration,
        List<NamedOptionsViewObject> clients,
        bool middlewareEnabled)
    {
        return DashboardConfigurationSnapshot.Create(
            configuration,
            clients,
            middlewareEnabled,
            [new SearchDashboardContributor()]);
    }

    private static ServiceProvider ConnectionServices(Action<RelewiseOptionsBuilder> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.MapContent("article"));
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddRelewise(configure);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider Services(Action<RelewiseUmbracoOptionsBuilder> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => configure(options));
        services.AddSingleton<RelewiseUmbracoConfiguration>();

        return services.BuildServiceProvider();
    }
}
