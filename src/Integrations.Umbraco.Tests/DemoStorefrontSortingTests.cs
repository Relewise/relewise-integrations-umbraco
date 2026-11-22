using Microsoft.AspNetCore.Http;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.DataTypes.Search.Sorting.Enums;
using Relewise.Client.Requests.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Search.Querying.Sorting;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoStorefrontSortingTests
{
    [Theory]
    [InlineData(false, "")]
    [InlineData(false, "?q=tent")]
    [InlineData(true, "")]
    [InlineData(true, "?q=trail")]
    public void RelewiseDefaultDoesNotOverridePersonalizedRanking(bool editorial, string query)
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString(query);
        var search = new StorefrontSearch(http.Request, editorial, offers: false);
        Assert.Empty(search.Sorters());
    }

    [Theory]
    [InlineData("price-asc", Direction.Ascending)]
    [InlineData("price-desc", Direction.Descending)]
    public void PriceSortingRetainsItsPrimaryDirectionAndUsesRelevanceForTies(string sort, Direction direction)
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?sort=" + sort);
        var search = new StorefrontSearch(http.Request, editorial: false, offers: false);
        var sorters = search.Sorters();
        Assert.Equal(2, sorters.Length);
        Assert.Equal(direction, Assert.IsType<DecimalSorter>(sorters[0]).Direction);
        Assert.Equal(Direction.Descending, Assert.IsType<ScoreSorter>(sorters[1]).Direction);

        var request = new ContentSearchRequest(new Language("en-US"), new Currency("EUR"), User.Anonymous(), "Test", null, 0, 24)
        {
            Sorting = new ContentSortBySpecification()
        };
        CoreSearchTranslation.ApplySorting(request, sorters, "en-US");
        var primary = Assert.IsType<ContentDataSorting>(request.Sorting.Value);
        Assert.Equal(SortOrder.Descending, Assert.IsType<ContentRelevanceSorting>(primary.ThenBy).Order);
    }

    [Theory]
    [InlineData("name", Direction.Ascending)]
    [InlineData("name-desc", Direction.Descending)]
    public void ExplicitAlphabeticalSortingIsPreserved(string sort, Direction direction)
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?sort=" + sort);
        var search = new StorefrontSearch(http.Request, editorial: false, offers: false);
        Assert.Equal(direction, Assert.IsType<KeywordSorter>(Assert.Single(search.Sorters())).Direction);
    }

}
