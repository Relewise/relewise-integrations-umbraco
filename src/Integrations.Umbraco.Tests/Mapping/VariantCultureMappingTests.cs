using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class VariantCultureMappingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapsPublishedChildrenAcrossCulturesAndRetainsOnlyParentCultures(bool englishOnly)
    {
        IPublishedContent product = Node("product", "en-US", "da-DK");
        IPublishedContent english = Node("variant", "en-US");
        IPublishedContent danish = Node("variant", "da-DK");
        IPublishedContent invariant = Node("variant");
        IPublishedContent unrelated = Node("article", "en-US");
        Mock.Get(english).SetupGet(value => value.SortOrder).Returns(2);
        Mock.Get(danish).SetupGet(value => value.SortOrder).Returns(1);
        Mock.Get(invariant).SetupGet(value => value.SortOrder).Returns(3);
        var draftKey = Guid.NewGuid();
        var unpublishedAncestorKey = Guid.NewGuid();
        IEnumerable<Guid> keys =
            [english.Key, danish.Key, invariant.Key, unrelated.Key, draftKey, unpublishedAncestorKey];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetChildrenKeys(product.Key, out keys)).Returns(true);
        var cache = new Mock<IPublishedContentCache>();

        foreach (IPublishedContent child in new[] { english, danish, invariant, unrelated })
        {
            cache.Setup(value => value.GetById(preview: false, child.Key)).Returns(child);
        }

        var context = new Mock<IUmbracoContext>();
        context.SetupGet(value => value.Content).Returns(cache.Object);
        var contexts = new Mock<IUmbracoContextFactory>();
        contexts.Setup(value => value.EnsureUmbracoContext()).Returns(() =>
            new UmbracoContextReference(context.Object, isRoot: false, Mock.Of<IUmbracoContextAccessor>()));
        var services = new ServiceCollection();
        services.AddSingleton(navigation.Object);
        services.AddSingleton(Mock.Of<IPublishedContentStatusFilteringService>());
#if UMBRACO_SEARCH_ADDON
        var status = new Mock<IPublishStatusQueryService>();
        status.Setup(value => value.IsDocumentPublishedInAnyCulture(It.Is<Guid>(key => key != draftKey)))
            .Returns(true);
#else
        var status = new Mock<IDocumentPublishStatusQueryService>();
        status.Setup(value => value.IsPublishedInAnyCulture(It.Is<Guid>(key => key != draftKey)))
            .Returns(true);
#endif
        status.Setup(value => value.HasPublishedAncestorPath(It.Is<Guid>(key => key != unpublishedAncestorKey)))
            .Returns(true);
        services.AddSingleton(status.Object);
        services.AddSingleton(contexts.Object);
        services.AddSingleton(Mock.Of<IDefaultCultureAccessor>(value => value.DefaultCulture == "en-US"));
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.UseContentKeysAsIds = true;
            options.MapProduct("product", mapping =>
            {
                mapping.AdministrativeActions = AdministrativeActionPolicy.None();
                mapping.VariantsFromChildren("variant");
            });
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var configuration = provider.GetRequiredService<RelewiseUmbracoConfiguration>();
        await using var operation = new MappingOperation(provider, CancellationToken.None);
        string[] cultures = englishOnly ? ["en-US"] : ["en-US", "da-DK"];
        MappedEntity mapped = await new EntityMapper(configuration).MapAsync(
            product,
            configuration.Mappings["product"],
            operation,
            version: 123,
            publishedCultures: cultures);

        Guid[] expected = englishOnly ? [english.Key, invariant.Key] : [danish.Key, english.Key, invariant.Key];
        cache.Verify(value => value.GetById(preview: false, draftKey), Times.Never);
        cache.Verify(value => value.GetById(preview: false, unpublishedAncestorKey), Times.Never);
        Assert.Equal(expected, mapped.Variants.Select(variant => variant.NodeKey));
        Assert.All(mapped.Variants, variant => Assert.Equal(variant.NodeKey.ToString("D"), variant.Id));
    }

    private static IPublishedContent Node(string alias, params string[] cultures)
    {
        var type = new Mock<IPublishedContentType>();
        type.SetupGet(value => value.Alias).Returns(alias);
        type.SetupGet(value => value.Variations)
            .Returns(cultures.Length == 0 ? ContentVariation.Nothing : ContentVariation.Culture);
        var node = new Mock<IPublishedContent>();
        node.SetupGet(value => value.ItemType).Returns(PublishedItemType.Content);
        node.SetupGet(value => value.Key).Returns(Guid.NewGuid());
        node.SetupGet(value => value.Id).Returns(42);
        node.SetupGet(value => value.Path).Returns("-1,42");
        node.SetupGet(value => value.ContentType).Returns(type.Object);
        node.SetupGet(value => value.Cultures).Returns(cultures.ToDictionary(
            culture => culture,
            culture => new PublishedCultureInfo(culture, culture, culture, DateTime.UnixEpoch)));
        return node.Object;
    }
}
