using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class CoreSearchMetadataTests
{
    [Fact]
    public void MembershipMetadataDoesNotDuplicateOrModifyBusinessValues()
    {
        var content = new Content("article")
        {
            Data = new Dictionary<string, DataValue?> { ["stock"] = 20, ["tags"] = new DataValue(new[] { "camp" }) }
        };
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);
        var group = Guid.NewGuid();

        CoreSearchMetadata.Apply(
            mapped,
            new ExportSourceDocument([null, "da-DK", "en-US"], ["", "da-dk"], [group]));

        Assert.Equal(20, content.Data["stock"]!.ValueAs<double>());
        Assert.Equal(5, content.Data.Count);
        Assert.DoesNotContain(content.Data.Keys, key => key.StartsWith("rwUmbracoField_", StringComparison.Ordinal));
        Assert.Equal(new[] { "", "da-dk" }, content.Data[RelewiseDataKeys.Search.Culture]!.ValueAsListOf<string>());
        Assert.True(content.Data[RelewiseDataKeys.Search.Protected]!.ValueAs<bool>());
        Assert.Contains(group.ToString("D"), content.Data["rwUmbracoSearchAccess"]!.ValueAsListOf<string>());
    }

    [Fact]
    public void RemovingProtectionAndDefaultSegmentsUpdatesMembership()
    {
        var content = new Content("article");
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "article", content, true);
        CoreSearchMetadata.Apply(
            mapped,
            new ExportSourceDocument(["en-US"], ["en-us"], [Guid.NewGuid()]));
        CoreSearchMetadata.Apply(mapped, new ExportSourceDocument(["en-US"], [], null));

        Assert.Empty(content.Data![RelewiseDataKeys.Search.Culture]!.ValueAsListOf<string>());
        Assert.False(content.Data[RelewiseDataKeys.Search.Protected]!.ValueAs<bool>());
        Assert.Empty(content.Data["rwUmbracoSearchAccess"]!.ValueAsListOf<string>());
    }
}
