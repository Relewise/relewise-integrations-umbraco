namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;

/// <summary>
/// Display values for a product or editorial card.
/// </summary>
public sealed record ListingCard(
    Guid Key,
    string EntityId,
    string Url,
    string Title,
    string Summary,
    string ImageHtml,
    bool Product,
    string Price,
    string? ListPrice,
    string Availability,
    string? Options,
    string? Savings = null,
    string? Brand = null);
