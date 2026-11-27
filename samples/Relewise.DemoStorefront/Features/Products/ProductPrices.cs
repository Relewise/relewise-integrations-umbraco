using System.Globalization;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Features.Products;

internal static class ProductPrices
{
    public static (string Sale, string? List, string? Savings) Display(
        IPublishedContent product,
        HttpContext http)
    {
        DemoMarket market = DemoMarket.Current(http);

        string Amount(decimal value)
        {
            return market.Currency + " " +
                   value.ToString(
                       "N2",
                       CultureInfo.GetCultureInfo(market.Culture));
        }

        decimal? sale = Read(product, PropertyAliases.SalesPrice, market.Currency);
        decimal? list = Read(product, PropertyAliases.ListPrice, market.Currency);
        int? percent = DiscountPercentage(sale, list);
        string? savings = null;

        if (percent.HasValue)
        {
            savings = percent.Value > 0
                ? Storefront.Text(http, "SavePercent", percent.Value)
                : Storefront.Text(http, "SaveAmount", Amount(list!.Value - sale!.Value));
        }

        return (sale.HasValue ? Amount(sale.Value) : Storefront.Text(http, "PriceUnavailable", market.Currency),
            percent.HasValue ? Amount(list!.Value) : null, savings);
    }

    private static int? DiscountPercentage(decimal? salesPrice, decimal? listPrice)
    {
        return salesPrice is >= 0 && listPrice > salesPrice && listPrice > 0
            ? (int)decimal.Floor((listPrice.Value - salesPrice.Value) / listPrice.Value * 100)
            : null;
    }

    public static decimal? Read(IPublishedContent product, string propertyAlias, string currency)
    {
        return PriceConverter.Read(product.GetProperty(propertyAlias)?.GetValue())
            ?.Values?
            .SingleOrDefault(price => price.Currency.Value == currency)
            ?.Amount;
    }
}
