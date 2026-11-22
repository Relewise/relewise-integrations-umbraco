using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class CoreSearchMetadataTests
{
    [Fact]
    public void MixedInvariantAndLocalizedBlockFieldsRetainSharedTextWithoutLeakingLanguages()
    {
        var content = new Content("article");
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);

        CoreSearchMetadata.Apply(
            mapped,
            new CoreSearchDocument(
                [new Variation("en-US", null), new Variation("da-DK", null)],
                [
                    new IndexField(
                        "blocks",
                        new IndexValue { Texts = ["Shared text"], Keywords = ["shared"] },
                        null,
                        null),
                    new IndexField(
                        "blocks",
                        new IndexValue { Texts = ["English text"], Keywords = ["english"] },
                        "en-US",
                        null),
                    new IndexField(
                        "blocks",
                        new IndexValue { Texts = ["Dansk tekst"], Keywords = ["dansk"] },
                        "da-DK",
                        null)
                ],
                null),
            ["en-US", "da-DK", "de-DE"]);

        var text = content.Data![CoreSearchTranslation.Field("blocks", "text")]!.ValueAs<Multilingual>();

        Assert.Equal("Shared text English text", text.Values!.Single(value => value.Language.Value == "en-us").Text);
        Assert.Equal("Shared text Dansk tekst", text.Values!.Single(value => value.Language.Value == "da-dk").Text);
        Assert.Equal("Shared text", text.Values!.Single(value => value.Language.Value == "de-de").Text);

        var keywords = content.Data[CoreSearchTranslation.Field("blocks", "keyword")]!
            .ValueAs<MultilingualCollection>();

        Assert.Equal(
            new[] { "shared", "dansk" },
            keywords.Values!.Single(value => value.Language.Value == "da-dk").Values);
    }

    [Fact]
    public void LocalizedNumericAndSegmentFieldsDoNotBlockMappedBusinessData()
    {
        var content = new Content("article")
        { Data = new Dictionary<string, DataValue?> { ["customerField"] = "retained" } };
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);

        CoreSearchMetadata.Apply(
            mapped,
            new CoreSearchDocument(
                [new Variation("en-US", null), new Variation("en-US", "vip")],
                [
                    new IndexField("price", new IndexValue { Decimals = [12.5m] }, "en-US", null),
                    new IndexField("body", new IndexValue { Texts = ["Default text"] }, "en-US", null),
                    new IndexField("body", new IndexValue { Texts = ["Private segment"] }, "en-US", "vip")
                ],
                null));
        Assert.Equal("retained", content.Data["customerField"]!.ValueAs<string>());
        Assert.Equal(
            12.5d,
            content.Data[CoreSearchTranslation.Field("price", "decimalMin", "en-US")]!.ValueAs<double>());

        var text = content.Data[CoreSearchTranslation.Field("body", "text")]!.ValueAs<Multilingual>();

        Assert.Equal("Default text", Assert.Single(text.Values!).Text);
    }

    [Fact]
    public void ProtectionAndCultureMetadataRetainCoreMembership()
    {
        var group = Guid.NewGuid();
        var product = new Product("sku");
        var mapped = new MappedEntity(Guid.NewGuid(), "product", "sku", product, true);

        CoreSearchMetadata.Apply(
            mapped,
            new CoreSearchDocument(
                [new Variation(null, null), new Variation("da-DK", null)],
                [],
                new ContentProtection([group])));
        Assert.True(product.Data![RelewiseDataKeys.Search.Protected]!.ValueAs<bool>());
        Assert.Contains(group.ToString("D"), product.Data[RelewiseDataKeys.Search.Access]!.ValueAsListOf<string>());
        Assert.Equal(new[] { "", "da-dk" }, product.Data[RelewiseDataKeys.Search.Culture]!.ValueAsListOf<string>());
    }

    [Fact]
    public async Task SegmentQueriesFailBeforeAnySdkRequest()
    {
        var services = new ServiceCollection();

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapContent("article", mapping => mapping.AutoMap());
            options.SearchProviders.AddContent("articles");
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();

        using ServiceProvider provider = services.BuildServiceProvider();
        var searcher =
            new MappedEntitySearcher(provider.GetRequiredService<RelewiseUmbracoConfiguration>(), null!, null!);

        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync("articles", segment: "vip"));
    }
}
