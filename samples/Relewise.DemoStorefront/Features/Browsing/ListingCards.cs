using Relewise.DemoStorefront.Features.Products;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.Site.SiteContent;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class ListingCards
{
    internal static ListingCard CardData(
        IPublishedContent page,
        HttpContext http)
    {
        bool product = page.ContentType.Alias == "product";
        string image = page.ContentType.Alias is "product" or "article" &&
                       !string.IsNullOrWhiteSpace(page.Value<string>("heroImage"))
            ? Storefront.Image(page)
            : Storefront.Icon();
        (string Sale, string? List, string? Savings) prices =
            product ? ProductPrices.Display(page, http) : (Sale: "", List: null, Savings: null);
        var stock = page.Value<int>("stock");
        string[] options = product
            ? page.Children()
                .Where(node => node.ContentType.Alias == "productVariant")
                .Select(ProductOptions.Label)
                .Distinct()
                .ToArray()
            : [];

        return new ListingCard(
            page.Key,
            DemoConfiguration.EntityId(page, http.RequestServices),
            page.Url(),
            Title(page),
            page.Value<string>("bodyText") ?? "",
            image,
            product,
            prices.Sale,
            prices.List,
            product
                ? Storefront.Text(http, stock <= 0 ? "OutOfStock" : stock <= 3 ? "LowStock" : "InStock", stock)
                : "",
            options.Length > 0 ? Storefront.Text(http, "AvailableOptions", string.Join(", ", options)) : null,
            prices.Savings,
            product && page.Value<IPublishedContent>("brand") is { } brand ? Title(brand) : null
        );
    }
}
