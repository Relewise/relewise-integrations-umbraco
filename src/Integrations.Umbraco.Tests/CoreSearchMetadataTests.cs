using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests;

public class CoreSearchMetadataTests
{
    [Fact]
    public void LocalizedNumericAndSegmentFieldsDoNotBlockMappedBusinessData()
    {
        var content = new Content("article") { Data = new() { ["customerField"] = "retained" } };
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);
        CoreSearchMetadata.Apply(mapped, new CoreSearchDocument([new("en-US", null), new("en-US", "vip")],
            [new("price", new() { Decimals = [12.5m] }, "en-US", null),
             new("body", new() { Texts = ["Default text"] }, "en-US", null),
             new("body", new() { Texts = ["Private segment"] }, "en-US", "vip")], null));
        Assert.Equal("retained", content.Data["customerField"]!.ValueAs<string>());
        Assert.Contains("price", mapped.UnsupportedCoreFields);
        var text = content.Data[CoreSearchTranslation.Field("body", "text")]!.ValueAs<Multilingual>();
        Assert.Equal("Default text", Assert.Single(text.Values!).Text);
    }
    [Fact]
    public void ProtectionAndCultureMetadataRetainCoreMembership()
    {
        Guid group = Guid.NewGuid();
        var product = new Product("sku");
        var mapped = new MappedEntity(Guid.NewGuid(), "product", "sku", product, true);
        CoreSearchMetadata.Apply(mapped, new CoreSearchDocument([new(null, null), new("da-DK", null)], [], new([group])));
        Assert.True(product.Data![CoreSearchTranslation.ProtectedKey]!.ValueAs<bool>());
        Assert.Contains(group.ToString("D"), product.Data[CoreSearchTranslation.AccessKey]!.ValueAsListOf<string>());
        Assert.Equal(new[] { "", "da-dk" }, product.Data[CoreSearchTranslation.CultureKey]!.ValueAsListOf<string>());
    }
    [Fact]
    public async Task SegmentQueriesFailBeforeAnySdkRequest()
    {
        var searcher = new MappedEntitySearcher(null!, null!, null!);
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync("catalogue", segment: "vip"));
    }
}
