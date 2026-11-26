using Relewise.DemoStorefront.Site;

namespace Relewise.DemoStorefront.Features.Browsing;

/// <summary>
/// Presentation data for a listing or content-preview page.
/// </summary>
public sealed record ListingPageModel(
    string Heading,
    BreadcrumbItem[] Breadcrumb,
    StorefrontLink[] Categories,
    ListingModel? Listing,
    ListingCard[] Preview);
