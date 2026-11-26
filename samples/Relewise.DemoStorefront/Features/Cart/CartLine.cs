namespace Relewise.DemoStorefront.Features.Cart;

internal sealed record CartLine(Guid ProductKey, Guid? VariantKey, int Quantity);
