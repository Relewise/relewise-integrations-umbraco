using Newtonsoft.Json;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Search;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Querying.Filtering;
#endif
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Searching.Filtering;
#endif
using RelewiseFilter = Relewise.Client.Requests.Filters.Filter;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class CoreSearchTranslationTests
{
    [Fact]
    public void ScalarRangeUsesTheStoredNumberWithInclusiveLowerAndExclusiveUpperBounds()
    {
        var field = new SearchFieldDefinition(SearchFieldKind.Data, "stock", typeof(int));
        var filter = Assert.IsType<OrFilter>(
            CoreSearchTranslation.MapFilter(
                new IntegerRangeFilter("quantity", [new IntegerRangeFilterRange(10, 20)], false),
                field,
                false,
                "da-DK"));
        var range = Assert.IsType<AndFilter>(Assert.Single(filter.Filters));
        DataFilter[] bounds = [.. range.Filters.Cast<DataFilter>()];
        Assert.All(bounds, bound => Assert.Equal("stock", Assert.IsType<ContentDataFilter>(bound).Key));
        string json = JsonConvert.SerializeObject(range);
        Assert.Contains("LessThanCondition", json);
        Assert.Contains("\"Negated\":true", json);
        Assert.Contains("\"Value\":10", json);
        Assert.Contains("\"Value\":20", json);
        Assert.DoesNotContain("rwUmbracoField", json);
    }

    [Fact]
    public void KeywordScalarsUseEqualityWhileListsUseMembership()
    {
        foreach (bool collection in new[] { false, true })
        {
            var field = new SearchFieldDefinition(SearchFieldKind.Data, "tags", typeof(string), collection);
            RelewiseFilter result = CoreSearchTranslation.MapFilter(
                new KeywordFilter("tags", ["camp"], false),
                field,
                false,
                null);
            string json = JsonConvert.SerializeObject(result);
            Assert.Contains(collection ? nameof(ContainsCondition) : nameof(EqualsCondition), json);
        }
    }

    [Fact]
    public void NumericListRangesAndFieldTextOperationsAreRejected()
    {
        var field = new SearchFieldDefinition(SearchFieldKind.Data, "sizes", typeof(int), true);
        Assert.Throws<NotSupportedException>(() => CoreSearchTranslation.MapFilter(
            new IntegerRangeFilter("sizes", [new IntegerRangeFilterRange(10, 20)], false),
            field,
            false,
            null));
        Assert.Throws<NotSupportedException>(() => CoreSearchTranslation.MapFilter(
            new KeywordFilter("sizes", ["10"], false),
            field,
            false,
            null));
    }
}
