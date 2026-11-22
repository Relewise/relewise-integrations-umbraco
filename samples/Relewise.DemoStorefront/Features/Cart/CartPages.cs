using System.Text;
using System.Text.Json;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.DemoStorefront.Features.Cart.CartModelBuilder;
using static Relewise.DemoStorefront.Features.Cart.CartPricing;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartPages
{
    internal static async Task<IResult> Page(
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
                http.Items["Fieldwork:Checkout"] = true;
            }

            var html = new StringBuilder();

            if (!checkout)
            {
                html.Append(StorefrontNavigation.Breadcrumb(null, http, "shop", Storefront.Text(http, key)));
            }

            string heading = T(http, checkout ? "ReviewOrder" : "Cart");
            string introduction = checkout ? $"<p>{T(http, "ReviewIntro")}</p>" : string.Empty;

            html.Append($"<section class='hero'><h1>{heading}</h1>{introduction}</section>");

            List<CartLine> lines = [.. basket.Lines];
            PricedCartLine[] priced = Price(lines, context.UmbracoContext.Content, http, true)!;

            if (!checkout)
            {
                string json =
                    JsonSerializer.Serialize(Model(http, lines, priced), StorefrontChrome.Json);

                html.Append(
                    $"<div id='cart-page'></div><script id='cart-data' type='application/json'>{json}</script>");
            }
            else if (lines.Count == 0)
            {
                html.Append(
                    $"<p class='empty'>{T(http, "CartEmpty")}</p><a class='button' href='" +
                    $"{Storefront.E(StorefrontNavigation.ShoppingUrl(http))}'>{T(http, "ContinueShopping")}</a>");
            }
            else
            {
                AppendCheckout(html, lines, priced, basket, http);
            }

            return Html(http, key, html.ToString());
        }
        finally
        {
            basket.Gate.Release();
        }
    }

    private static void AppendCheckout(
        StringBuilder html,
        List<CartLine> lines,
        PricedCartLine[] priced,
        CartState basket,
        HttpContext http)
    {
        string path = DemoMarket.Current(http).LanguagePath;

        html.Append(
            $$"""
              <ol class="checkout-steps">
                  <li><a href="{{path}}/cart">{{T(http, "Cart")}}</a></li>
                  <li aria-current="step">{{T(http, "ReviewOrder")}}</li>
                  <li>{{T(http, "Confirmation")}}</li>
              </ol>
              <section class="checkout-review">
                  <div class="checkout-items"><h2>{{T(http, "CartItems")}}</h2>
              """);

        foreach (CartLine line in lines)
        {
            PricedCartLine? item = priced.SingleOrDefault(value => value.Line == line);

            if (item == null)
            {
                html.Append($"<p role='alert'>{T(http, "UnavailableItem")}</p>");
                continue;
            }

            IPublishedContent page = item.Variant ?? item.Product;
            string title = Storefront.E(SiteContent.Title(page));

            html.Append(
                $$"""
                  <div class="review-item">
                      <div class="illustration">{{Storefront.Image(page)}}</div>
                      <span>{{title}}<small>{{T(http, "Quantity")}}: {{line.Quantity}}</small></span>
                      <strong>{{Money(http, item.Amount * line.Quantity)}}</strong>
                  </div>
                  """);
        }

        decimal total = priced.Sum(line => line.Amount * line.Line.Quantity);
        string totalLabel = T(http, "Total");

        html.Append(
            $$"""
                  </div>
                  <aside class="checkout-summary">
                      <h2>{{T(http, "OrderSummary")}}</h2>
                      <div class="cart-total"><span>{{totalLabel}}</span><strong>{{Money(http, total)}}</strong></div>
                      <p class="muted">{{T(http, "DemoCheckout")}}</p>
              """);

        bool ready = priced.Length == lines.Count && InStock(priced);

        if (ready)
        {
            string review = ReviewKey(priced, http);

            if (basket.CheckoutKey != review || basket.CheckoutToken == null)
            {
                basket.CheckoutKey = review;
                basket.CheckoutToken = Guid.NewGuid().ToString("N");
            }

            html.Append(
                $$"""
                  <form method="post" action="{{path}}/checkout">
                      {{FormFields.Token(http)}}{{FormFields.Hidden("checkoutToken", basket.CheckoutToken)}}
                      <button>{{T(http, "PlaceOrder")}}</button>
                  </form>
                  """);
        }
        else
        {
            basket.CheckoutToken = null;
            basket.CheckoutKey = null;
            html.Append($"<p role='alert'>{T(http, "CartUnavailable")}</p>");
        }

        html.Append("</aside></section>");
    }

    internal static IResult Confirmation(HttpContext http)
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

        http.Items["Fieldwork:Checkout"] = true;

        var html = new StringBuilder();

        html.Append(
            $$"""
              <section class="hero">
                  <p class="eyebrow">{{T(http, "DemoOrder")}}</p>
                  <h1>{{T(http, "ThankYou")}}</h1>
                  <p>{{T(http, "OrderConfirmed")}}</p>
                  <p>{{T(http, "OrderNumber")}}: <strong>{{Storefront.E(receipt.OrderNumber)}}</strong></p>
              </section>
              <div class="order-summary">
              """);

        foreach (PurchasedCartLine line in receipt.Lines)
        {
            html.Append(
                $"<p>{Storefront.E(line.Name)} × {line.Quantity}<strong>" +
                $"{Money(http, line.Total, receipt.Currency)}</strong></p>");
        }

        string totalLabel = T(http, "Total");
        string totalValue = Money(http, receipt.Total, receipt.Currency);

        string continueUrl = Storefront.E(StorefrontNavigation.ShoppingUrl(http));
        string continueLabel = T(http, "ContinueShopping");

        html.Append(
            $$"""
                  <div class="cart-total"><span>{{totalLabel}}</span><strong>{{totalValue}}</strong></div>
              </div>
              <a class="button" href="{{continueUrl}}">{{continueLabel}}</a>
              """);

        return Html(http, "Confirmation", html.ToString());
    }

    private static IResult Html(HttpContext http, string title, string body, int status = 200)
    {
        return Results.Content(
            Storefront.Layout(
                Storefront.Text(http, title),
                body,
                http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null,
                http,
                "cart"
            ),
            "text/html",
            statusCode: status
        );
    }
}
