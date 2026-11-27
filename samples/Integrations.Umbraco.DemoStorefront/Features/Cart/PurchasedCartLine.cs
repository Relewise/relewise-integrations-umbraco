namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

/// <summary>
/// A purchased line captured at the time of the demo order.
/// </summary>
public sealed record PurchasedCartLine(string Name, int Quantity, decimal Total);
