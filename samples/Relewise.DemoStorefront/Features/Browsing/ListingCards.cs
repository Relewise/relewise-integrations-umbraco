using Relewise.DemoStorefront.Features.Products;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.DemoConstants;
using static Relewise.DemoStorefront.Site.SiteContent;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class ListingCards
{
    public static ListingCard CardData(
        IPublishedContent page,
        HttpContext http)
    {
        bool product = page.ContentType.Alias == DocumentTypeAliases.Product;
        string image = page.ContentType.Alias is DocumentTypeAliases.Product or DocumentTypeAliases.Article &&
                       !string.IsNullOrWhiteSpace(page.Value<string>(PropertyAliases.HeroImage))
            ? Storefront.Image(page)
            : Storefront.Icon();
        (string Sale, string? List, string? Savings) prices =
            product ? ProductPrices.Display(page, http) : (Sale: "", List: null, Savings: null);
        var stock = page.Value<int>(PropertyAliases.Stock);
        string[] options = product
            ? page.Children()
                .Where(node => node.ContentType.Alias == DocumentTypeAliases.ProductVariant)
                .Select(ProductOptions.Label)
                .Distinct()
                .ToArray()
            : [];

        return new ListingCard(
            page.Key,
            DemoConfiguration.EntityId(page, http.RequestServices),
            page.Url(),
            Title(page),
            page.Value<string>(PropertyAliases.BodyText) ?? "",
            image,
            product,
            prices.Sale,
            prices.List,
            product
                ? Storefront.Text(http, stock <= 0 ? "OutOfStock" : stock <= 3 ? "LowStock" : "InStock", stock)
                : "",
            options.Length > 0 ? Storefront.Text(http, "AvailableOptions", string.Join(", ", options)) : null,
            prices.Savings,
            product && page.Value<IPublishedContent>(PropertyAliases.Brand) is { } brand ? Title(brand) : null
        );
    }
}
