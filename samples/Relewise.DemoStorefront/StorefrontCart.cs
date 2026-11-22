using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Caching.Memory;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Relewise.DemoStorefront;

internal static class StorefrontCart
{
    private const string SessionMarkerKey = "fieldwork-shopping";
    // A session identifies the basket, but its per-request snapshot cannot serialize concurrent writes.
    private static readonly object StateCreation = new();

    internal static void MapCart(this WebApplication app)
    {
        app.MapGet("/{language:regex(^(en|da)$)}/cart/state", async Task<IResult> (HttpContext http) =>
        {
            if (DemoConnection.RequireConnection(http, json: true) is { } blocked)
            {
                return blocked;
            }
            http.Response.Headers.CacheControl = "no-store";
            await http.Session.LoadAsync(http.RequestAborted);
            Basket basket = State(http);
            await basket.Gate.WaitAsync(http.RequestAborted);
            try
            {
                using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
                SetCulture(http);
                List<Line> lines = Read(http);
                PricedLine[] priced = Price(lines, context.UmbracoContext.Content!, http, availableOnly: true)!;
                return Results.Json(Model(http, lines, priced));
            }
            finally
            {
                basket.Gate.Release();
            }
        });
        app.MapGet("/{language:regex(^(en|da)$)}/cart", async Task<IResult> (HttpContext http) => await Page(http, false));
        app.MapGet("/{language:regex(^(en|da)$)}/checkout", async Task<IResult> (HttpContext http) => await Page(http, true));
        app.MapGet("/{language:regex(^(en|da)$)}/confirmation", Confirmation);
        app.MapPost("/{language:regex(^(en|da)$)}/cart/{action:regex(^(add|update|clear)$)}", (HttpContext http, string action) => Mutate(http, action));
        app.MapPost("/{language:regex(^(en|da)$)}/checkout", async Task<IResult> (HttpContext http) => await Mutate(http, "checkout"));
    }

    internal static async Task ChangeMarket(HttpContext http)
    {
        if (!DemoConnection.IsConfigured(http))
        {
            return;
        }
        await http.Session.LoadAsync(http.RequestAborted);
        Basket current = State(http);
        await current.Gate.WaitAsync(http.RequestAborted);
        try
        {
            current.CheckoutToken = null;
            current.CheckoutKey = null;
            List<Line> lines = Read(http);
            if (lines.Count == 0)
            {
                return;
            }

            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
            SetCulture(http);
            PricedLine[]? priced = Price(lines, context.UmbracoContext.Content!, http, availableOnly: true);
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

    internal static int Count(HttpContext http)
    {
        return Read(http).Sum(line => line.Quantity);
    }

    internal static string Availability(IPublishedContent product, HttpContext http)
    {
        int stock = product.Value<int>("stock");
        if (stock <= 0)
        {
            string path = product.Parent()?.ContentType.Alias == "productCategory" ? product.Parent()!.Url() : DemoMarket.Current(http).StoreUrl;
            return $"<p class='availability sold-out'>{T(http, "OutOfStock")}</p><a class='text-link' href='{Storefront.E(path)}?available=true'>{T(http, "ShopAvailable")}</a>";
        }
        if (UnitPrice(product, http) == null)
        {
            return $"<p class='availability'>{T(http, "Unavailable")}</p>";
        }
        return $"<p class='availability'>{(stock <= 3 ? Storefront.E(Storefront.Text(http, "LowStock", stock)) : T(http, "InStock"))}</p>";
    }

    internal static string AddForm(IPublishedContent page, HttpContext http)
    {
        if (!DemoConnection.IsConfigured(http))
        {
            return $"<p class='muted'>{T(http, "ShoppingDisabled")}</p>";
        }
        bool isVariant = page.ContentType.Alias == "productVariant";
        IPublishedContent product = isVariant ? page.Parent()! : page;
        int stock = product.Value<int>("stock");
        if (stock <= 0 || UnitPrice(product, http) == null)
        {
            return string.Empty;
        }
        if (!isVariant && product.Children().Any(node => node.ContentType.Alias == "productVariant"))
        {
            return $"<p class='muted'>{T(http, "SelectVariant")}</p>";
        }

        AntiforgeryTokenSet tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var fields = new Dictionary<string, string>
        {
            [tokens.FormFieldName] = tokens.RequestToken ?? "",
            ["product"] = product.Key.ToString(),
            ["variant"] = isVariant ? page.Key.ToString() : ""
        };
        string[] keys = ["Quantity", "AddCart", "AddingToCart", "PurchaseFailed", "CartUpdated"];
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            url = DemoMarket.Current(http).LanguagePath + "/cart/add",
            maximum = Math.Min(99, stock),
            fields,
            labels = keys.ToDictionary(key => key, key => Storefront.Text(http, key))
        }, StorefrontChrome.Json);
        return $"<div id='purchase-controls'><form method='post' action='{DemoMarket.Current(http).LanguagePath}/cart/add' class='add-cart'>" +
            string.Concat(fields.Select(field => Hidden(field.Key, field.Value))) +
            $"<label>{T(http, "Quantity")}<input type='number' name='quantity' min='1' max='{Math.Min(99, stock)}' value='1' required></label><button>{T(http, "AddCart")}</button></form></div><script id='purchase-data' type='application/json'>{json}</script>";
    }

