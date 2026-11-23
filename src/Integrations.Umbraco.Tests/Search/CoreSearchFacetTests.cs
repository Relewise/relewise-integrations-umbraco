using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using RFacet = Relewise.Client.DataTypes.Search.Facets.Queries.Facet;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class CoreSearchFacetTests
{
    [Fact]
    public void ValueFacetsUseNativeSelectionsAndRetainUnrelatedFilters()
    {
        var query = new ContentFacetQuery();
        UFacet[] facets = [new KeywordFacet("category"), new IntegerExactFacet("size")];
        UFilter[] filters =
        [
            new KeywordFilter("category", ["a", "b"], false),
            new IntegerExactFilter("size", [4, 8], false),
            new KeywordFilter("scope", ["fixed"], false)
        ];

        UFilter[] remaining = CoreSearchFacets.Configure(
            query, facets, filters, "en-US", new Dictionary<string, string>());

        Assert.Equal("scope", Assert.Single(remaining).FieldName);
        Assert.Equal(2, query.Items.Count);
        Assert.Equal(new[] { "a", "b" }, Assert.IsType<ContentDataStringValueFacet>(query.Items[0]).Selected);
        Assert.Equal(new[] { "4", "8" }, Assert.IsType<ContentDataStringValueFacet>(query.Items[1]).Selected);
    }

    [Fact]
    public void NegatedMultipleAndEmptyPredicatesRemainHardFilters()
    {
        var query = new ContentFacetQuery();
        UFacet[] facets = [new KeywordFacet("brand"), new KeywordFacet("category"), new KeywordFacet("empty")];
        UFilter[] filters =
        [
            new KeywordFilter("brand", ["a"], true),
            new KeywordFilter("category", ["a"], false),
            new KeywordFilter("category", ["b"], false),
            new KeywordFilter("empty", [], false)
        ];

        Assert.Equal(
            filters,
            CoreSearchFacets.Configure(query, facets, filters, null, new Dictionary<string, string>()));
        Assert.All(query.Items, facet => Assert.Null(Assert.IsType<ContentDataStringValueFacet>(facet).Selected));
    }

    [Fact]
    public void DataRangeFacetsAreRejectedInsteadOfIssuingCountSearches()
    {
        UFacet[] facets =
        [
            new IntegerRangeFacet("number", [new IntegerRangeFacetRange("low", null, 10)]),
            new DecimalRangeFacet("number", [new DecimalRangeFacetRange("low", null, 10)]),
            new DateTimeOffsetRangeFacet("date", [new DateTimeOffsetRangeFacetRange("all", null, null)])
        ];

        foreach (UFacet facet in facets)
        {
            Assert.Throws<NotSupportedException>(() => CoreSearchFacets.Configure(
                new ContentFacetQuery(), [facet], [], null, new Dictionary<string, string>()));
        }
    }

    [Fact]
    public void CoreSelectionsCannotBeRemovedByRequestCustomization()
    {
        var query = new ContentFacetQuery();
        CoreSearchFacets.Configure(query, [new KeywordFacet("category")],
            [new KeywordFilter("category", ["a"], false)], null, new Dictionary<string, string>());
        var configured = new ContentFacetQuery();
        configured.AddDataString(CoreSearchTranslation.Field("category", "keyword"));
        configured.AddDataString("custom");

        List<RFacet> merged = CoreSearchFacets.Merge(query.Items, configured.Items);

        Assert.Equal(2, merged.Count);
        Assert.Same(query.Items[0], merged[1]);
        Assert.Equal(new[] { "a" }, Assert.IsType<ContentDataStringValueFacet>(merged[1]).Selected);
    }
}
