using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Search;

namespace Relewise.Integrations.Umbraco.Tests;

public class CoreSearchMetadataTests
{
    [Fact]
    public void MixedInvariantAndLocalizedBlockFieldsRetainSharedTextWithoutLeakingLanguages()
    {
        var content = new Content("article");
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);
        CoreSearchMetadata.Apply(mapped, new CoreSearchDocument([new("en-US", null), new("da-DK", null)],
            [new("blocks", new() { Texts = ["Shared text"], Keywords = ["shared"] }, null, null),
             new("blocks", new() { Texts = ["English text"], Keywords = ["english"] }, "en-US", null),
             new("blocks", new() { Texts = ["Dansk tekst"], Keywords = ["dansk"] }, "da-DK", null)], null),
            ["en-US", "da-DK", "de-DE"]);

        Multilingual text = content.Data![CoreSearchTranslation.Field("blocks", "text")]!.ValueAs<Multilingual>();
        Assert.Equal("Shared text English text", text.Values!.Single(value => value.Language.Value == "en-us").Text);
        Assert.Equal("Shared text Dansk tekst", text.Values!.Single(value => value.Language.Value == "da-dk").Text);
        Assert.Equal("Shared text", text.Values!.Single(value => value.Language.Value == "de-de").Text);
        MultilingualCollection keywords = content.Data[CoreSearchTranslation.Field("blocks", "keyword")]!.ValueAs<MultilingualCollection>();
        Assert.Equal(new[] { "shared", "dansk" }, keywords.Values!.Single(value => value.Language.Value == "da-dk").Values);
    }

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
        Assert.Equal(12.5d, content.Data[CoreSearchTranslation.Field("price", "decimalMin", "en-US")]!.ValueAs<double>());
        Multilingual text = content.Data[CoreSearchTranslation.Field("body", "text")]!.ValueAs<Multilingual>();
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
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapContent("article", mapping => mapping.AutoMap());
            options.SearchProviders.AddContent("articles");
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        using ServiceProvider provider = services.BuildServiceProvider();
        var searcher = new MappedEntitySearcher(provider.GetRequiredService<RelewiseUmbracoConfiguration>(), null!, null!);
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync("articles", segment: "vip"));
    }
}
