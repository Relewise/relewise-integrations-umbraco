using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Cart;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal sealed record CartMutationResult(CartModel Cart, CartLineModel? Added = null);
