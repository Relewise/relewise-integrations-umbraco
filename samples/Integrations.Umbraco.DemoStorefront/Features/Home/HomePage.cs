using Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Setup;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;
using static Relewise.Integrations.Umbraco.DemoStorefront.Site.SiteContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.Site.StorefrontNavigation;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Home;

internal static class HomePage
{
    public static HomePageModel Create(IPublishedContent home, HttpContext http)
    {
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        IPublishedContent[] nodes = [.. home.Descendants().Where(node => !access.IsProtected(node.Path).Success)];

        bool connected = DemoConnection.IsConfigured(http);

        return new HomePageModel(
            home,
            connected
                ? []
                :
                [
                    .. nodes.Where(node => node.ContentType.Alias == DocumentTypeAliases.Product)
                        .OrderBy(Title, StringComparer.CurrentCulture)
                        .Take(3)
                        .Select(page => ListingCards.CardData(page, http))
                ],
            [
                .. nodes.Where(
                    node => node.ContentType.Alias is DocumentTypeAliases.Article
                        or DocumentTypeAliases.LocalizedArticle)
                    .OrderByDescending(node => node.CreateDate)
                    .ThenBy(Title, StringComparer.CurrentCulture)
                    .Take(3)
                    .Select(page => ListingCards.CardData(page, http))
            ],
            connected ? [] : Links(ShoppingCategories(home)),
            RecommendationPlacement.Configuration(home, http));
    }
}
