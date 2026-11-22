using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Relewise.DemoStorefront.Site;

internal static class SiteContent
{
    internal static IPublishedContent Home(IServiceProvider services)
    {
        HttpContext? http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;

        if (http?.Items["Fieldwork:Home"] is IPublishedContent cached)
        {
            return cached;
        }

        Guid key = services.GetRequiredService<IContentService>()
            .GetRootContent()
            .Single(node => node.ContentType.Alias == "home")
            .Key;
        IPublishedContent home = services.GetRequiredService<IUmbracoContextAccessor>()
            .GetRequiredUmbracoContext()
            .Content.GetById(key)!;

        if (http != null)
        {
            http.Items["Fieldwork:Home"] = home;
        }

        return home;
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
