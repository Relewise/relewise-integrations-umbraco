using System.Globalization;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

internal static class CartPricing
{
    public static PricedCartLine[]? Price(
        List<CartLine> lines,
        IPublishedContentCache cache,
        HttpContext http,
        bool availableOnly = false)
    {
        DemoMarket market = DemoMarket.Current(http);
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        var result = new List<PricedCartLine>();

        foreach (CartLine line in lines)
        {
            IPublishedContent? product = cache.GetById(line.ProductKey);
            IPublishedContent? variant = line.VariantKey.HasValue ? cache.GetById(line.VariantKey.Value) : null;

            if (product?.ContentType.Alias != DocumentTypeAliases.Product
                || !product.Cultures.ContainsKey(market.Culture) ||
                access.IsProtected(product.Path).Success ||
                line.Quantity < 1 || line.Quantity > 99 ||
                (line.VariantKey.HasValue && (variant?.ContentType.Alias != DocumentTypeAliases.ProductVariant ||
                                              access.IsProtected(variant.Path).Success ||
                                              variant.Parent()?.Key != product.Key ||
                                              !variant.Cultures.ContainsKey(market.Culture))) ||
                (!line.VariantKey.HasValue &&
                 product.Children().Any(node => node.ContentType.Alias == DocumentTypeAliases.ProductVariant)) ||
                ProductPrices.Read(product, PropertyAliases.SalesPrice, market.Currency) is not { } amount)
            {
                if (availableOnly)
                {
                    continue;
                }

                return null;
            }

            result.Add(new PricedCartLine(line, product, variant, amount));
        }

        return [.. result];
    }

    public static bool InStock(PricedCartLine[] lines)
    {
        return lines.GroupBy(line => line.Product.Key)
            .All(
                group => group.Sum(
                    line => line.Line.Quantity) <= group.First().Product.Value<int>(PropertyAliases.Stock));
    }

    public static string ReviewKey(
        PricedCartLine[] lines,
        HttpContext http)
    {
        return DemoMarket.Current(http).Currency + "|" +
               string.Join(
                   "|",
                   lines.Select(line =>
                       $"{line.Line.ProductKey:D}/{line.Line.VariantKey:D}/{line.Line.Quantity}/" +
                       $"{line.Amount.ToString("G29", CultureInfo.InvariantCulture)}"));
    }
}
