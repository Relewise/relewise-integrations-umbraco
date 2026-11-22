using Moq;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Integration.Indexing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoProductIndexingTests
{
    [Fact]
    public async Task PublishedPricesAndRelationshipsIgnoreDraftEdits()
    {
        var type = new Mock<ISimpleContentType>();
        type.SetupGet(value => value.Alias).Returns("product");
        var product = new Mock<IContent>();
        product.SetupGet(value => value.ContentType).Returns(type.Object);
        product.SetupGet(value => value.ParentId).Returns(42);
        product.Setup(value => value.GetValue<string>("salesPrice", null, null, true)).Returns(Prices(("EUR", 40m), ("DKK", 400m)));
        product.Setup(value => value.GetValue<string>("salesPrice", null, null, false)).Returns(Prices(("EUR", 999m)));
        Guid brand = Guid.NewGuid();
        product.Setup(value => value.GetValue<string>("brand", null, null, true)).Returns(Udi.Create(global::Umbraco.Cms.Core.Constants.UdiEntityType.Document, brand).ToString());
        Guid category = Guid.NewGuid();
        var identities = new Mock<IIdKeyMap>();
        identities.Setup(value => value.GetKeyForId(42, UmbracoObjectTypes.Document)).Returns(Attempt.Succeed(category));

        var fields = (await new StorefrontProductIndexer(identities.Object)
            .GetIndexFieldsAsync(product.Object, ["en-US", "da-DK"], true)).ToDictionary(field => field.FieldName);

        Assert.Equal(new[] { 40m }, fields["shopPriceEUR"].Value.Decimals);
        Assert.DoesNotContain(fields.Keys, key => key.StartsWith("shopOffer", StringComparison.Ordinal));
        Assert.DoesNotContain(fields.Keys, key => key.StartsWith("onSale", StringComparison.Ordinal));
        Assert.DoesNotContain("shopAvailable", fields.Keys);
        Assert.DoesNotContain("inStock", fields.Keys);
        Assert.Contains(category.ToString("D"), fields["shopCategory"].Value.Keywords!);
        Assert.Equal(new[] { brand.ToString("D") }, fields["shopBrand"].Value.Keywords);
        Assert.All(fields.Values, field => Assert.Null(field.Culture));
        product.Verify(value => value.GetValue<string>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), false), Times.Never);
    }

    [Fact]
    public void DuplicateCurrencyEntriesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => StoredPriceReader.Read(Prices(("EUR", 40m), ("eur", 50m))));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.001)]
    public void InvalidDemoPricePrecisionIsRejectedByConversionAndIndexing(double amount)
    {
        decimal value = (decimal)amount;
        Assert.Throws<InvalidOperationException>(() => PriceConverter.ValidateAmount(value));
        Assert.Throws<InvalidOperationException>(() => StoredPriceReader.Read(Prices(("EUR", value))));
    }

    private static string Prices(params (string Currency, decimal Amount)[] values) => System.Text.Json.JsonSerializer.Serialize(new
    {
        contentData = values.Select(value => new
        {
            values = new object[]
            {
                new { alias = "currency", value = (object)System.Text.Json.JsonSerializer.Serialize(new[] { value.Currency }) },
                new { alias = "amount", value = (object)value.Amount }
            }
        })
    });
}