    private static async Task<IResult> Mutate(HttpContext http, string action)
    {
        if (DemoConnection.RequireConnection(http, WantsJson(http)) is { } blocked)
        {
            return blocked;
        }
        try
        {
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Error(http, "CartInvalid");
        }

        http.Response.Headers.CacheControl = "no-store";
        await http.Session.LoadAsync(http.RequestAborted);
        Basket current = State(http);
        await current.Gate.WaitAsync(http.RequestAborted);
        try
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
            SetCulture(http);
            IFormCollection form = await http.Request.ReadFormAsync(http.RequestAborted);
            List<Line> lines = Read(http);
            Guid commandId = Guid.Empty;
            string? signature = null;
            if (action == "checkout")
            {
                string? token = State(http).CheckoutToken;
                Receipt? previous = ReadReceipt(http);
                if (previous != null && previous.CheckoutToken == form["checkoutToken"].ToString())
                {
                    return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/confirmation");
                }

                if (string.IsNullOrEmpty(token) || token != form["checkoutToken"] || lines.Count == 0)
                {
                    return Error(http, "CartInvalid");
                }

                PricedLine[]? priced = Price(lines, context.UmbracoContext.Content!, http);
                if (priced == null || !InStock(priced) || current.CheckoutKey != ReviewKey(priced, http))
                {
                    return Error(http, "CartUnavailable");
                }

                string orderNumber = "FW-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
                User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUser();
                decimal total = priced.Sum(line => line.Amount * line.Line.Quantity);
                string currency = DemoMarket.Current(http).Currency;
                ITracker tracker = RequiredTracker(http);
                Trackable[] events =
                [
                    new Order(user, new Money(currency, total), orderNumber, Items(priced, http)),
                    new Cart(user, new Money(currency, 0m), Array.Empty<LineItem>())
                ];
                await tracker.TrackAsync(events, http.RequestAborted);

                var receipt = new Receipt(orderNumber, currency, total, priced.Select(line =>
                    new PurchasedLine(
                        StorefrontPages.Title(line.Variant ?? line.Product),
                        line.Line.Quantity,
                        line.Amount * line.Line.Quantity
                    )).ToArray(), token);
                Basket state = State(http);
                state.Receipt = receipt;
                state.Lines = [];
                state.CheckoutToken = null;
                state.CheckoutKey = null;
                return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/confirmation");
            }

            if (action == "clear")
            {
                lines.Clear();
            }
            else
            {
                bool remove = form["remove"] == "true";
                int quantity = 0;
                if (!Guid.TryParse(form["product"], out Guid productKey) ||
                    !remove && (!int.TryParse(form["quantity"], out quantity) || quantity < 0 || quantity > 99))
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
                        PricedLine[] snapshot = Price(
                            lines,
                            context.UmbracoContext.Content!,
                            http,
                            availableOnly: true
                        )!;
                        PricedLine? item = snapshot.SingleOrDefault(value => value.Line.ProductKey == productKey && value.Line.VariantKey == variantKey);
                        CartLineModel? addedLine = item == null ? null : LineModel(item.Line, item, http, quantity);
                        return Results.Json(new CartMutationResult(Model(http, lines, snapshot), addedLine));
                    }
                }

