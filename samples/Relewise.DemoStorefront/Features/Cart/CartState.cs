namespace Relewise.DemoStorefront.Features.Cart;

internal sealed class CartState
{
    public Dictionary<Guid, string> Additions { get; } = [];
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public string? CheckoutKey { get; set; }
    public CartLine[] Lines { get; set; } = [];
    public OrderReceipt? Receipt { get; set; }
    public string? CheckoutToken { get; set; }
}
