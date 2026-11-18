using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests;

public class EntityMappingTests
{
    [Fact]
    public void ContentAllowsDistinctAliasesButOtherEntitiesHaveOneSource()
    {
        var options = new RelewiseUmbracoOptionsBuilder();
        options.MapContent("article"); options.MapContent("guide");
        Assert.Throws<InvalidOperationException>(() => options.MapContent("ARTICLE"));
        options.MapProduct("product");
        Assert.Throws<InvalidOperationException>(() => options.MapProduct("otherProduct"));
        options.MapBrand("brand");
        Assert.Throws<InvalidOperationException>(() => options.MapBrand("otherBrand"));
        options.MapProductCategory("category");
        Assert.Throws<InvalidOperationException>(() => options.MapProductCategory("otherCategory"));
        options.MapContentCategory("topic");
        Assert.Throws<InvalidOperationException>(() => options.MapContentCategory("otherTopic"));
        Assert.Throws<InvalidOperationException>(() => options.MapContent("product"));
    }
    [Fact]
    public void LegacyRegistrationRetainsMergingAndViewOnlyBehavior()
    {
        using var services = Services(options => { options.AddContentType("article"); options.AddContentType("article", type => type.AutoMap()); options.AddContentType("viewOnly"); });
        var configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        Assert.True(configuration.CanMap("article")); Assert.False(configuration.CanMap("viewOnly"));
        Assert.Throws<ArgumentException>(() => new RelewiseUmbracoOptionsBuilder().AddContentType("article").AddContentType("article", throwIfExists: true));
    }
    [Fact]
    public void DataAliasIsRequiredButDestinationConfigurationIsOptional()
    {
        using var services = Services(options => options.MapContent("article", mapping => mapping.MapData("hero")));
        var definition = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["article"];
        Assert.Equal("hero", Assert.Single(definition.DataMappings).Options.DataKey);
        Assert.Throws<InvalidOperationException>(() => new RelewiseUmbracoOptionsBuilder().MapContent("article", mapping => mapping.MapData("foo", data => data.DataKey = "contentTypeAlias")));
    }
    [Fact]
    public void CanonicalIdOverrideIsSharedAndEmptyValuesFail()
    {
        using var services = Services(options => options.MapBrand("brand", mapping => mapping.IdFromProperty("brandId")));
        var config = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(p => p.PropertyType).Returns(Mock.Of<IPublishedPropertyType>());
        property.Setup(p => p.GetValue(null, null)).Returns("erp-brand");
        var node = new Mock<IPublishedContent>();
        node.SetupGet(p => p.ContentType.Alias).Returns("brand");
        node.Setup(p => p.GetProperty("brandId")).Returns(property.Object);
        Assert.Equal("erp-brand", config.GetContentId(node.Object));
        property.Setup(p => p.GetValue(null, null)).Returns(" ");
        Assert.Throws<InvalidOperationException>(() => config.GetContentId(node.Object));
    }
    [Fact]
    public void BuiltInMappingRejectsIdentityNestedAndDuplicateDestinations()
    {
        var mapping = new EntityMappingBuilder<Product>();
        Assert.Throws<ArgumentException>(() => mapping.Map(entity => entity.Id, "sku"));
        Assert.Throws<ArgumentException>(() => mapping.Map(entity => entity.Brand!.Id, "brand"));
        mapping.Map(entity => entity.DisplayName, "heading");
        Assert.Throws<InvalidOperationException>(() => mapping.Map(entity => entity.DisplayName, "title"));
    }
    [Fact]
    public async Task ActivatedConvertersAndScopedDependenciesAreDisposedByTheirOwners()
    {
        var services = new ServiceCollection(); services.AddScoped<ScopedDependency>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        ScopedDependency dependency;
        AsyncConverter converter;
        await using (var scope = provider.CreateAsyncScope())
        {
            dependency = scope.ServiceProvider.GetRequiredService<ScopedDependency>();
            await using (var operation = new MappingOperation(scope.ServiceProvider, default))
            {
                converter = operation.Resolve<AsyncConverter>();
                Assert.Same(converter, operation.Resolve<AsyncConverter>());
                Assert.Same(dependency, converter.Dependency);
            }
            Assert.True(converter.Disposed); Assert.False(dependency.Disposed);
        }
        Assert.True(dependency.Disposed);
    }
    [Fact]
    public void SupplementPreservesUnspecifiedFieldsAndCannotClearAndReplace()
    {
        var product = Assert.IsType<Product>(EntityMapper.Create(typeof(Product), "sku"));
        Assert.Null(product.Assortments); Assert.Null(product.CategoryPaths); Assert.Null(product.ListPrice);
        var mapped = new MappedEntity(Guid.NewGuid(), "product", "sku", product, false);
        var update = Assert.IsType<ProductUpdate>(mapped.Update());
        Assert.False(update.ReplaceExistingVariants);
        Assert.Throws<InvalidOperationException>(() => mapped.Update(ContentUpdate.UpdateKind.ClearAndReplace));
    }
    [Fact]
    public void ProvidersValidateEntityTypeAndContentAliasSelection()
    {
        Assert.Throws<InvalidOperationException>(() => { using var _ = Services(options => { options.MapContent("article"); options.SearchProviders.AddProduct("products"); }); });
        Assert.Throws<InvalidOperationException>(() => { using var _ = Services(options => { options.MapContent("article"); options.SearchProviders.AddContent("articles", provider => provider.DocumentTypeAliases = ["missing"]); }); });
        using var services = Services(options => { options.MapContent("article"); options.SearchProviders.AddContent("articles"); });
        Assert.Null(services.GetRequiredService<RelewiseUmbracoConfiguration>().SearchProviders.DefaultPublishedContentProviderAlias);
    }
    [Fact]
    public void ParentExportAndTrackingSwitchesApplyToChildVariants()
    {
        using var services = Services(options => options.MapProduct("product", mapping =>
        {
            mapping.Export = false;
            mapping.TrackViews = false;
            mapping.VariantsFromChildren("variant");
        }));
        var variant = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["variant"];
        Assert.False(variant.Export);
        Assert.False(variant.TrackViews);
    }
    private static ServiceProvider Services(Action<RelewiseUmbracoOptionsBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => configure(options));
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _ = provider.GetRequiredService<RelewiseUmbracoConfiguration>();
        return provider;
    }
    public sealed class ScopedDependency : IDisposable { public bool Disposed { get; private set; } public void Dispose() => Disposed = true; }
    public sealed class AsyncConverter(ScopedDependency dependency) : IAsyncDisposable
    {
        public ScopedDependency Dependency { get; } = dependency;
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
