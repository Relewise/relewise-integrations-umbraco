using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Features.Editorial;
using Relewise.DemoStorefront.Features.Home;
using Relewise.DemoStorefront.Features.Products;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Site;

internal static class StorefrontPages
{
    internal static async Task<string> RenderAsync(IPublishedContent page, HttpContext http)
    {
        if (page.ContentType.Alias is "productCategory" or "folder" or "brand")
        {
            return await StorefrontListing.RenderAsync(http, page);
        }

        return Render(page, http);
    }

    private static string Render(IPublishedContent page, HttpContext http)
    {
        string html = page.ContentType.Alias switch
        {
            "home" => HomePage.Render(page, http),
            "product" or "productVariant" => ProductPage.Render(page, http),
            _ => EditorialPage.Render(page, http)
        };
        string section = page.ContentType.Alias is "article" or "localizedArticle" or "guide" ? "journal" :
            page.ContentType.Alias == "home" ? "home" : "shop";
        bool connected = http.RequestServices.GetService<Relewise.Integrations.Umbraco.RelewiseUmbracoConfiguration>() != null;
        return Storefront.Layout(SiteContent.Title(page), html, connected, http, section);
    }
}
