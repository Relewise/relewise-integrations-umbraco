using System.Globalization;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.DemoConstants;
using static Relewise.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.DemoStorefront.Features.Cart.CartModelBuilder;
using static Relewise.DemoStorefront.Features.Cart.CartPricing;
using static Relewise.DemoStorefront.Features.Cart.CartTracking;

namespace Relewise.DemoStorefront.Features.Cart;

internal static class CartOperations
{
    internal static async Task ChangeMarketAsync(HttpContext http)
    {
        if (!DemoConnection.IsConfigured(http))
        {
            return;
        }

        await http.Session.LoadAsync(http.RequestAborted);

        CartState current = CartStateStore.Get(http);

        await current.Gate.WaitAsync(http.RequestAborted);

        // Serialize tracking with the state change so concurrent requests cannot overwrite a newer cart.
        try
        {
            current.CheckoutToken = null;
            current.CheckoutKey = null;

            List<CartLine> lines = [.. current.Lines];

            if (lines.Count == 0)
            {
                return;
            }

            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();

            SetCulture(http);

            PricedCartLine[]? priced = Price(lines, context.UmbracoContext.Content, http, true);

            if (priced != null)
            {
                await TrackCart(priced, http);
            }
        }
        finally
        {
            current.Gate.Release();
        }
    }

