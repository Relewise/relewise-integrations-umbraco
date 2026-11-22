using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Site;

internal static class SiteContent
{
    internal static IPublishedContent Home(IServiceProvider services)
    {
        var navigation = services.GetRequiredService<IDocumentNavigationQueryService>();
        IPublishedContentCache content = services.GetRequiredService<IUmbracoContextAccessor>()
            .GetRequiredUmbracoContext()
            .Content;

        // Rendering needs published content only, not a lookup through the editing/content service.
        if (!navigation.TryGetRootKeysOfType(DocumentTypeAliases.Home, out IEnumerable<Guid> keys))
        {
            throw new InvalidOperationException("The published demo home page could not be found.");
        }

        return keys.Select(key => content.GetById(key)).OfType<IPublishedContent>().Single();
    }

    internal static IEnumerable<IPublishedContent> ShoppingCategories(IPublishedContent home)
    {
        IPublishedContent? equipment = home.Descendants()
            .FirstOrDefault(node =>
                node.ContentType.Alias == DocumentTypeAliases.ProductCategory &&
                node.Value<string>(PropertyAliases.CategoryId) == "U19-SMOKE-EQUIPMENT");

        return equipment?.Children().Where(node => node.ContentType.Alias == DocumentTypeAliases.ProductCategory) ?? [];
    }

    internal static IEnumerable<IPublishedContent> ShoppingBrands(IPublishedContent home)
    {
        return home.Descendants()
            .Where(node => node.ContentType.Alias == DocumentTypeAliases.Brand)
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase);
    }

    internal static string Title(IPublishedContent page)
    {
        return page.Value<string>(PropertyAliases.Heading) ?? page.Name;
    }
}
