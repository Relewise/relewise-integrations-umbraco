namespace Relewise.DemoStorefront;

internal sealed class StorefrontShellModel
{
    public Dictionary<string, string> Labels { get; init; } = [];
    public string HomeUrl { get; init; } = "";
    public string CartUrl { get; init; } = "";
    public string SearchUrl { get; init; } = "";
    public string SuggestionsUrl { get; init; } = "";
    public string MarketUrl { get; init; } = "/store/market";
    public string Market { get; init; } = "";
    public string MarketLabel { get; init; } = "";
    public StorefrontMarketOption[] Markets { get; init; } = [];
    public StorefrontLink[] Links { get; init; } = [];
    public StorefrontLink[] ShopLinks { get; init; } = [];
    public StorefrontLink[] BrandLinks { get; init; } = [];
    public string JournalUrl { get; init; } = "";
    public string? DatasetId { get; init; }
    public int Count { get; init; }
    public bool Checkout { get; init; }
    public bool SearchConfigured { get; init; }
    public bool ShoppingConfigured { get; init; }
    public bool SearchPage { get; init; }
    public string SearchScope { get; init; } = "product";
    public string Query { get; init; } = "";
}
