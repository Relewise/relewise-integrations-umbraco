using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Relewise.DemoStorefront.Features.Cart;
using Relewise.DemoStorefront.Setup;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Relewise.DemoStorefront.Site;

internal static class StorefrontChrome
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string Render(
        string title,
        string body,
        HttpContext http,
        string section)
    {
        http.Response.Headers.CacheControl = "no-store";

        DemoMarket market = DemoMarket.Current(http);

        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(market.Culture);

        var checkout = http.Items.ContainsKey("Fieldwork:Checkout");
        using UmbracoContextReference context =
            http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        IPublishedContent home = SiteContent.Home(http.RequestServices);
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();

        bool Available(IPublishedContent node)
        {
            return node.Cultures.ContainsKey(market.Culture) && !access.IsProtected(node.Path).Success;
        }

        string path = http.Request.Path;
        List<StorefrontLink> links =
        [
            new(
                Storefront.Text(http, "Shop"),
                market.StoreUrl,
                section == "shop" && path.TrimEnd('/') == market.StoreUrl
            )
        ];

        links.AddRange(
            SiteContent.ShoppingCategories(home)
                .Where(Available)
                .Select(node =>
                    new StorefrontLink(
                        SiteContent.Title(node),
                        node.Url(market.Culture),
                        path.StartsWith(node.Url(market.Culture), StringComparison.OrdinalIgnoreCase)
                    )));
        links.Add(
            new StorefrontLink(
                Storefront.Text(http, "OffersNavigation"),
                market.LanguagePath + "/offers",
                section == "offers",
                DemoConnection.SearchConfigured(http)
            ));
        links.Add(
            new StorefrontLink(
                Storefront.Text(http, "Journal"),
                market.StoreUrl + "?type=article",
                section == "journal"));

        string[] keys =
        [
            "OutdoorGoods", "MainNavigation", "Menu", "Close", "Cart", "Search", "SearchProducts", "SearchJournal",
            "SearchPlaceholder", "EditorialSearchPlaceholder", "ClearSearch", "SuggestedSearches",
            "SuggestionsAvailable", "RefineSuggestion",
            "Market", "ChooseMarket", "Apply", "ViewCart", "Checkout", "ContinueShopping", "AddedToCart", "Quantity",
            "Total", "Each", "EditCart", "Confirmation", "DemoStorefront", "SearchDisabled", "ShoppingDisabled",
            "Shop", "Journal", "FooterIntroduction", "FooterNavigation", "OurBrands", "Explore", "ChangeMarket",
            "PreviewMode", "SearchProvider", "ConfigureRelewise"
        ];
        Dictionary<string, string> shellLabels = keys.ToDictionary(key => key, key => Storefront.Text(http, key));

        if (http.Request.Path.Value?.EndsWith("/confirmation", StringComparison.Ordinal) == true)
        {
            shellLabels["EditCart"] = Storefront.Text(http, "ContinueShopping");
        }

        var model = new StorefrontShellModel
        {
            Labels = shellLabels,
            HomeUrl = market.HomeUrl,
            CartUrl = http.Request.Path.Value?.EndsWith("/confirmation", StringComparison.Ordinal) == true
                ? market.StoreUrl
                : market.LanguagePath + "/cart",
            SearchUrl = market.LanguagePath + "/search",
            SuggestionsUrl = market.LanguagePath + "/store/suggestions",

            Market = market.Id,
            MarketLabel = Storefront.Text(
                http,
                market.Id switch { "dk" => "DenmarkShort", "us" => "UnitedStatesShort", _ => "EuropeShort" }),
            Markets =
            [
                .. DemoMarket.All.Select(option =>
                    new StorefrontMarketOption(option.Id, Storefront.Text(http, option.LabelKey)))
            ],

            Links = [.. links],
            ShopLinks =
            [
                .. links.Where(link => link.Url != market.StoreUrl + "?type=article")
                    .Select(link =>
                        link.Url == market.StoreUrl
                            ? link with { Label = Storefront.Text(http, "ShopAllProducts") }
                            : link)
            ],
            BrandLinks =
            [
                .. SiteContent.ShoppingBrands(home)
                    .Where(Available)
                    .Select(node =>
                        new StorefrontLink(
                            SiteContent.Title(node),
                            node.Url(market.Culture),
                            section == "shop" && path == node.Url(market.Culture)
                        ))
            ],

            JournalUrl = market.StoreUrl + "?type=article",

            DatasetId = DemoConnection.IsConfigured(http)
                ? http.RequestServices.GetRequiredService<IConfiguration>()["Relewise:DatasetId"]
                : null,

            Count = CartStateStore.Count(http),
            Checkout = checkout,

            SearchConfigured = DemoConnection.SearchConfigured(http),
            ShoppingConfigured = DemoConnection.IsConfigured(http),
            SearchPage = section == "search",
            SearchScope = section == "journal" || (section == "search" && http.Request.Query["type"] == "article")
                ? "article"
                : "product",
            Query = section == "search" ? http.Request.Query["q"].ToString() : ""
        };

        var versions = http.RequestServices.GetRequiredService<IFileVersionProvider>();
        var css = versions.AddFileVersionToPath(http.Request.PathBase, "/storefront.css");
        var script = versions.AddFileVersionToPath(http.Request.PathBase, "/storefront/listing.js");

        var previewNotice = DemoConnection.IsConfigured(http) ? "" : DemoConnection.PreviewNotice(http);

        var shellJson = JsonSerializer.Serialize(model, Json);
        var pageClass = checkout ? "checkout-page" : "shopping-page";

        return $$"""
                 <!doctype html>
                 <html lang="{{market.Culture}}">
                 <head>
                     <meta charset="utf-8">
                     <meta name="viewport" content="width=device-width">
                     <title>{{Storefront.E(title)}} | Fieldwork</title>
                     <link rel="icon" href="/favicon.svg" type="image/svg+xml">
                     <link rel="stylesheet" href="{{Storefront.E(css)}}">
                     <script type="module" src="{{Storefront.E(script)}}"></script>
                 </head>
                 <body class="{{pageClass}}">
                     <div id="storefront-shell"></div>
                     <script id="shell-data" type="application/json">{{shellJson}}</script>
                     <main>{{previewNotice}}{{StorefrontNavigation.Feedback(http)}}{{body}}</main>
                     <div id="storefront-footer"></div>
                 </body>
                 </html>
                 """;
    }
}
