using System.Text;
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
    internal static string Render(
        IPublishedContent home,
        HttpContext http)
    {
        DemoMarket market = DemoMarket.Current(http);
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        IPublishedContent[] nodes = [.. home.Descendants().Where(node => !access.IsProtected(node.Path).Success)];

        var html = new StringBuilder();

        html.Append("<section class='welcome'><div>");
        html.Append($"<p class='eyebrow'>Fieldwork {T(http, "OutdoorGoods")}</p>");
        html.Append($"<h1>{Storefront.E(Title(home))}</h1>");
        html.Append($"<p class='lead'>{Storefront.E(home.Value<string>("bodyText"))}</p>");
        html.Append("<div class='actions'>");
        html.Append($"<a class='button' href='{market.StoreUrl}'>{T(http, "ExploreCollection")}</a>");
        html.Append($"<a class='button secondary' href='{market.StoreUrl}?type=article'>");
        html.Append($"{T(http, "ExploreJournal")}</a></div></div>");
        html.Append($"<div class='illustration'>{Storefront.Icon()}</div></section>");

        if (DemoConnection.IsConfigured(http))
        {
            html.Append(RecommendationPlacement.Placeholder(home, http));
        }
        else
        {
            html.Append($"<section class='page-section'><h2>{T(http, "ShopByCategory")}</h2>");
            html.Append($"{CategoryLinks(home, http)}</section>");
            html.Append("<section class='page-section'><div class='section-heading'>");
            html.Append($"<h2>{T(http, "FromCollection")}</h2>");
            html.Append($"<a href='{market.StoreUrl}'>{T(http, "ViewAllProducts")}</a></div><div class='grid'>");

            IEnumerable<IPublishedContent> products = nodes
                .Where(node => node.ContentType.Alias == "product")
                .OrderBy(Title, StringComparer.CurrentCulture)
                .Take(3);

            foreach (IPublishedContent product in products)
            {
                html.Append(ListingCards.Card(product, http));
            }

            html.Append("</div></section>");
        }

        html.Append("<section class='page-section'><div class='section-heading'>");
        html.Append($"<h2>{T(http, "Journal")}</h2>");
        html.Append(
            $"<a href='{market.StoreUrl}?type=article'>{T(http, "ViewAllStories")}</a></div><div class='grid'>");

        IEnumerable<IPublishedContent> articles = nodes
            .Where(node => node.ContentType.Alias is "article" or "localizedArticle")
            .OrderByDescending(node => node.CreateDate)
            .ThenBy(Title, StringComparer.CurrentCulture)
            .Take(3);

        foreach (IPublishedContent article in articles)
        {
            html.Append(ListingCards.Card(article, http));
        }

        html.Append("</div></section>");

        return html.ToString();
    }

    private static string T(HttpContext http, string key)
    {
        return Storefront.E(Storefront.Text(http, key));
    }
}
