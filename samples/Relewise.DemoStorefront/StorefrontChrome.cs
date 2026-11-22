using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

internal static class StorefrontChrome
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string Render(string title, string body, HttpContext http, string section)
    {
        http.Response.Headers.CacheControl = "no-store";
        var market = DemoMarket.Current(http);
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(market.Culture);
        string T(string key) => Storefront.E(Storefront.Text(http, key));
        bool checkout = http.Items.ContainsKey("Fieldwork:Checkout");
        using var context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        var home = StorefrontPages.Home(http.RequestServices);
        string path = http.Request.Path;
        var links = new List<StorefrontLink>
        {
            new(Storefront.Text(http, "Shop"), market.StoreUrl, section == "shop" && path.TrimEnd('/') == market.StoreUrl)
        };
        if (home != null)
        {
            links.AddRange(StorefrontPages.ShoppingCategories(home).Where(node => node.Cultures.ContainsKey(market.Culture)).Select(node =>
                new StorefrontLink(StorefrontPages.Title(node), node.Url(market.Culture), path.StartsWith(node.Url(market.Culture), StringComparison.OrdinalIgnoreCase))));
        }
        links.Add(new(Storefront.Text(http, "OffersNavigation"), market.LanguagePath + "/offers", section == "offers", DemoConnection.SearchConfigured(http)));
        links.Add(new(Storefront.Text(http, "Journal"), market.StoreUrl + "?type=article", section == "journal"));
        string[] keys = ["OutdoorGoods", "MainNavigation", "Menu", "Close", "Cart", "Search", "SearchProducts", "SearchJournal", "SearchPlaceholder", "EditorialSearchPlaceholder",
            "Market", "ChooseMarket", "Apply", "ViewCart", "Checkout", "ContinueShopping", "AddedToCart", "Quantity", "Total", "Each", "EditCart", "Confirmation", "DemoStorefront", "SearchDisabled", "ShoppingDisabled"];
        var shellLabels = keys.ToDictionary(key => key, key => Storefront.Text(http, key));
        if (http.Request.Path.Value?.EndsWith("/confirmation", StringComparison.Ordinal) == true)
        {
            shellLabels["EditCart"] = Storefront.Text(http, "ContinueShopping");
        }
        var model = new StorefrontShellModel
        {
            Labels = shellLabels,
            HomeUrl = market.HomeUrl,
            CartUrl = http.Request.Path.Value?.EndsWith("/confirmation", StringComparison.Ordinal) == true ? market.StoreUrl : market.LanguagePath + "/cart",
            SearchUrl = market.LanguagePath + "/search",
            SuggestionsUrl = market.LanguagePath + "/store/suggestions",
            Market = market.Id,
            MarketLabel = Storefront.Text(http, market.Id switch { "dk" => "DenmarkShort", "us" => "UnitedStatesShort", _ => "EuropeShort" }),
            Markets = DemoMarket.All.Select(option => new StorefrontMarketOption(option.Id, Storefront.Text(http, option.LabelKey))).ToArray(),
            Links = links.ToArray(),
            Count = StorefrontCart.Count(http),
            Checkout = checkout,
            SearchConfigured = DemoConnection.SearchConfigured(http),
            ShoppingConfigured = DemoConnection.IsConfigured(http),
            SearchPage = section == "search",
            SearchScope = http.Request.Query["type"] == "article" ? "article" : "product",
            Query = section == "search" ? http.Request.Query["q"].ToString() : ""
        };
        var versions = http.RequestServices.GetRequiredService<IFileVersionProvider>();
        string css = versions.AddFileVersionToPath(http.Request.PathBase, "/storefront.css");
        string script = versions.AddFileVersionToPath(http.Request.PathBase, "/storefront/listing.js");
        string details = checkout ? string.Empty : DemoDetails(http);
        string previewNotice = DemoConnection.IsConfigured(http) ? "" : DemoConnection.PreviewNotice(http);
        return $"<!doctype html><html lang='{market.Culture}'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>{Storefront.E(title)} | Fieldwork</title><link rel='stylesheet' href='{Storefront.E(css)}'><script type='module' src='{Storefront.E(script)}'></script></head><body class='{(checkout ? "checkout-page" : "shopping-page")}'><div id='storefront-shell'></div><script id='shell-data' type='application/json'>{JsonSerializer.Serialize(model, Json)}</script><main>{previewNotice}{StorefrontNavigation.Feedback(http)}{body}</main><footer class='site-footer'><span>FIELDWORK · {T("DemoStorefront")}</span>{details}</footer></body></html>";
    }

    private static string DemoDetails(HttpContext http)
    {
        bool configured = http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null;
        string T(string key) => Storefront.E(Storefront.Text(http, key));
        string provider = DemoConnection.SearchConfigured(http) ? "Relewise" : T("SearchDisabled");
        string dataset = configured ? Storefront.E(http.RequestServices.GetRequiredService<IConfiguration>()["Relewise:DatasetId"]) : "—";
        return $"<details class='demo-details'><summary>{T("DemoDetails")} <span class='{(configured ? "" : "demo-warning")}'>{T(configured ? "RelewiseConfigured" : "PreviewMode")}</span></summary><div>{(configured ? "" : $"<p>{T("DemoPreviewHelp")}</p>")}<dl><dt>{T("SearchProvider")}</dt><dd>{provider}</dd><dt>Dataset ID</dt><dd>{dataset}</dd></dl><p><a href='https://github.com/Relewise/relewise-integrations-umbraco/tree/main/samples/Relewise.DemoStorefront#connect-a-local-relewise-dataset'>{T("ConfigureRelewise")}</a></p></div></details>";
    }
}
