namespace Relewise.Integrations.Umbraco.Tests;

public class DemoSavingsTests
{
    [Theory]
    [InlineData(40, 50, 20)]
    [InlineData(34.95, 39.95, 12)]
    [InlineData(99.99, 100, 0)]
    [InlineData(0, 50, 100)]
    public void SavingsUseActualPricesWithoutOverstatingThePercentage(decimal salesPrice, decimal listPrice, int expected)
    {
        Assert.Equal(expected, StorefrontPages.DiscountPercentage(salesPrice, listPrice));
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(40, null)]
    [InlineData(40, 40)]
    [InlineData(50, 40)]
    [InlineData(0, 0)]
    [InlineData(-1, 50)]
    public void MissingOrNonDiscountedPricesDoNotProduceSavings(int? salesPrice, int? listPrice)
    {
        Assert.Null(StorefrontPages.DiscountPercentage((decimal?)salesPrice, (decimal?)listPrice));
    }
}
