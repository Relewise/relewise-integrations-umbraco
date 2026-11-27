using Relewise.Integrations.Umbraco.DemoStorefront.Setup;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;

internal static class RecommendationPlacement
{
    public static RecommendationModel? Configuration(
        IPublishedContent page,
        HttpContext http,
        bool emptyCart = false)
    {
        if (!DemoConnection.IsConfigured(http))
        {
            return null;
        }

        string[] keys =
        [
            "SimilarProducts", "RecentlyViewedProducts", "PopularProducts", "PopularCategories",
            "LoadingRecommendations", "Equipment", "FieldNotes", "OnOffer", "ReadStory"
        ];

        return new RecommendationModel
        {
            Endpoint = DemoMarket.Current(http).LanguagePath + "/recommendations/" + page.Key +
                       (emptyCart ? "?placement=cart" : ""),
            Labels = keys.ToDictionary(key => key, key => Storefront.Text(http, key))
        };
    }
}
