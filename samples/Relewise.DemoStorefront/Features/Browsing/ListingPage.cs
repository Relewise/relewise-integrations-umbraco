using System.Text;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.Site.Storefront;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class ListingPage
{
    internal static string Render(
        HttpContext http,
        IPublishedContent? collection,
        ListingModel model,
        string heading)
    {
        var section = model.SearchPage ? "search" :
            model.View == "offers" ? "offers" :
            model.Editorial ? "journal" : "shop";
        var body = new StringBuilder(StorefrontNavigation.Breadcrumb(
            collection,
            http,
            section,
            model.SearchPage || model.View == "offers" ? heading : null
        ));

        body.Append($"<section class='listing-heading'><h1>{E(heading)}</h1></section>");

        if (collection != null)
        {
            body.Append(StorefrontNavigation.Links(
                collection.Children().Where(node => node.ContentType.Alias is "productCategory" or "folder"),
                http));
        }

        var json = System.Text.Json.JsonSerializer.Serialize(model, StorefrontChrome.Json);

        // Default JSON escaping protects the script boundary even when CMS values contain markup.
        body.Append(
            $"<div id='storefront-listing'><p role='status'>{E(Text(http, "UpdatingResults"))}</p></div><script id='listing-data' type='application/json'>{json}</script>");
        http.Items["Fieldwork:HasSearch"] = true;
        http.Items["Fieldwork:SearchProvider"] = "Relewise";

        return Layout(
            heading,
            body.ToString(),
            true,
            http,
            section
        );
    }

    internal static string Preview(HttpContext http,
        IPublishedContent? collection)
    {
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(DemoMarket.Current(http).Culture);

        using UmbracoContextReference context =
            http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        IPublishedContent home = SiteContent.Home(http.RequestServices);
        var editorial = http.Request.Query["type"] == "article";
        var heading = collection == null
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

        var body = StorefrontNavigation.Breadcrumb(collection, http, editorial ? "journal" : "shop") +
                   $"<h1>{E(heading)}</h1><p>{E(Text(http, "ContentPreview"))}</p><div class='grid'>" +
                   string.Concat(pages.OrderBy(SiteContent.Title, StringComparer.CurrentCulture)
                       .Take(6)
                       .Select(page => ListingCards.Card(page, http))) + "</div>";

        return Layout(heading, body, false, http, editorial ? "journal" : "shop");
    }
}
