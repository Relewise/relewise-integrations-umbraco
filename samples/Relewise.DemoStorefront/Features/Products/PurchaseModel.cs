namespace Relewise.DemoStorefront.Features.Products;

/// <summary>Availability and form data for a product purchase control.</summary>
public sealed record PurchaseModel(
    string Availability,
    string? AvailableUrl,
    string? Message,
    int Maximum,
    Dictionary<string, string> Fields,
    string? Json);
