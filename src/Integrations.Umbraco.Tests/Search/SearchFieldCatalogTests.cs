using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class SearchFieldCatalogTests
{
    [Fact]
    public void AutomaticBindingsReuseExportDestinationsAndCacheOnlyDescriptions()
    {
        var property = new Mock<IPropertyType>();
        property.SetupGet(value => value.Alias).Returns("stock");
        property.SetupGet(value => value.PropertyEditorAlias).Returns("Umbraco.Integer");
        var schema = new Mock<IContentType>();
        schema.SetupGet(value => value.CompositionPropertyTypes).Returns([property.Object]);
        var types = new Mock<IContentTypeService>();
        types.Setup(value => value.Get("product")).Returns(schema.Object);
        var registrations = new ServiceCollection();
        registrations.AddSingleton(types.Object);
        registrations.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapProduct(
                "product",
                mapping =>
                {
                    mapping.IdFromProperty("sku");
                    mapping.BrandFrom("brand");
                    mapping.Map(product => product.DisplayName, "heading");
                    mapping.MapData("stock", field => field.DataKey = "inventory");
                });
            options.ConfigureSearch(search =>
            {
                search.AddProductProvider("native");
            });
        });
        registrations.AddSingleton<RelewiseUmbracoConfiguration>();
        using ServiceProvider services = registrations.BuildServiceProvider();
        RelewiseUmbracoConfiguration configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        SearchProviderDefinition provider = configuration.GetSearchOptions().Providers["native"];

        SearchFieldDefinition Resolve(string name) =>
            SearchFieldCatalog.Resolve(name, configuration, provider, services);

        SearchFieldDefinition stock = Resolve("stock");
        Assert.Equal("inventory", stock.DataKey);
        Assert.Equal(typeof(int), stock.ValueType);
        Assert.False(stock.Localized);
        Assert.Same(stock, Resolve("stock"));
        Assert.Equal(SearchFieldKind.Id, Resolve("sku").Kind);
        Assert.Equal(SearchFieldKind.Brand, Resolve("brand").Kind);
        Assert.Equal(SearchFieldKind.DisplayName, Resolve("heading").Kind);
        types.Verify(value => value.Get("product"), Times.Once);
    }

    [Theory]
    [InlineData("Umbraco.Integer")]
    [InlineData("Umbraco.TrueFalse")]
    public void CultureVaryingNonTextValuesRequireAnExplicitStableKey(string editor)
    {
        var property = new Mock<IPropertyType>();
        property.SetupGet(value => value.Alias).Returns("score");
        property.SetupGet(value => value.PropertyEditorAlias).Returns(editor);
        property.SetupGet(value => value.Variations).Returns(ContentVariation.Culture);
        var schema = new Mock<IContentType>();
        schema.SetupGet(value => value.CompositionPropertyTypes).Returns([property.Object]);
        var types = new Mock<IContentTypeService>();
        types.Setup(value => value.Get("article")).Returns(schema.Object);
        var registrations = new ServiceCollection();
        registrations.AddSingleton(types.Object);
        registrations.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapContent("article", mapping => mapping.MapData("score"));
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider("automatic");
                search.AddContentProvider(
                    "explicit",
                    fields => fields.MapData<int>(
                        "score",
                        field =>
                            field.DataKey = "stableScore"));
            });
        });
        registrations.AddSingleton<RelewiseUmbracoConfiguration>();
        using ServiceProvider services = registrations.BuildServiceProvider();
        RelewiseUmbracoConfiguration configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        Assert.Throws<NotSupportedException>(() => SearchFieldCatalog.Resolve(
            "score",
            configuration,
            configuration.GetSearchOptions().Providers["automatic"],
            services));
        Assert.Equal(
            "stableScore",
            SearchFieldCatalog.Resolve(
                    "score",
                    configuration,
                    configuration.GetSearchOptions().Providers["explicit"],
                    services)
                .DataKey);
    }

    [Fact]
    public void ConflictingContentDestinationsRequireAnExplicitBinding()
    {
        var property = new Mock<IPropertyType>();
        property.SetupGet(value => value.Alias).Returns("stock");
        property.SetupGet(value => value.PropertyEditorAlias).Returns("Umbraco.Integer");
        var schema = new Mock<IContentType>();
        schema.SetupGet(value => value.CompositionPropertyTypes).Returns([property.Object]);
        var types = new Mock<IContentTypeService>();
        types.Setup(value => value.Get(It.IsAny<string>())).Returns(schema.Object);
        var registrations = new ServiceCollection();
        registrations.AddSingleton(types.Object);
        registrations.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapContent("article", mapping => mapping.MapData("stock", field => field.DataKey = "a"));
            options.MapContent("guide", mapping => mapping.MapData("stock", field => field.DataKey = "b"));
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider("mixed");
            });
        });
        registrations.AddSingleton<RelewiseUmbracoConfiguration>();
        using ServiceProvider services = registrations.BuildServiceProvider();
        RelewiseUmbracoConfiguration configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        Assert.Throws<InvalidOperationException>(() => SearchFieldCatalog.Resolve(
            "stock",
            configuration,
            configuration.GetSearchOptions().Providers["mixed"],
            services));
    }
}
