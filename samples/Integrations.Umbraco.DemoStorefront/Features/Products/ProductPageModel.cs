using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;

/// <summary>
/// Presentation data for a product or variant detail page.
/// </summary>
public sealed class ProductPageModel
{
    public required IPublishedContent Page { get; init; }
    public IPublishedContent? Brand { get; init; }
    public required string Lead { get; init; }
    public required (string Sale, string? List, string? Savings) Prices { get; init; }
    public StorefrontLink[] Variants { get; init; } = [];
    public required string OptionLabel { get; init; }
    public string? SizeGuide { get; init; }
    public int Weight { get; init; }
    public string? Capacity { get; init; }
    public required PurchaseModel Purchase { get; init; }
    public RecommendationModel? Recommendations { get; init; }
}
