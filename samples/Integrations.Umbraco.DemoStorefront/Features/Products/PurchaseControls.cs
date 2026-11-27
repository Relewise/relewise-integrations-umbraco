using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Relewise.Integrations.Umbraco.DemoStorefront.Setup;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;

internal static class PurchaseControls
{
    public static PurchaseModel Create(IPublishedContent page, IPublishedContent product, HttpContext http)
    {
        var stock = product.Value<int>(PropertyAliases.Stock);
        bool priced = ProductPrices.Read(
            product,
            PropertyAliases.SalesPrice,
            DemoMarket.Current(http).Currency) != null;
        string availability = Storefront.Text(
            http,
            stock <= 0 ? "OutOfStock" : !priced ? "Unavailable" : stock <= 3 ? "LowStock" : "InStock",
            stock);
        string? availableUrl = stock <= 0
            ? (product.Parent()?.ContentType.Alias == DocumentTypeAliases.ProductCategory
                ? product.Parent()!.Url()
                : DemoMarket.Current(http).StoreUrl) + "?available=true"
            : null;
        string? message = !DemoConnection.IsConfigured(http) ? Storefront.Text(http, "ShoppingDisabled") : null;
        bool isVariant = page.ContentType.Alias == DocumentTypeAliases.ProductVariant;
        var fields = new Dictionary<string, string>();
        int maximum = Math.Min(99, stock);
        string? json = null;

        if (message == null && stock > 0 && priced)
        {
            if (!isVariant
                && product.Children().Any(node => node.ContentType.Alias == DocumentTypeAliases.ProductVariant))
            {
                message = Storefront.Text(http, "SelectVariant");
            }
            else
            {
                AntiforgeryTokenSet tokens = http.RequestServices.GetRequiredService<IAntiforgery>()
                    .GetAndStoreTokens(http);
                fields[tokens.FormFieldName] = tokens.RequestToken ?? "";
                fields["product"] = product.Key.ToString();
                fields["variant"] = isVariant ? page.Key.ToString() : "";
                string[] keys = ["Quantity", "AddCart", "AddingToCart", "PurchaseFailed", "CartUpdated"];
                json = JsonSerializer.Serialize(
                    new
                    {
                        url = DemoMarket.Current(http).LanguagePath + "/cart/add",
                        maximum,
                        fields,
                        labels = keys.ToDictionary(key => key, key => Storefront.Text(http, key))
                    },
                    StorefrontChrome.Json);
            }
        }

        return new PurchaseModel(availability, availableUrl, message, maximum, fields, json);
    }
}
