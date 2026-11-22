using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Tests;

public class EntityMappingTests
{
    [Fact]
    public async Task InvariantNameIsSharedAcrossTheExportLanguages()
    {
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(value => value.PropertyType).Returns(Mock.Of<IPublishedPropertyType>());
        property.Setup(value => value.GetValue(null, null)).Returns("Trail flask");
        IPublishedContent source = Mock.Of<IPublishedContent>();
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var operation = new MappingOperation(services, default);
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US", "da-DK"],
            nameof(Product.DisplayName),
            operation);

        Multilingual result = await BuiltInPropertyConversion.ConvertAsync<Multilingual>(context);

        Assert.Collection(
            result.Values!,
            value =>
            {
                Assert.Equal("en-us", value.Language.Value);
                Assert.Equal("Trail flask", value.Text);
            },
            value =>
            {
                Assert.Equal("da-dk", value.Language.Value);
                Assert.Equal("Trail flask", value.Text);
            });
    }

    [Fact]
    public async Task LocalizedNameDoesNotExportAnUnpublishedTranslation()
    {
        var propertyType = new Mock<IPublishedPropertyType>();
        propertyType.SetupGet(value => value.Variations).Returns(global::Umbraco.Cms.Core.Models.ContentVariation.Culture);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(value => value.PropertyType).Returns(propertyType.Object);
        property.Setup(value => value.GetValue("en-US", null)).Returns("Across the seasons");
        property.Setup(value => value.GetValue("da-DK", null)).Returns("Gennem årstiderne");
        IPublishedContent source = Mock.Of<IPublishedContent>();
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var operation = new MappingOperation(services, default);
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US"],
            nameof(Relewise.Client.DataTypes.Content.DisplayName),
            operation);

        Multilingual result = await BuiltInPropertyConversion.ConvertAsync<Multilingual>(context);

        Multilingual.Value name = Assert.Single(result.Values!);
        Assert.Equal("en-us", name.Language.Value);
        Assert.Equal("Across the seasons", name.Text);
    }

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("DA-dk", "da-DK")]
    [InlineData("fr-FR", "da-DK")]
    public async Task LocalizedBrandNameUsesTheDefaultPublishedLanguageDeterministically(string defaultCulture, string expected)
    {
        var languages = new Mock<ILanguageService>();
        languages.Setup(value => value.GetDefaultIsoCodeAsync()).ReturnsAsync(defaultCulture);
        var services = new ServiceCollection();
        services.AddSingleton(languages.Object);
        await using ServiceProvider root = services.BuildServiceProvider();
        await using var operation = new MappingOperation(root, default);
        var type = new Mock<IPublishedPropertyType>();
        type.SetupGet(value => value.Variations).Returns(global::Umbraco.Cms.Core.Models.ContentVariation.Culture);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(value => value.PropertyType).Returns(type.Object);
        property.Setup(value => value.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => "Name: " + culture);
        IPublishedContent source = Mock.Of<IPublishedContent>();
        var context = new PropertyMappingContext(source, source, property.Object, ["en-US", "da-DK"], nameof(Brand.DisplayName), operation);

        Assert.Equal("Name: " + expected, await BuiltInPropertyConversion.ConvertAsync<string>(context));
        Assert.Equal("Name: " + expected, await BuiltInPropertyConversion.ConvertAsync<string>(context));
        languages.Verify(value => value.GetDefaultIsoCodeAsync(), Times.Once);
    }

    [Fact]
    public async Task CancellingDuringLanguageResolutionDoesNotReadTheSourceValue()
    {
        var language = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var languages = new Mock<ILanguageService>();
        languages.Setup(value => value.GetDefaultIsoCodeAsync()).Returns(language.Task);
        var services = new ServiceCollection();
        services.AddSingleton(languages.Object);
        await using ServiceProvider root = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        await using var operation = new MappingOperation(root, cancellation.Token);
        var type = new Mock<IPublishedPropertyType>();
        type.SetupGet(value => value.Variations).Returns(global::Umbraco.Cms.Core.Models.ContentVariation.Culture);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(value => value.PropertyType).Returns(type.Object);
        IPublishedContent source = Mock.Of<IPublishedContent>();
        var context = new PropertyMappingContext(source, source, property.Object, ["en-US"], nameof(Brand.DisplayName), operation);

        Task<string> conversion = BuiltInPropertyConversion.ConvertAsync<string>(context).AsTask();
        cancellation.Cancel();
        language.SetResult("en-US");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion);
        property.Verify(value => value.GetValue(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void FinalConfigurationRejectsConflictingOrReservedDataKeysAfterRegistration()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = Services(options =>
            {
                DataMappingOptions? retained = null;
                options.MapProduct("product", mapping => mapping.MapData("field", data => retained = data));
                retained!.DataKey = "rwUmbracoSite";
            });
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = Services(options =>
            {
                DataMappingOptions? retained = null;
                options.MapBrand("brand", mapping =>
                {
                    mapping.MapData("first");
                    mapping.MapData("second", data => retained = data);
                });
                retained!.DataKey = "first";
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AProviderMustHaveAnExplicitUnsupportedSegmentPolicy(bool product)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = Services(options =>
            {
                if (product)
                {
                    options.MapProduct("product");
                    options.SearchProviders.AddProduct("search", provider => provider.UnsupportedSegmentBehavior = null!);
                }
                else
                {
                    options.MapContent("article");
                    options.SearchProviders.AddContent("search", provider => provider.UnsupportedSegmentBehavior = null!);
                }
            });
        });
    }

    [Fact]
    public void ContentAllowsDistinctAliasesButOtherEntitiesHaveOneSource()
    {
        var options = new RelewiseUmbracoOptionsBuilder();
        options.MapContent("article");
        options.MapContent("guide");
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
        using ServiceProvider services = Services(options => { options.AddContentType("article"); options.AddContentType("article", type => type.AutoMap()); options.AddContentType("viewOnly"); });
        RelewiseUmbracoConfiguration configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        Assert.True(configuration.CanMap("article"));
        Assert.False(configuration.CanMap("viewOnly"));
        Assert.Throws<ArgumentException>(() => new RelewiseUmbracoOptionsBuilder().AddContentType("article").AddContentType("article", throwIfExists: true));
    }
    [Fact]
    public void DataAliasIsRequiredButDestinationConfigurationIsOptional()
    {
        using ServiceProvider services = Services(options => options.MapContent("article", mapping => mapping.MapData("hero")));
        MappingDefinition definition = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["article"];
        Assert.Equal("hero", Assert.Single(definition.DataMappings).Options.DataKey);
        Assert.Throws<InvalidOperationException>(() => new RelewiseUmbracoOptionsBuilder().MapContent("article", mapping => mapping.MapData("foo", data => data.DataKey = "contentTypeAlias")));
    }
    [Fact]
    public void CanonicalIdOverrideIsSharedAndEmptyValuesFail()
    {
        using ServiceProvider services = Services(options => options.MapBrand("brand", mapping => mapping.IdFromProperty("brandId")));
        RelewiseUmbracoConfiguration config = services.GetRequiredService<RelewiseUmbracoConfiguration>();
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
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        ScopedDependency dependency;
        AsyncConverter converter;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            dependency = scope.ServiceProvider.GetRequiredService<ScopedDependency>();
            await using (var operation = new MappingOperation(scope.ServiceProvider, default))
            {
                converter = operation.Resolve<AsyncConverter>();
                Assert.Same(converter, operation.Resolve<AsyncConverter>());
                Assert.Same(dependency, converter.Dependency);
            }
            Assert.True(converter.Disposed);
            Assert.False(dependency.Disposed);
        }
        Assert.True(dependency.Disposed);
    }
    [Fact]
    public void SupplementPreservesUnspecifiedFieldsAndCannotClearAndReplace()
    {
        Product product = Assert.IsType<Product>(EntityMapper.Create(typeof(Product), "sku"));
        Assert.Null(product.Assortments);
        Assert.Null(product.CategoryPaths);
        Assert.Null(product.ListPrice);
        var mapped = new MappedEntity(Guid.NewGuid(), "product", "sku", product, false);
        ProductUpdate update = Assert.IsType<ProductUpdate>(mapped.Update());
        Assert.False(update.ReplaceExistingVariants);
        Assert.Throws<InvalidOperationException>(() => mapped.Update(ContentUpdate.UpdateKind.ClearAndReplace));
    }
    [Fact]
    public void ProvidersValidateEntityTypeAndContentAliasSelection()
    {
        Assert.Throws<InvalidOperationException>(() => { using ServiceProvider _ = Services(options => { options.MapContent("article"); options.SearchProviders.AddProduct("products"); }); });
        Assert.Throws<InvalidOperationException>(() => { using ServiceProvider _ = Services(options => { options.MapContent("article"); options.SearchProviders.AddContent("articles", provider => provider.DocumentTypeAliases = ["missing"]); }); });
        using ServiceProvider services = Services(options => { options.MapContent("article"); options.SearchProviders.AddContent("articles"); });
        Assert.Null(services.GetRequiredService<RelewiseUmbracoConfiguration>().SearchProviders.DefaultPublishedContentProviderAlias);
    }
    [Fact]
    public void ParentExportAndTrackingSwitchesApplyToChildVariants()
    {
        using ServiceProvider services = Services(options => options.MapProduct("product", mapping =>
        {
            mapping.Export = false;
            mapping.TrackViews = false;
            mapping.VariantsFromChildren("variant");
        }));
        MappingDefinition variant = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["variant"];
        Assert.False(variant.Export);
        Assert.False(variant.TrackViews);
    }
    private static ServiceProvider Services(Action<RelewiseUmbracoOptionsBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => configure(options));
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _ = provider.GetRequiredService<RelewiseUmbracoConfiguration>();
        return provider;
    }
    public sealed class ScopedDependency : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    public sealed class AsyncConverter(ScopedDependency dependency) : IAsyncDisposable
    {
        public ScopedDependency Dependency { get; } = dependency;
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
