using System.Text.Json;
using Relewise.DemoStorefront.Integration.Converters;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoPriceConversionTests
{
    [Fact]
    public void DuplicateCurrencyEntriesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => StoredPriceReader.Read(Prices(("EUR", 40m), ("eur", 50m))));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.001)]
    public void InvalidDemoPricePrecisionIsRejectedByConversionAndStoredPriceReading(double amount)
    {
        var value = (decimal)amount;

        Assert.Throws<InvalidOperationException>(() => PriceConverter.ValidateAmount(value));
        Assert.Throws<InvalidOperationException>(() => StoredPriceReader.Read(Prices(("EUR", value))));
    }

    private static string Prices(params (string Currency, decimal Amount)[] values)
    {
        return JsonSerializer.Serialize(
            new
            {
                contentData = values.Select(value => new
                {
                    values = new object[]
                    {
                        new
                        {
                            alias = "currency",
                            value = (object)JsonSerializer.Serialize(new[] { value.Currency })
                        },
                        new { alias = "amount", value = (object)value.Amount }
                    }
                })
            });
    }
}
