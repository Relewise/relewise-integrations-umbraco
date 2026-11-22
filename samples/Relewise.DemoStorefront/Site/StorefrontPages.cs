using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Features.Editorial;
using Relewise.DemoStorefront.Features.Home;
using Relewise.DemoStorefront.Features.Products;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Site;

internal static class StorefrontPages
{
    internal static async Task<StorefrontPage?> CreateAsync(IPublishedContent page, HttpContext http)
    {
        string section = page.ContentType.Alias is "article" or "localizedArticle" or "guide" ? "journal" :
            page.ContentType.Alias == "home" ? "home" : "shop";

        if (page.ContentType.Alias is "productCategory" or "folder" or "brand")
        {
            return (await StorefrontListing.GetAsync(http, page)).Page;
        }

        return page.ContentType.Alias switch
        {
            "home" => new StorefrontPage(
                SiteContent.Title(page),
                section,
                "/Views/Home/Index.cshtml",
                HomePage.Create(page, http)),
            "product" or "productVariant" => new StorefrontPage(
                SiteContent.Title(page),
                section,
                "/Views/Products/Detail.cshtml",
                ProductPage.Create(page, http)),
            _ => new StorefrontPage(
                SiteContent.Title(page),
                section,
                "/Views/Editorial/Detail.cshtml",
                EditorialPage.Create(page, http))
        };
    }
}
