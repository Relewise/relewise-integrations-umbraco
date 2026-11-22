using System.Text;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.Site.SiteContent;
using static Relewise.DemoStorefront.Site.StorefrontNavigation;

namespace Relewise.DemoStorefront.Features.Editorial;

internal static class EditorialPage
{
    internal static string Render(IPublishedContent page, HttpContext http)
    {
        string culture = DemoMarket.Current(http).Culture;
        var html = new StringBuilder(Breadcrumb(page, http, "journal"));
        html.Append($"<article class='reading'><header class='article-header'><p class='eyebrow'>{T(http, "Journal")}</p><h1>{Storefront.E(Title(page))}</h1><p class='lead'>{Storefront.E(page.Value<string>("bodyText"))}</p></header>");
        foreach (string alias in new[] { "contentRows", "pageGrid" })
        {
            html.Append(StorefrontEditorial.Render(page.GetProperty(alias)?.GetValue(), culture));
        }

        html.Append($"<p class='reading-back'><a class='text-link' href='{DemoMarket.Current(http).StoreUrl}?type=article'>{T(http, "ReturnJournal")}</a></p></article>");
        html.Append(IntegrationDetails.Render(page, http));
        return html.ToString();
    }

    private static string T(HttpContext http, string key) => Storefront.E(Storefront.Text(http, key));
}
