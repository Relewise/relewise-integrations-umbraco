namespace Relewise.DemoStorefront.Features.Cart;

internal sealed record OrderReceipt(
    string OrderNumber,
    string Currency,
    decimal Total,
    PurchasedCartLine[] Lines,
    string CheckoutToken);
