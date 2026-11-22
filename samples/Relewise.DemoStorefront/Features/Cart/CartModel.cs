using Relewise.DemoStorefront.Features.Recommendations;

namespace Relewise.DemoStorefront.Features.Cart;

/// <summary>
/// A cart snapshot and labels shared with the cart component.
/// </summary>
public sealed class CartModel
{
    public CartLineModel[] Lines { get; init; } = [];
    public Dictionary<string, string> Labels { get; init; } = [];
    public string CartUrl { get; init; } = "";
    public string CheckoutUrl { get; init; } = "";
    public string ContinueUrl { get; init; } = "";
    public string ShopUrl { get; init; } = "";
    public string OffersUrl { get; init; } = "";
    public RecommendationModel? Recommendations { get; init; }
    public string AntiforgeryField { get; init; } = "";
    public string AntiforgeryToken { get; init; } = "";
    public int Count { get; init; }
    public string Total { get; init; } = "";
    public bool Ready { get; init; }
}
