using System.Text.Json;
using Relewise.DemoStorefront.Features.Cart;
using Relewise.DemoStorefront.Setup;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Site;

internal static class StorefrontChrome
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static StorefrontShellModel Create(HttpContext http, string section)
    {
        http.Response.Headers.CacheControl = "no-store";

        DemoMarket market = DemoMarket.Current(http);

        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(market.Culture);

        bool checkout = http.Items.ContainsKey(RequestItems.Checkout);
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

        return new StorefrontShellModel
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
                ? http.RequestServices.GetRequiredService<IConfiguration>()[ConfigurationKeys.DatasetId]
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
    }
}
