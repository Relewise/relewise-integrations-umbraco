using Microsoft.AspNetCore.Antiforgery;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Products;

internal static class PurchaseControls
{
    internal static string Availability(IPublishedContent product, HttpContext http)
    {
        int stock = product.Value<int>("stock");
        if (stock <= 0)
        {
            string path = product.Parent()?.ContentType.Alias == "productCategory" ? product.Parent()!.Url() : DemoMarket.Current(http).StoreUrl;
            return $"<p class='availability sold-out'>{T(http, "OutOfStock")}</p><a class='text-link' href='{Storefront.E(path)}?available=true'>{T(http, "ShopAvailable")}</a>";
        }
        if (ProductPrices.Read(product, "salesPrice", DemoMarket.Current(http).Currency) == null)
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
        if (stock <= 0 || ProductPrices.Read(product, "salesPrice", DemoMarket.Current(http).Currency) == null)
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
            string.Concat(fields.Select(field => FormFields.Hidden(field.Key, field.Value))) +
            $"<label>{T(http, "Quantity")}<input type='number' name='quantity' min='1' max='{Math.Min(99, stock)}' value='1' required></label><button>{T(http, "AddCart")}</button></form></div><script id='purchase-data' type='application/json'>{json}</script>";
    }
    private static string T(HttpContext http, string key) => Storefront.E(Storefront.Text(http, key));
}
