using Microsoft.AspNetCore.Http;

// Application preferences: these are not integration configuration or Dataset boundaries.
internal sealed record DemoMarket(string Id, string Label, string Culture, string Currency)
{
    internal const string CookieName = "storefront-market";
    internal static readonly DemoMarket[] All =
    [
        new("dk", "Denmark · Dansk · DKK", "da-DK", "DKK"),
        new("eu", "Europe · English · EUR", "en-US", "EUR"),
        new("us", "United States · English · USD", "en-US", "USD")
    ];

    internal static DemoMarket Current(HttpContext? context)
        => All.FirstOrDefault(market => market.Id == context?.Request.Cookies[CookieName]) ?? All[0];

    internal string EditorialUrl => Culture == "da-DK" ? "/da/" : "/en/";

    internal string ReturnUrl(string? value)
    {
        // Keep redirects on this site, including when the form is submitted by hand.
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') || value.StartsWith("//")
            || value.Contains('\\') || value.Any(char.IsControl))
        {
            return "/store";
        }

        int suffix = value.IndexOfAny(['?', '#']);
        string path = (suffix < 0 ? value : value[..suffix]).TrimEnd('/');
        return path is "/en" or "/da" ? EditorialUrl + (suffix < 0 ? "" : value[suffix..]) : value;
    }
}
