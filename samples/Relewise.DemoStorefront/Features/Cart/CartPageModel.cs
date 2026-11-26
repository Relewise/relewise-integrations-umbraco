namespace Relewise.DemoStorefront.Features.Cart;

/// <summary>
/// An immutable cart or checkout presentation snapshot.
/// </summary>
public sealed record CartPageModel(
    bool Checkout,
    CartModel? Cart,
    CheckoutLineModel[] Lines,
    bool Empty,
    string Total,
    string? CheckoutToken);
