using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

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
        if (!navigation.TryGetRootKeysOfType("home", out IEnumerable<Guid> keys))
        {
            throw new InvalidOperationException("The published demo home page could not be found.");
        }

        return keys.Select(key => content.GetById(key)).OfType<IPublishedContent>().Single();
    }

    internal static IEnumerable<IPublishedContent> ShoppingCategories(IPublishedContent home)
    {
        IPublishedContent? equipment = home.Descendants()
            .FirstOrDefault(node =>
                node.ContentType.Alias == "productCategory" &&
                node.Value<string>("categoryId") == "U19-SMOKE-EQUIPMENT");

        return equipment?.Children().Where(node => node.ContentType.Alias == "productCategory") ?? [];
    }

    internal static IEnumerable<IPublishedContent> ShoppingBrands(IPublishedContent home)
    {
        return home.Descendants()
            .Where(node => node.ContentType.Alias == "brand")
            .OrderBy(Title, StringComparer.CurrentCultureIgnoreCase);
    }

    internal static string Title(IPublishedContent page)
    {
        return page.Value<string>("heading") ?? page.Name;
    }
}
