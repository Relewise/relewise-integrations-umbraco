using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.Site.Storefront;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class ListingPage
{
    internal static StorefrontPage Create(
        HttpContext http,
        IPublishedContent? collection,
        ListingModel model,
        string heading)
    {
        string section = model.SearchPage ? "search" :
            model.View == "offers" ? "offers" :
            model.Editorial ? "journal" : "shop";
        var page = new ListingPageModel(
            heading,
            StorefrontNavigation.Breadcrumb(
                collection,
                http,
                section,
                model.SearchPage || model.View == "offers" ? heading : null),
            collection == null
                ? []
                : StorefrontNavigation.Links(
                    collection.Children()
                        .Where(node => node.ContentType.Alias is "productCategory" or "folder")),
            model,
            []);

        http.Items["Fieldwork:HasSearch"] = true;
        http.Items["Fieldwork:SearchProvider"] = "Relewise";

        return new StorefrontPage(heading, section, "/Views/Browsing/Listing.cshtml", page);
    }

    internal static StorefrontPage Preview(
        HttpContext http,
        IPublishedContent? collection)
    {
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(DemoMarket.Current(http).Culture);

        using UmbracoContextReference context =
            http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        IPublishedContent home = SiteContent.Home(http.RequestServices);
        bool editorial = http.Request.Query["type"] == "article";
        string heading = collection == null
            ? Text(http, editorial ? "Journal" : "Collection")
            : SiteContent.Title(collection);
        IEnumerable<IPublishedContent> pages = (collection?.ContentType.Alias == "brand" ? home : collection ?? home)
            .Descendants()
            .Where(node =>
                editorial
                    ? node.ContentType.Alias is "article" or "guide" or "localizedArticle"
                    : node.ContentType.Alias == "product");

        if (collection?.ContentType.Alias == "brand")
        {
            pages = pages.Where(node => node.Value<IPublishedContent>("brand")?.Key == collection.Key);
        }

        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();

        pages = pages.Where(node => !access.IsProtected(node.Path).Success);

        string section = editorial ? "journal" : "shop";
        var page = new ListingPageModel(
            heading,
            StorefrontNavigation.Breadcrumb(collection, http, section),
            [],
            null,
            [
                .. pages.OrderBy(SiteContent.Title, StringComparer.CurrentCulture)
                    .Take(6)
                    .Select(node => ListingCards.CardData(node, http))
            ]);

        return new StorefrontPage(heading, section, "/Views/Browsing/Listing.cshtml", page);
    }
}
