namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

internal sealed record CartLine(Guid ProductKey, Guid? VariantKey, int Quantity);
