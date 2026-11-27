namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

/// <summary>
/// Display values for one checkout line, detached from mutable cart state.
/// </summary>
public sealed record CheckoutLineModel(string? Title, string ImageHtml, int Quantity, string Total);
