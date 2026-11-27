using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class MappingCollectionTests
{
    [Fact]
    public void LookupPreservesCanonicalAliasAndExportMembership()
    {
        var article = new MappingDefinition(typeof(Content)) { DocumentTypeAlias = "Article" };
        var product = new MappingDefinition(typeof(Product)) { DocumentTypeAlias = "product", Export = false };
        var mappings = new MappingCollection([article, product]);

        Assert.Same(article, mappings["ARTICLE"]);
        Assert.True(mappings.TryGetMapping("article", out MappingDefinition? found));
        Assert.Equal("Article", found.DocumentTypeAlias);
        Assert.False(mappings.TryGetMapping("missing", out _));
        Assert.Same(article, Assert.Single(mappings.Exported));
        Assert.True(mappings.HasExports);
        Assert.True(mappings.Exports(typeof(Content)));
        Assert.True(mappings.HasEntityType(typeof(Product)));
        Assert.False(mappings.Exports(typeof(Product)));
        Assert.False(mappings.HasEntityType(typeof(Brand)));
        Assert.Equal(2, mappings.Count);
    }

    [Fact]
    public void CollectionSnapshotsRegistrationMembership()
    {
        var source = new List<MappingDefinition>();
        var mappings = new MappingCollection(source);
        source.Add(new MappingDefinition(typeof(Content)) { DocumentTypeAlias = "article" });

        Assert.Empty(mappings);
        Assert.False(mappings.HasExports);
        Assert.False(mappings.TryGetMapping("article", out _));
    }
}
