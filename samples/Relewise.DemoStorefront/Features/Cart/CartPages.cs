using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.DemoConstants;
using static Relewise.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.DemoStorefront.Features.Cart.CartModelBuilder;
using static Relewise.DemoStorefront.Features.Cart.CartPricing;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartPages
{
    public static async Task<IResult> Page(
        HttpContext http,
        bool checkout)
    {
        if (DemoConnection.RequireConnection(http) is { } blocked)
        {
            return blocked;
        }

        await http.Session.LoadAsync(http.RequestAborted);

        CartState basket = CartStateStore.Get(http);

        await basket.Gate.WaitAsync(http.RequestAborted);

        try
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();

            SetCulture(http);

            string key = checkout ? "Checkout" : "Cart";

            if (checkout)
            {
                http.Items[RequestItems.Checkout] = true;
            }

            List<CartLine> lines = [.. basket.Lines];
            PricedCartLine[] priced = Price(lines, context.UmbracoContext.Content, http, true)!;
            CheckoutLineModel[] reviewLines = !checkout
                ? []
                :
                [
                    .. lines.Select(line =>
                    {
                        PricedCartLine? item = priced.SingleOrDefault(value => value.Line == line);
                        IPublishedContent? page = item?.Variant ?? item?.Product;

                        return new CheckoutLineModel(
                            page == null ? null : SiteContent.Title(page),
                            page == null ? "" : Storefront.Image(page),
                            line.Quantity,
                            item == null ? "" : Money(http, item.Amount * line.Quantity));
                    })
                ];

            if (checkout && lines.Count > 0)
            {
                if (priced.Length == lines.Count && InStock(priced))
                {
                    string review = ReviewKey(priced, http);

                    if (basket.CheckoutKey != review || basket.CheckoutToken == null)
                    {
                        basket.CheckoutKey = review;
                        basket.CheckoutToken = Guid.NewGuid().ToString("N");
                    }
                }
                else
                {
                    basket.CheckoutToken = null;
                    basket.CheckoutKey = null;
                }
            }

            var model = new CartPageModel(
                checkout,
                checkout ? null : Model(http, lines, priced),
                reviewLines,
                lines.Count == 0,
                Money(http, priced.Sum(line => line.Amount * line.Line.Quantity)),
                checkout ? basket.CheckoutToken : null);
            var page = new StorefrontPage(Storefront.Text(http, key), "cart", "/Views/Cart/Index.cshtml", model);

            // Razor receives a snapshot; it never holds the cart lock while writing to a slow client.
            return new StorefrontResponse(page);
        }
        finally
        {
            basket.Gate.Release();
        }
    }

    public static IResult Confirmation(HttpContext http)
    {
        if (DemoConnection.RequireConnection(http) is { } blocked)
        {
            return blocked;
        }

        OrderReceipt? receipt = CartStateStore.Get(http).Receipt;

        if (receipt == null)
        {
            return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
        }

        http.Items[RequestItems.Checkout] = true;

        var page = new StorefrontPage(
            Storefront.Text(http, "Confirmation"),
            "cart",
            "/Views/Cart/Confirmation.cshtml",
            receipt);

        return new StorefrontResponse(page);
    }
}