    internal static async Task<IResult> ExecuteAsync(
        HttpContext http,
        string action,
        IFormCollection form)
    {
        http.Response.Headers.CacheControl = "no-store";
        await http.Session.LoadAsync(http.RequestAborted);

        CartState current = CartStateStore.Get(http);

        await current.Gate.WaitAsync(http.RequestAborted);

        // Serialize tracking with the state change so concurrent requests cannot overwrite a newer cart.
        try
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();

            SetCulture(http);

            List<CartLine> lines = [.. current.Lines];
            var commandId = Guid.Empty;
            string? signature = null;

            if (action == "checkout")
            {
                string? token = current.CheckoutToken;
                OrderReceipt? previous = current.Receipt;

                if (previous != null && previous.CheckoutToken == form["checkoutToken"].ToString())
                {
                    return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/confirmation");
                }

                if (string.IsNullOrEmpty(token) || token != form["checkoutToken"] || lines.Count == 0)
                {
                    return Error(http, "CartInvalid");
                }

                PricedCartLine[]? priced = Price(lines, context.UmbracoContext.Content, http);

                if (priced == null || !InStock(priced) || current.CheckoutKey != ReviewKey(priced, http))
                {
                    return Error(http, "CartUnavailable");
                }

                // Reuse the checkout attempt's identity if tracking succeeded but its response was lost.
                string orderNumber = "FW-" + token[..12].ToUpperInvariant();
                decimal total = priced.Sum(line => line.Amount * line.Line.Quantity);
                string currency = DemoMarket.Current(http).Currency;

                await TrackOrderAsync(priced, orderNumber, http);

                PurchasedCartLine[] purchased =
                [
                    .. priced.Select(line => new PurchasedCartLine(
                        SiteContent.Title(line.Variant ?? line.Product),
                        line.Line.Quantity,
                        line.Amount * line.Line.Quantity))
                ];
                var receipt = new OrderReceipt(orderNumber, currency, total, purchased, token);

                current.Receipt = receipt;
                current.Lines = [];
                current.CheckoutToken = null;
                current.CheckoutKey = null;

                return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/confirmation");
            }

            if (action == "clear")
            {
                lines.Clear();
            }
            else
            {
                bool remove = form["remove"] == "true";
                var quantity = 0;

                if (!Guid.TryParse(form["product"], out Guid productKey) ||
                    (!remove && (!int.TryParse(form["quantity"], out quantity) || quantity < 0 || quantity > 99)))
                {
                    return Error(http, "CartInvalid");
                }

                Guid? variantKey = null;

                if (!string.IsNullOrWhiteSpace(form["variant"]))
                {
                    if (!Guid.TryParse(form["variant"], out Guid parsedVariant))
                    {
                        return Error(http, "CartInvalid");
                    }

                    variantKey = parsedVariant;
                }

                // Reusing a command ID makes retrying a lost response safe without adding the item twice.
                if (action == "add" && WantsJson(http))
                {
                    if (!Guid.TryParse(form["commandId"], out commandId) || commandId == Guid.Empty)
                    {
                        return Error(http, "CartInvalid");
                    }

                    signature = $"{productKey:D}|{variantKey:D}|{quantity}";

                    if (current.Additions.TryGetValue(commandId, out string? previousCommand))
                    {
                        if (previousCommand != signature)
                        {
                            return Error(http, "CartInvalid");
                        }

                        PricedCartLine[] snapshot = Price(
                            lines,
                            context.UmbracoContext.Content,
                            http,
                            true
                        )!;
                        PricedCartLine? item = snapshot.SingleOrDefault(value =>
                            value.Line.ProductKey == productKey && value.Line.VariantKey == variantKey);
                        CartLineModel? addedLine = item == null ? null : LineModel(item.Line, item, http, quantity);

                        return Results.Json(new CartMutationResult(Model(http, lines, snapshot), addedLine));
                    }
                }

                CartLine? existing =
                    lines.SingleOrDefault(line => line.ProductKey == productKey && line.VariantKey == variantKey);

                if (action == "add")
                {
                    if (quantity == 0)
                    {
                        return Error(http, "CartInvalid");
                    }

                    quantity += existing?.Quantity ?? 0;
                }

                lines.RemoveAll(line => line.ProductKey == productKey && line.VariantKey == variantKey);

                if (quantity > 0)
                {
                    lines.Add(new CartLine(productKey, variantKey, quantity));
                }
            }

            bool removing = action == "clear" || form["remove"] == "true" ||
                            (action == "update" && form["quantity"] == "0");
            PricedCartLine[]? updated = Price(lines, context.UmbracoContext.Content, http, removing);

            if (updated == null)
            {
                return Error(http, "CartUnavailable");
            }

            if (!removing && !InStock(updated))
            {
                IGrouping<Guid, PricedCartLine> exceeded = updated.GroupBy(line => line.Product.Key)
                    .First(
                        group => group.Sum(
                            line => line.Line.Quantity) > group.First().Product.Value<int>(PropertyAliases.Stock));
                IPublishedContent product = exceeded.First().Product;
                var stock = Math.Max(
                    0,
                    product.Value<int>(PropertyAliases.Stock)).ToString(CultureInfo.InvariantCulture);

                return Error(http, "CartStockLimit", product.Name, stock);
            }

            await TrackCart(updated, http);

            current.Lines = [.. lines];
            current.CheckoutToken = null;
            current.CheckoutKey = null;

            if (commandId != Guid.Empty)
            {
                if (current.Additions.Count >= 16)
                {
                    current.Additions.Remove(current.Additions.Keys.First());
                }

                current.Additions[commandId] = signature!;
            }

            if (WantsJson(http))
            {
                PricedCartLine? added = action == "add"
                    ? updated.Single(value =>
                        value.Line.ProductKey == Guid.Parse(form["product"].ToString()) &&
                        value.Line.VariantKey == (Guid.TryParse(form["variant"], out Guid key) ? key : null))
                    : null;
                CartLineModel? addedLine = added == null
                    ? null
                    : LineModel(
                        added.Line,
                        added,
                        http,
                        int.Parse(form["quantity"].ToString(), CultureInfo.InvariantCulture));

                return Results.Json(new CartMutationResult(Model(http, lines, updated), addedLine));
            }

            if (action == "add")
            {
                Guid addedProduct = Guid.Parse(form["product"].ToString());
                Guid? addedVariant = Guid.TryParse(form["variant"], out Guid variant) ? variant : null;
                PricedCartLine added = updated.Single(line =>
                    line.Line.ProductKey == addedProduct && line.Line.VariantKey == addedVariant);
                IPublishedContent page = added.Variant ?? added.Product;

                StorefrontNavigation.Notify(http, "CartAdded", false, SiteContent.Title(page));

                return Results.LocalRedirect(page.Url(DemoMarket.Current(http).Culture));
            }

            StorefrontNavigation.Notify(http, action == "clear" ? "CartCleared" : "CartUpdated");

            return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("StorefrontCart")
                .LogError(error, "Unable to complete the demo cart action.");

            return Error(
                http,
                DemoConnection.IsConnectionFailure(error, http.RequestAborted) ? "RelewiseActionFailed" : "CartFailed");
        }
        finally
        {
            current.Gate.Release();
        }
    }
}
