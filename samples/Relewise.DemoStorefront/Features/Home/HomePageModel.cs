using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Home;

/// <summary>
/// Published cards, categories and recommendation placement for the homepage.
/// </summary>
public sealed record HomePageModel(
    IPublishedContent Page,
    ListingCard[] Products,
    ListingCard[] Articles,
    StorefrontLink[] Categories,
    RecommendationModel? Recommendations);
