internal sealed class CartModel
{
    public CartLineModel[] Lines { get; init; } = [];
    public Dictionary<string, string> Labels { get; init; } = [];
    public string CartUrl { get; init; } = "";
    public string CheckoutUrl { get; init; } = "";
    public string ContinueUrl { get; init; } = "";
    public string AntiforgeryField { get; init; } = "";
    public string AntiforgeryToken { get; init; } = "";
    public int Count { get; init; }
    public string Total { get; init; } = "";
    public bool Ready { get; init; }
}
