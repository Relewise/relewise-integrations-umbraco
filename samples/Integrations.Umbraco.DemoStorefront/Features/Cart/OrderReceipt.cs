namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

/// <summary>
/// The completed demo order shown on the confirmation page.
/// </summary>
public sealed record OrderReceipt(
    string OrderNumber,
    string Currency,
    decimal Total,
    PurchasedCartLine[] Lines,
    string CheckoutToken);
