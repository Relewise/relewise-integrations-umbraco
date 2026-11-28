using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.DemoStorefront.Integration.Mapping;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Tests;

public class DemoProductMapperTests
{
    [Fact]
    public void BusinessFlagsAreWrittenDirectlyToTheSdkProductUsingItsMappedPrices()
    {
        var product = new Product("sku")
        {
            ListPrice = new MultiCurrency(new Money("EUR", 100), new Money("DKK", 800), new Money("USD", 120)),
            SalesPrice = new MultiCurrency(new Money("EUR", 80), new Money("DKK", 800), new Money("USD", 90))
        };

        DemoProductMapper.Apply(product, 2);

        Assert.Equal(true, product.Data!["inStock"]!.Value);
        Assert.Equal(true, product.Data!["onSale_EUR"]!.Value);
        Assert.Equal(false, product.Data!["onSale_DKK"]!.Value);
        Assert.Equal(true, product.Data!["onSale_USD"]!.Value);
        Assert.DoesNotContain("onSaleCurrencies", product.Data.Keys);
        Assert.Equal(80m, product.SalesPrice!.Values![0].Amount);
        Assert.Equal("sku", product.Id);
    }

    [Fact]
    public void MissingListPricesDoNotCreateDiscountsAndOldFlagsAreReplaced()
    {
        var product = new Product("sku")
        {
            SalesPrice = new MultiCurrency(new Money("EUR", 80)),
            Data = new Dictionary<string, DataValue?>
            { ["inStock"] = true, ["onSale_EUR"] = true, ["onSale_DKK"] = true, ["onSale_USD"] = true }
        };

        DemoProductMapper.Apply(product, 0);
        Assert.Equal(false, product.Data["inStock"]!.Value);
        Assert.Equal(false, product.Data["onSale_EUR"]!.Value);
        Assert.Equal(false, product.Data["onSale_DKK"]!.Value);
        Assert.Equal(false, product.Data["onSale_USD"]!.Value);
    }

    [Fact]
    public void RemovingADiscountOrAnEntireCurrencyExplicitlyWritesFalse()
    {
        var product = new Product("sku")
        {
            ListPrice = new MultiCurrency(new Money("EUR", 100), new Money("DKK", 800)),
            SalesPrice = new MultiCurrency(new Money("EUR", 80), new Money("DKK", 700))
        };

        DemoProductMapper.Apply(product, 1);
        product.SalesPrice = new MultiCurrency(new Money("EUR", 100));
        DemoProductMapper.Apply(product, 1);
        Assert.Equal(false, product.Data!["onSale_EUR"]!.Value);
        Assert.Equal(false, product.Data["onSale_DKK"]!.Value);
        Assert.Equal(false, product.Data["onSale_USD"]!.Value);
    }
}
