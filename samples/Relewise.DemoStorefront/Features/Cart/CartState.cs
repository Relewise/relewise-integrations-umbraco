namespace Relewise.DemoStorefront.Features.Cart;

internal sealed class CartState
{
    internal Dictionary<Guid, string> Additions { get; } = [];
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal string? CheckoutKey { get; set; }
    internal CartLine[] Lines { get; set; } = [];
    internal OrderReceipt? Receipt { get; set; }
    internal string? CheckoutToken { get; set; }
}
