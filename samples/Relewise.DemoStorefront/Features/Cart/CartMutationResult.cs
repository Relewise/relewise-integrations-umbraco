namespace Relewise.DemoStorefront.Features.Cart;

internal sealed record CartMutationResult(CartModel Cart, CartLineModel? Added = null);
