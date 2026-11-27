using Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Home;

/// <summary>
/// Published cards, categories and recommendation placement for the homepage.
/// </summary>
public sealed record HomePageModel(
    IPublishedContent Page,
    ListingCard[] Products,
    ListingCard[] Articles,
    StorefrontLink[] Categories,
    RecommendationModel? Recommendations);
