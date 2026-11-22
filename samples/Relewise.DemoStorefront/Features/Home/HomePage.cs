using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using static Relewise.DemoStorefront.Site.SiteContent;
using static Relewise.DemoStorefront.Site.StorefrontNavigation;

namespace Relewise.DemoStorefront.Features.Home;

internal static class HomePage
{
    internal static string Render(IPublishedContent home,
        HttpContext http)
    {
        DemoMarket market = DemoMarket.Current(http);
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        IPublishedContent[] nodes = [.. home.Descendants().Where(node => !access.IsProtected(node.Path).Success)];

        return
            $"<section class='welcome'><div><p class='eyebrow'>Fieldwork {T(http, "OutdoorGoods")}</p><h1>{Storefront.E(Title(home))}</h1><p class='lead'>{Storefront.E(home.Value<string>("bodyText"))}</p><div class='actions'><a class='button' href='{market.StoreUrl}'>{T(http, "ExploreCollection")}</a><a class='button secondary' href='{market.StoreUrl}?type=article'>{T(http, "ExploreJournal")}</a></div></div><div class='illustration'>{Storefront.Icon()}</div></section>" +
            (DemoConnection.IsConfigured(http)
                ? RecommendationPlacement.Placeholder(home, http)
                : $"<section class='page-section'><h2>{T(http, "ShopByCategory")}</h2>{CategoryLinks(home, http)}</section>" +
                  $"<section class='page-section'><div class='section-heading'><h2>{T(http, "FromCollection")}</h2><a href='{market.StoreUrl}'>{T(http, "ViewAllProducts")}</a></div><div class='grid'>" +
                  string.Concat(nodes.Where(node => node.ContentType.Alias == "product")
                      .OrderBy(Title, StringComparer.CurrentCulture)
                      .Take(3)
                      .Select(node => ListingCards.Card(node, http))) + "</div></section>") +
            $"<section class='page-section'><div class='section-heading'><h2>{T(http, "Journal")}</h2><a href='{market.StoreUrl}?type=article'>{T(http, "ViewAllStories")}</a></div><div class='grid'>" +
            string.Concat(nodes.Where(node => node.ContentType.Alias is "article" or "localizedArticle")
                .OrderByDescending(node => node.CreateDate)
                .ThenBy(Title, StringComparer.CurrentCulture)
                .Take(3)
                .Select(node => ListingCards.Card(node, http))) + "</div></section>";
    }

    private static string T(HttpContext http, string key)
    {
        return Storefront.E(Storefront.Text(http, key));
    }
}
