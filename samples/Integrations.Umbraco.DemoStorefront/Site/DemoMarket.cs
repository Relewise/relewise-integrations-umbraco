using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;
namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

// Application preferences: these are not integration configuration or Dataset boundaries.
internal sealed record DemoMarket(
    string Id,
    string LabelKey,
    string Culture,
    string Currency)
{
    public static readonly DemoMarket[] All =
    [
        new("eu", "Europe", "en-US", "EUR"),
        new("dk", "Denmark", "da-DK", "DKK"),
        new("us", "UnitedStates", "en-US", "USD")
    ];

    public static DemoMarket Current(HttpContext? context)
    {
        if (context?.Items[typeof(DemoMarket)] is DemoMarket selected)
        {
            return selected;
        }

        DemoMarket? saved = All.FirstOrDefault(market => market.Id == context?.Request.Cookies[Cookies.Market]);
        string? prefix = context?.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        // Explicit language URLs win over a remembered market; English still permits both EUR and USD.
        return prefix switch
        {
            "da" => All.Single(market => market.Id == "dk"),
            "en" => saved?.Culture == "en-US" ? saved : All[0],
            _ => saved ?? All[0]
        };
    }

    public string LanguagePath => Culture == "da-DK" ? "/da" : "/en";
    public string HomeUrl => LanguagePath + "/";
    public string StoreUrl => LanguagePath + "/store";

    public string ReturnUrl(string? value)
    {
        // Keep redirects on this site, including when the form is submitted by hand.
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') || value.StartsWith("//")
            || value.Contains('\\') || value.Any(char.IsControl))
        {
            return StoreUrl;
        }

        int suffix = value.IndexOfAny(['?', '#']);
        string path = (suffix < 0 ? value : value[..suffix]).TrimEnd('/');
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > 0 && parts[0] is "en" or "da")
        {
            if (parts.Length > 2 && parts[1] == "cart")
            {
                return LanguagePath + "/cart";
            }

            parts[0] = LanguagePath.TrimStart('/');

            return "/" + string.Join('/', parts) + (suffix < 0 ? "" : value[suffix..]);
        }

        return path == "/store" ? StoreUrl + (suffix < 0 ? "" : value[suffix..]) : StoreUrl;
    }
}