                Line? existing = lines.SingleOrDefault(line => line.ProductKey == productKey && line.VariantKey == variantKey);

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
                    lines.Add(new Line(productKey, variantKey, quantity));
                }
            }

            bool removing = action == "clear" || form["remove"] == "true" || action == "update" && form["quantity"] == "0";
            PricedLine[]? updated = Price(lines, context.UmbracoContext.Content!, http, availableOnly: removing);
            if (updated == null)
            {
                return Error(http, "CartUnavailable");
            }
            if (!removing && !InStock(updated))
            {
                IGrouping<Guid, PricedLine> exceeded = updated.GroupBy(line => line.Product.Key)
                    .First(group => group.Sum(line => line.Line.Quantity) > group.First().Product.Value<int>("stock"));
                IPublishedContent product = exceeded.First().Product;
                string stock = Math.Max(0, product.Value<int>("stock")).ToString(CultureInfo.InvariantCulture);
                return Error(http, "CartStockLimit", product.Name, stock);
            }

            await TrackCart(updated, http);

            Basket basket = State(http);
            basket.Lines = lines.ToArray();
            basket.CheckoutToken = null;
            basket.CheckoutKey = null;
            if (commandId != Guid.Empty)
            {
                if (basket.Additions.Count >= 16)
                {
                    basket.Additions.Remove(basket.Additions.Keys.First());
                }
                basket.Additions[commandId] = signature!;
            }
            if (WantsJson(http))
            {
                PricedLine? added = action == "add" ? updated.Single(value =>
                    value.Line.ProductKey == Guid.Parse(form["product"].ToString()) &&
                    value.Line.VariantKey == (Guid.TryParse(form["variant"], out Guid key) ? key : null)) : null;
                CartLineModel? addedLine = added == null ? null : LineModel(
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
                PricedLine added = updated.Single(line => line.Line.ProductKey == addedProduct && line.Line.VariantKey == addedVariant);
                IPublishedContent page = added.Variant ?? added.Product;
                StorefrontNavigation.Notify(http, "CartAdded", false, StorefrontPages.Title(page));
                return Results.LocalRedirect(page.Url(DemoMarket.Current(http).Culture));
            }
            StorefrontNavigation.Notify(http, action == "clear" ? "CartCleared" : "CartUpdated");
            return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("StorefrontCart").LogError(error, "Unable to complete the demo cart action.");
            return Error(http, DemoConnection.IsConnectionFailure(error, http.RequestAborted) ? "RelewiseActionFailed" : "CartFailed");
        }
        finally
        {
            current.Gate.Release();
        }
    }

    private static async Task<IResult> Page(HttpContext http, bool checkout)
    {
        if (DemoConnection.RequireConnection(http) is { } blocked)
        {
            return blocked;
        }
        await http.Session.LoadAsync(http.RequestAborted);
        Basket basket = State(http);
        await basket.Gate.WaitAsync(http.RequestAborted);
        try
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
            SetCulture(http);
            string key = checkout ? "Checkout" : "Cart";
            if (checkout)
            {
                http.Items["Fieldwork:Checkout"] = true;
            }
            var html = new StringBuilder();
            if (!checkout)
            {
                html.Append(StorefrontPages.Breadcrumb(null, http, "shop", Storefront.Text(http, key)));
            }
            string heading = T(http, checkout ? "ReviewOrder" : "Cart");
            string introduction = checkout ? $"<p>{T(http, "ReviewIntro")}</p>" : string.Empty;
            html.Append($"<section class='hero'><h1>{heading}</h1>{introduction}</section>");

            List<Line> lines = Read(http);
            PricedLine[] priced = Price(lines, context.UmbracoContext.Content!, http, availableOnly: true)!;
            if (!checkout)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(Model(http, lines, priced), StorefrontChrome.Json);
                html.Append($"<div id='cart-page'></div><script id='cart-data' type='application/json'>{json}</script>");
            }
            else if (lines.Count == 0)
            {
                html.Append($"<p class='empty'>{T(http, "CartEmpty")}</p><a class='button' href='{Storefront.E(StorefrontNavigation.ShoppingUrl(http))}'>{T(http, "ContinueShopping")}</a>");
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

    private static void AppendCheckout(StringBuilder html, List<Line> lines, PricedLine[] priced, Basket basket, HttpContext http)
    {
        string path = DemoMarket.Current(http).LanguagePath;
        html.Append($$"""
            <ol class="checkout-steps">
                <li><a href="{{path}}/cart">{{T(http, "Cart")}}</a></li>
                <li aria-current="step">{{T(http, "ReviewOrder")}}</li>
                <li>{{T(http, "Confirmation")}}</li>
            </ol>
            <section class="checkout-review">
                <div class="checkout-items"><h2>{{T(http, "CartItems")}}</h2>
            """);
        foreach (Line line in lines)
        {
            PricedLine? item = priced.SingleOrDefault(value => value.Line == line);
            if (item == null)
            {
                html.Append($"<p role='alert'>{T(http, "UnavailableItem")}</p>");
                continue;
            }

            IPublishedContent page = item.Variant ?? item.Product;
            html.Append($$"""
                <div class="review-item">
                    <div class="illustration">{{Storefront.Image(page)}}</div>
                    <span>{{Storefront.E(StorefrontPages.Title(page))}}<small>{{T(http, "Quantity")}}: {{line.Quantity}}</small></span>
                    <strong>{{Money(http, item.Amount * line.Quantity)}}</strong>
                </div>
                """);
        }
        decimal total = priced.Sum(line => line.Amount * line.Line.Quantity);
        html.Append($$"""
                </div>
                <aside class="checkout-summary">
                    <h2>{{T(http, "OrderSummary")}}</h2>
                    <div class="cart-total"><span>{{T(http, "Total")}}</span><strong>{{Money(http, total)}}</strong></div>
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
            html.Append($$"""
                <form method="post" action="{{path}}/checkout">
                    {{Token(http)}}{{Hidden("checkoutToken", basket.CheckoutToken)}}
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

    private static string ReviewKey(PricedLine[] lines, HttpContext http) => DemoMarket.Current(http).Currency + "|" +
        string.Join("|", lines.Select(line => $"{line.Line.ProductKey:D}/{line.Line.VariantKey:D}/{line.Line.Quantity}/{line.Amount.ToString("G29", CultureInfo.InvariantCulture)}"));

    private static IResult Confirmation(HttpContext http)
    {
        if (DemoConnection.RequireConnection(http) is { } blocked)
        {
            return blocked;
        }
        Receipt? receipt = ReadReceipt(http);
        if (receipt == null)
        {
            return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
        }

        http.Items["Fieldwork:Checkout"] = true;
        var html = new StringBuilder();
        html.Append($$"""
            <section class="hero">
                <p class="eyebrow">{{T(http, "DemoOrder")}}</p>
                <h1>{{T(http, "ThankYou")}}</h1>
                <p>{{T(http, "OrderConfirmed")}}</p>
                <p>{{T(http, "OrderNumber")}}: <strong>{{Storefront.E(receipt.OrderNumber)}}</strong></p>
            </section>
            <div class="order-summary">
            """);
        foreach (PurchasedLine line in receipt.Lines)
        {
            html.Append($"<p>{Storefront.E(line.Name)} × {line.Quantity}<strong>{Money(http, line.Total, receipt.Currency)}</strong></p>");
        }
        html.Append($$"""
                <div class="cart-total"><span>{{T(http, "Total")}}</span><strong>{{Money(http, receipt.Total, receipt.Currency)}}</strong></div>
            </div>
            <a class="button" href="{{Storefront.E(StorefrontNavigation.ShoppingUrl(http))}}">{{T(http, "ContinueShopping")}}</a>
            """);
        return Html(http, "Confirmation", html.ToString());
    }

    private static PricedLine[]? Price(List<Line> lines, Umbraco.Cms.Core.PublishedCache.IPublishedContentCache cache, HttpContext http, bool availableOnly = false)
    {
        var result = new List<PricedLine>();
        foreach (Line line in lines)
        {
            IPublishedContent? product = cache.GetById(line.ProductKey);
            IPublishedContent? variant = line.VariantKey.HasValue ? cache.GetById(line.VariantKey.Value) : null;
            if (product?.ContentType.Alias != "product" || !product.Cultures.ContainsKey(DemoMarket.Current(http).Culture) ||
                line.Quantity < 1 || line.Quantity > 99 ||
                line.VariantKey.HasValue && (variant?.ContentType.Alias != "productVariant" || variant.Parent()?.Key != product.Key ||
                    !variant.Cultures.ContainsKey(DemoMarket.Current(http).Culture)) ||
                !line.VariantKey.HasValue && product.Children().Any(node => node.ContentType.Alias == "productVariant") ||
                UnitPrice(product, http) is not decimal amount)
            {
                if (availableOnly)
                {
                    continue;
                }

                return null;
            }

            result.Add(new PricedLine(line, product, variant, amount));
        }

        return result.ToArray();
    }

    private static bool InStock(PricedLine[] lines) => lines.GroupBy(line => line.Product.Key)
        .All(group => group.Sum(line => line.Line.Quantity) <= group.First().Product.Value<int>("stock"));

    private static async Task TrackCart(PricedLine[] lines, HttpContext http)
    {
        ITracker tracker = RequiredTracker(http);
        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUser();
        await tracker.TrackAsync(new Cart(user, new Money(DemoMarket.Current(http).Currency,
            lines.Sum(line => line.Amount * line.Line.Quantity)), Items(lines, http)), http.RequestAborted);
    }

    private static ITracker RequiredTracker(HttpContext http) => http.RequestServices.GetRequiredService<IExportContentService>().GetTrackerOrNull()
        ?? throw new InvalidOperationException("The demo requires a configured Relewise Tracker for shopping actions.");

    private static LineItem[] Items(PricedLine[] lines, HttpContext http) => lines.Select(line => new LineItem(
        DemoConfiguration.EntityId(line.Product, http.RequestServices),
        line.Variant == null ? null : DemoConfiguration.EntityId(line.Variant, http.RequestServices),
        line.Line.Quantity,
        line.Amount * line.Line.Quantity
    )).ToArray();

    private static decimal? UnitPrice(IPublishedContent product, HttpContext http) =>
        PriceConverter.Read(product.GetProperty("salesPrice")?.GetValue())?.Values?
            .SingleOrDefault(price => price.Currency.Value == DemoMarket.Current(http).Currency)?.Amount;

    internal static string Token(HttpContext http)
    {
        AntiforgeryTokenSet token = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        return Hidden(token.FormFieldName, token.RequestToken ?? "");
    }

    private static string Hidden(string name, string value) => $"<input type='hidden' name='{Storefront.E(name)}' value='{Storefront.E(value)}'>";
    private static Basket State(HttpContext http)
    {
        // Establish the session for both page renders and clients starting at the JSON endpoint.
        if (http.Session.GetString(SessionMarkerKey) == null)
        {
            http.Session.SetString(SessionMarkerKey, "active");
        }
        string key = "fieldwork-basket-" + http.Session.Id;
        IMemoryCache cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        lock (StateCreation)
        {
            return cache.GetOrCreate(key, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(8);
                return new Basket();
            })!;
        }
    }

    private static List<Line> Read(HttpContext http) => State(http).Lines.ToList();
    private static Receipt? ReadReceipt(HttpContext http) => State(http).Receipt;
    private static void SetCulture(HttpContext http) => http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(DemoMarket.Current(http).Culture);
    private static string T(HttpContext http, string key) => Storefront.E(Storefront.Text(http, key));
    private static string Money(HttpContext http, decimal amount, string? currency = null) => Storefront.E((currency ?? DemoMarket.Current(http).Currency) + " " + amount.ToString("N2", CultureInfo.GetCultureInfo(DemoMarket.Current(http).Culture)));
    private static IResult Error(HttpContext http, string key, params string[] values)
    {
        if (WantsJson(http))
        {
            int status = key is "RelewiseActionFailed" or "CartFailed" ? 503 : key is "CartStockLimit" or "CartUnavailable" ? 409 : 400;
            return Results.Json(new
            {
                error = Storefront.Text(http, key, values.Cast<object>().ToArray())
            }, statusCode: status
            );
        }
        StorefrontNavigation.Notify(http, key, true, values);
        return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
    }

    private static bool WantsJson(HttpContext http) => http.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private static CartLineModel LineModel(Line line, PricedLine? priced, HttpContext http, int? quantity = null)
    {
        IPublishedContent? page = priced?.Variant ?? priced?.Product;
        int amount = quantity ?? line.Quantity;
        return new CartLineModel
        {
            ProductKey = line.ProductKey,
            VariantKey = line.VariantKey,
            ProductId = priced == null ? "" : DemoConfiguration.EntityId(priced.Product, http.RequestServices),
            VariantId = priced?.Variant == null ? null : DemoConfiguration.EntityId(priced.Variant, http.RequestServices),
            Name = page == null ? Storefront.Text(http, "UnavailableItem") : StorefrontPages.Title(page),
            Url = page?.Url() ?? "",
            ImageHtml = page == null ? "" : Storefront.Image(page),
            Quantity = amount,
            Maximum = priced == null ? 0 : Math.Max(0, Math.Min(99, priced.Product.Value<int>("stock"))),
            UnitPrice = priced == null ? "" : System.Net.WebUtility.HtmlDecode(Money(http, priced.Amount)),
            LineTotal = priced == null ? "" : System.Net.WebUtility.HtmlDecode(Money(http, priced.Amount * amount)),
            Available = priced != null
        };
    }

    private static CartModel Model(HttpContext http, List<Line> lines, PricedLine[] priced)
    {
        string path = DemoMarket.Current(http).LanguagePath;
        AntiforgeryTokenSet token = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        string[] labels = ["CartIntro", "EmptyCartHeading", "EmptyCartDescription", "ExploreCollection", "BrowseOffers", "CartEmpty", "ContinueShopping", "CartItems", "Quantity", "Each", "Total", "OrderSummary", "Checkout", "RemoveCart", "ChangeQuantity",
            "EmptyCartAction", "EmptyCartConfirm", "Cancel", "CartUpdating", "CartUpdated", "CartUnavailable", "UnavailableItem", "UnavailableTotal", "PurchaseFailed", "Close"];
        return new CartModel
        {
            Lines = lines.Select(line => LineModel(line, priced.SingleOrDefault(item => item.Line == line), http)).ToArray(),
            Labels = labels.ToDictionary(key => key, key => Storefront.Text(http, key)),
            CartUrl = path + "/cart",
            CheckoutUrl = path + "/checkout",
            ContinueUrl = StorefrontNavigation.ShoppingUrl(http),
            ShopUrl = path + "/store",
            OffersUrl = path + "/offers",
            Recommendations = StorefrontRecommendations.Configuration(
                StorefrontPages.Home(http.RequestServices),
                http,
                emptyCart: true
            ),
            AntiforgeryField = token.FormFieldName,
            AntiforgeryToken = token.RequestToken ?? "",
            Count = lines.Sum(line => line.Quantity),
            Total = System.Net.WebUtility.HtmlDecode(Money(http, priced.Sum(line => line.Amount * line.Line.Quantity))),
            Ready = lines.Count > 0 && priced.Length == lines.Count && InStock(priced)
        };
    }

    private static IResult Html(HttpContext http, string title, string body, int status = 200) => Results.Content(
        Storefront.Layout(
            Storefront.Text(http, title),
            body,
            http.RequestServices.GetService<RelewiseUmbracoConfiguration>() != null,
            http,
            "cart"
        ), "text/html", statusCode: status
    );

    private sealed class Basket
    {
        internal Dictionary<Guid, string> Additions { get; } = [];
        internal SemaphoreSlim Gate { get; } = new(1, 1);
        internal string? CheckoutKey { get; set; }
        internal Line[] Lines { get; set; } = [];
        internal Receipt? Receipt { get; set; }
        internal string? CheckoutToken { get; set; }
    }

    private sealed record Line(Guid ProductKey, Guid? VariantKey, int Quantity);
    private sealed record PricedLine(Line Line, IPublishedContent Product, IPublishedContent? Variant, decimal Amount);
    private sealed record PurchasedLine(string Name, int Quantity, decimal Total);
    private sealed record Receipt(string OrderNumber, string Currency, decimal Total, PurchasedLine[] Lines, string CheckoutToken);
}
