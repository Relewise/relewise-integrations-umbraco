using Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Editorial;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Home;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

internal static class StorefrontPages
{
    public static async Task<StorefrontPage?> CreateAsync(IPublishedContent page, HttpContext http)
    {
        string section = page.ContentType.Alias is DocumentTypeAliases.Article or DocumentTypeAliases.LocalizedArticle
            or DocumentTypeAliases.Guide ? "journal" :
            page.ContentType.Alias == DocumentTypeAliases.Home ? "home" : "shop";

        if (page.ContentType.Alias is DocumentTypeAliases.ProductCategory or DocumentTypeAliases.Folder
            or DocumentTypeAliases.Brand)
        {
            return (await StorefrontListing.GetAsync(http, page)).Page;
        }

        return page.ContentType.Alias switch
        {
            DocumentTypeAliases.Home => new StorefrontPage(
                SiteContent.Title(page),
                section,
                "/Views/Home/Index.cshtml",
                HomePage.Create(page, http)),
            DocumentTypeAliases.Product or DocumentTypeAliases.ProductVariant => new StorefrontPage(
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
