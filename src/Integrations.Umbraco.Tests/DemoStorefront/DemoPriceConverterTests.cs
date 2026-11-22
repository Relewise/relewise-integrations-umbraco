using Moq;
using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Features.Products;
using Relewise.DemoStorefront.Integration.Converters;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoPriceConverterTests
{
    [Fact]
    public void ReadsEachEnteredCurrencyAndAmountIncludingZero()
    {
        MultiCurrency prices = PriceConverter.Read(Entries(("DKK", 899m), ("eur", 119.95m), ("USD", 0m)))!;

        Assert.NotNull(prices.Values);
        Assert.Equal(3, prices.Values.Count);
        Assert.Contains(prices.Values, price => price.Currency.Value == "DKK" && price.Amount == 899m);
        Assert.Contains(prices.Values, price => price.Currency.Value == "EUR" && price.Amount == 119.95m);
        Assert.Contains(prices.Values, price => price.Currency.Value == "USD" && price.Amount == 0m);
    }

    [Fact]
    public void DuplicateCurrenciesAreRejectedAfterNormalization()
    {
        var error =
            Assert.Throws<InvalidOperationException>(() => PriceConverter.Read(Entries(("EUR", 10m), (" eur ", 20m))));

        Assert.Contains("more than once", error.Message);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("TOO-LONG", 10)]
    [InlineData("EUR", null)]
    public void IncompleteEntriesCannotSilentlyBecomeAZeroPrice(string? currency, int? amount)
    {
        Assert.Throws<InvalidOperationException>(() => PriceConverter.Read(Entries((currency, amount))));
    }

    [Fact]
    public void EmptyAndAbsentFieldsDoNotInventPrices()
    {
        Assert.Null(PriceConverter.Read(null));
        Assert.Empty(PriceConverter.Read(Entries())!.Values ?? []);
        Assert.Throws<InvalidOperationException>(() => PriceConverter.Read("899"));
    }

    [Fact]
    public void ReadingPricesHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            PriceConverter.Read(Entries(("DKK", 899m)), cancellation.Token));
    }

    [Fact]
    public void ProductPriceReadsDoNotSubstituteAnotherCurrencyOrProperty()
    {
        var sales = new Mock<IPublishedProperty>();

        sales.Setup(value => value.GetValue(null, null)).Returns(Entries(("EUR", 10m), ("DKK", 75m)));

        var list = new Mock<IPublishedProperty>();

        list.Setup(value => value.GetValue(null, null)).Returns(Entries(("EUR", 20m), ("USD", 22m)));

        var product = new Mock<IPublishedContent>();

        product.Setup(value => value.GetProperty("salesPrice")).Returns(sales.Object);
        product.Setup(value => value.GetProperty("listPrice")).Returns(list.Object);

        Assert.Equal(75m, ProductPrices.Read(product.Object, "salesPrice", "DKK"));
        Assert.Equal(20m, ProductPrices.Read(product.Object, "listPrice", "EUR"));
        Assert.Null(ProductPrices.Read(product.Object, "salesPrice", "USD"));
        Assert.Null(ProductPrices.Read(product.Object, "missingPrice", "EUR"));
    }

    private static BlockListModel Entries(params (string? Currency, decimal? Amount)[] prices)
    {
        return new BlockListModel(
        [
            .. prices.Select(price =>
            {
                var currency = new Mock<IPublishedProperty>();

                currency.Setup(value => value.GetValue(null, null))
                    .Returns(price.Currency);

                var amount = new Mock<IPublishedProperty>();

                amount.Setup(value => value.GetValue(null, null))
                    .Returns(price.Amount);

                var element = new Mock<IPublishedElement>();

                element.Setup(value => value.GetProperty("currency"))
                    .Returns(currency.Object);
                element.Setup(value => value.GetProperty("amount"))
                    .Returns(amount.Object);

                return new BlockListItem(Guid.NewGuid(), element.Object, null, null);
            })
        ]);
    }
}
