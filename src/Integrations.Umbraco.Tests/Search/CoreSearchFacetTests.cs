using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class CoreSearchFacetTests
{
    [Theory]
    [InlineData(1.5)]
    [InlineData(2147483648d)]
    public void InvalidIntegerFacetValuesAreNotSilentlyTruncated(double value)
    {
        var response = new ContentFacetResult
        {
            Items =
            [
                new ContentDataDoubleValueFacetResult(
                    "stock",
                    null,
                    CollectionFilterType.Or,
                    [new AvailableFacetValue<double>(value, false, 1)])
            ]
        };
        Assert.Throws<InvalidOperationException>(() => CoreSearchFacets.Read(
            response,
            [new IntegerExactFacet("stock")],
            _ => new SearchFieldDefinition(SearchFieldKind.Data, "stock", typeof(int))));
    }

    [Fact]
    public void ExactFacetsUseStoredTypesAndNativeSelections()
    {
        var query = new ContentFacetQuery();

        SearchFieldDefinition Resolve(string name) => name == "size"
            ? new SearchFieldDefinition(SearchFieldKind.Data, "size", typeof(int))
            : new SearchFieldDefinition(SearchFieldKind.Data, name, typeof(string), true);

        UFilter[] filters =
        [
            new KeywordFilter("tags", ["camp"], false),
            new IntegerExactFilter("size", [4, 8], false), new KeywordFilter("scope", ["fixed"], false)
        ];

        UFilter[] remaining = CoreSearchFacets.Configure(
            query,
            [new KeywordFacet("tags"), new IntegerExactFacet("size")],
            filters,
            Resolve);

        Assert.Equal("scope", Assert.Single(remaining).FieldName);
        Assert.Equal(new[] { "camp" }, Assert.IsType<ContentDataStringValueFacet>(query.Items[0]).Selected);
        Assert.Equal(new double[] { 4, 8 }, Assert.IsType<ContentDataDoubleValueFacet>(query.Items[1]).Selected);
    }

    [Fact]
    public void NumericRangesUseNativeBucketsAndRejectOverlapsAndListRanges()
    {
        var field = new SearchFieldDefinition(SearchFieldKind.Data, "stock", typeof(int));
        UFacet facet = new IntegerRangeFacet("stock", [new IntegerRangeFacetRange("low", 0, 10)]);
        var query = new ContentFacetQuery();
        CoreSearchFacets.Configure(query, [facet], [], _ => field);
        var ranges = Assert.IsType<ContentDataDoubleRangesFacet>(Assert.Single(query.Items));
        Assert.Equal(10, Assert.Single(ranges.PredefinedRanges!).UpperBoundExclusive);
        Assert.Throws<NotSupportedException>(() => CoreSearchFacets.Configure(
            new ContentFacetQuery(),
            [facet],
            [],
            _ => field with { Collection = true }));
        Assert.Throws<NotSupportedException>(() => CoreSearchFacets.Configure(
            new ContentFacetQuery(),
            [
                new IntegerRangeFacet(
                    "stock",
                    [
                        new IntegerRangeFacetRange("a", 0, 10),
                        new IntegerRangeFacetRange("b", 5, 20)
                    ])
            ],
            [],
            _ => field));
    }

    [Fact]
    public void NegatedAndMultiplePredicatesRemainHardFilters()
    {
        UFilter[] filters = [new KeywordFilter("tags", ["a"], true), new KeywordFilter("tags", ["b"], false)];
        var query = new ContentFacetQuery();
        Assert.Equal(
            filters,
            CoreSearchFacets.Configure(
                query,
                [new KeywordFacet("tags")],
                filters,
                _ => new SearchFieldDefinition(SearchFieldKind.Data, "tags", typeof(string), true)));
        Assert.Null(Assert.IsType<ContentDataStringValueFacet>(Assert.Single(query.Items)).Selected);
    }

    [Fact]
    public void MissingFacetResponsesAndDuplicateTargetsAreRejected()
    {
        var field = new SearchFieldDefinition(SearchFieldKind.Data, "stock", typeof(int));
        Assert.Throws<InvalidOperationException>(() => CoreSearchFacets.Read(
            null,
            [new IntegerExactFacet("stock")],
            _ => field));
        Assert.Throws<NotSupportedException>(() => CoreSearchFacets.Configure(
            new ContentFacetQuery(),
            [new IntegerExactFacet("stock"), new IntegerExactFacet("alias")],
            [],
            _ => field));
    }

    [Fact]
    public void IncompatibleFacetResponseTypesAreRejected()
    {
        var response = new ContentFacetResult
        {
            Items = [new ContentDataStringValueFacetResult("stock", [], null, [])]
        };

        Assert.Throws<InvalidOperationException>(() => CoreSearchFacets.Read(
            response,
            [new IntegerExactFacet("stock")],
            _ => new SearchFieldDefinition(SearchFieldKind.Data, "stock", typeof(int))));
    }
}
