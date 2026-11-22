namespace Relewise.DemoStorefront;

internal sealed record CartMutationResult(CartModel Cart, CartLineModel? Added = null);
