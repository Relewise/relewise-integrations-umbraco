using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.DemoStorefront.Features.Cart.CartPricing;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartModelBuilder
{
    internal static CartLineModel LineModel(
        CartLine line,
        PricedCartLine? priced,
        HttpContext http,
        int? quantity = null)
    {
        IPublishedContent? page = priced?.Variant ?? priced?.Product;
        var amount = quantity ?? line.Quantity;

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
            Maximum = priced == null ? 0 : Math.Max(0, Math.Min(99, priced.Product.Value<int>("stock"))),
            UnitPrice = priced == null ? "" : WebUtility.HtmlDecode(Money(http, priced.Amount)),
            LineTotal = priced == null ? "" : WebUtility.HtmlDecode(Money(http, priced.Amount * amount)),
            Available = priced != null
        };
    }

    internal static CartModel Model(
        HttpContext http,
        List<CartLine> lines,
        PricedCartLine[] priced)
    {
        var path = DemoMarket.Current(http).LanguagePath;
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
            Total = WebUtility.HtmlDecode(Money(http, priced.Sum(line => line.Amount * line.Line.Quantity))),
            Ready = lines.Count > 0 && priced.Length == lines.Count && InStock(priced)
        };
    }
}
