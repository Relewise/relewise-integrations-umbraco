using Microsoft.AspNetCore.Antiforgery;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Integration;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;
using static Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart.CartPricing;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

internal static class CartModelBuilder
{
    public static CartLineModel LineModel(
        CartLine line,
        PricedCartLine? priced,
        HttpContext http,
        int? quantity = null)
    {
        IPublishedContent? page = priced?.Variant ?? priced?.Product;
        int amount = quantity ?? line.Quantity;

        return new CartLineModel
        {
            ProductKey = line.ProductKey,
            VariantKey = line.VariantKey,
            ProductId = priced == null ? "" : DemoConfiguration.EntityId(priced.Product, http.RequestServices),
            VariantId = priced?.Variant == null
                ? null
                : DemoConfiguration.EntityId(priced.Variant, http.RequestServices),
            Name = page == null ? Storefront.Text(http, "UnavailableItem") : SiteContent.Title(page),
            Url = page?.Url() ?? "",
            ImageHtml = page == null ? "" : Storefront.Image(page),
            Quantity = amount,
            Maximum = priced == null ? 0 : Math.Max(0, Math.Min(99, priced.Product.Value<int>(PropertyAliases.Stock))),
            UnitPrice = priced == null ? "" : Money(http, priced.Amount),
            LineTotal = priced == null ? "" : Money(http, priced.Amount * amount),
            Available = priced != null
        };
    }

    public static CartModel Model(
        HttpContext http,
        List<CartLine> lines,
        PricedCartLine[] priced)
    {
        string path = DemoMarket.Current(http).LanguagePath;
        AntiforgeryTokenSet token = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        string[] labels =
        [
            "CartIntro", "EmptyCartHeading", "EmptyCartDescription", "ExploreCollection", "BrowseOffers", "CartEmpty",
            "ContinueShopping", "CartItems", "Quantity", "Each", "Total", "OrderSummary", "Checkout", "RemoveCart",
            "ChangeQuantity",
            "EmptyCartAction", "EmptyCartConfirm", "Cancel", "CartUpdating", "CartUpdated", "CartUnavailable",
            "UnavailableItem", "UnavailableTotal", "PurchaseFailed", "Close"
        ];

        return new CartModel
        {
            Lines = [.. lines.Select(line => LineModel(line, priced.SingleOrDefault(item => item.Line == line), http))],
            Labels = labels.ToDictionary(key => key, key => Storefront.Text(http, key)),
            CartUrl = path + "/cart",
            CheckoutUrl = path + "/checkout",
            ContinueUrl = StorefrontNavigation.ShoppingUrl(http),
            ShopUrl = path + "/store",
            OffersUrl = path + "/offers",
            Recommendations = RecommendationPlacement.Configuration(
                SiteContent.Home(http.RequestServices),
                http,
                true
            ),
            AntiforgeryField = token.FormFieldName,
            AntiforgeryToken = token.RequestToken ?? "",
            Count = lines.Sum(line => line.Quantity),
            Total = Money(http, priced.Sum(line => line.Amount * line.Line.Quantity)),
            Ready = lines.Count > 0 && priced.Length == lines.Count && InStock(priced)
        };
    }
}
