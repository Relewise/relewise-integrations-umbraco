using Microsoft.AspNetCore.Http;
using Moq;
using Relewise.DemoStorefront.Features.Browsing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoStorefrontSearchTests
{
    [Theory]
    [InlineData("/en/store", "EUR")]
    [InlineData("/da/store", "DKK")]
    public void OffersAndPriceSortingUseTheActiveMarket(string path, string currency)
    {
        var http = new DefaultHttpContext();

        http.Request.Path = path;
        http.Request.QueryString = new QueryString("?price=low&price=high&available=true&sort=price-desc");

        var search = new StorefrontSearch(http.Request, false, true);
        Filter[] filters = search.Filters();

        Assert.Contains(filters, filter => filter is KeywordFilter && filter.FieldName == "onSale_" + currency);
        Assert.Contains(filters, filter => filter is KeywordFilter && filter.FieldName == "inStock");
        Assert.Contains(filters, filter => filter is DecimalRangeFilter && filter.FieldName == "shopPrice" + currency);
        Assert.Equal("shopPrice" + currency, Assert.IsType<DecimalSorter>(search.Sorters()[0]).FieldName);
        Assert.Equal(Direction.Descending, search.Sorters()[0].Direction);
        Assert.Equal(4, search.Facets().Length);
    }

    [Fact]
    public void BrandSelectionsUseOneOrFilterAndFixedBrandPagesOmitTheFacet()
    {
        var http = new DefaultHttpContext();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        http.Request.QueryString = new QueryString($"?brand={first:D}&brand={second:D}");

        var search = new StorefrontSearch(http.Request, false, false);
        var filter = Assert.IsType<KeywordFilter>(Assert.Single(search.Filters()));

        Assert.Equal("shopBrand", filter.FieldName);
        Assert.Equal(new[] { first.ToString("D"), second.ToString("D") }, filter.Values);
        Assert.Contains(search.Facets(), facet => facet.FieldName == "shopBrand");
        Assert.DoesNotContain("brand=", search.ResetUrl());
        Assert.Contains("brand=", search.ClearQueryUrl());

        http.Request.QueryString = QueryString.Empty;

        var type = new Mock<IPublishedContentType>();

        type.SetupGet(value => value.Alias).Returns("brand");

        var brand = new Mock<IPublishedContent>();

        brand.SetupGet(value => value.ContentType).Returns(type.Object);
        brand.SetupGet(value => value.Key).Returns(first);

        var scoped = new StorefrontSearch(http.Request, false, false, brand.Object);

        Assert.DoesNotContain(scoped.Facets(), facet => facet.FieldName == "shopBrand");
        Assert.Equal(
            first.ToString("D"),
            Assert.Single(Assert.IsType<KeywordFilter>(Assert.Single(scoped.Filters())).Values));
    }

    [Fact]
    public void OnSaleUsesTheActiveCurrencyAndRemainsImplicitOnOffers()
    {
        var http = new DefaultHttpContext();

        http.Request.Path = "/da/store";
        http.Request.QueryString = new QueryString("?onSale=true&available=true&maxPrice=400");

        var search = new StorefrontSearch(http.Request, false, false);

        Assert.Contains(search.Filters(), filter => filter.FieldName == "onSale_DKK");
        Assert.Contains(search.Facets(), facet => facet.FieldName == "onSale_DKK");
        Assert.Contains(search.Filters(), filter => filter.FieldName == "inStock");
        Assert.DoesNotContain("onSale", search.ResetUrl());
        Assert.Contains("onSale=true", search.ClearQueryUrl());

        var offers = new StorefrontSearch(http.Request, false, true);

        Assert.Single(offers.Filters(), filter => filter.FieldName == "onSale_DKK");
        Assert.DoesNotContain(offers.Facets(), facet => facet.FieldName == "onSale_DKK");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("yes")]
    public void InvalidOnSaleValuesAreRejected(string value)
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString("?onSale=" + value);
        Assert.False(new StorefrontSearch(http.Request, false, false).IsValid(Mock.Of<IPublishedContent>()));
    }

    [Fact]
    public void ClearingFiltersRetainsQuerySortAndCategoryPage()
    {
        var http = new DefaultHttpContext();

        http.Request.Path = "/en/fieldwork-equipment/camping/";
        http.Request.QueryString = new QueryString("?q=tent&sort=price-asc&price=low&available=true&page=3");

        var search = new StorefrontSearch(http.Request, false, true);

        Assert.Equal("/en/fieldwork-equipment/camping/?q=tent&sort=price-asc", search.ResetUrl());
        Assert.Equal(
            "/en/fieldwork-equipment/camping/?sort=price-asc&price=low&available=true",
            search.ClearQueryUrl());
    }

    [Fact]
    public void FixedCategoryScopeIsNotRemovedByDisjunctiveFacetCounting()
    {
        var type = new Mock<IPublishedContentType>();

        type.SetupGet(value => value.Alias).Returns("productCategory");

        var category = new Mock<IPublishedContent>();

        category.SetupGet(value => value.ContentType).Returns(type.Object);

        var http = new DefaultHttpContext();
        var search = new StorefrontSearch(http.Request, false, false, category.Object);

        Assert.DoesNotContain(search.Facets(), facet => facet.FieldName == "shopCategory");
        Assert.Contains(search.Facets(), facet => facet.FieldName == "inStock");
        Assert.Contains(search.Facets(), facet => facet.FieldName == "shopPriceEUR");
    }

    [Fact]
    public void InclusiveMaximumUsesTheNextCentAsTheNativeExclusiveBound()
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString("?minPrice=25.50&maxPrice=100");

        var search = new StorefrontSearch(http.Request, false, false);
        var filter = Assert.IsType<DecimalRangeFilter>(Assert.Single(search.Filters()));
        DecimalRangeFilterRange range = Assert.Single(filter.Ranges);

        Assert.Equal(25.50m, range.MinValue);
        Assert.Equal(100.01m, range.MaxValue);
        Assert.True(search.HasFilters);
        Assert.DoesNotContain("minPrice", search.ResetUrl());
        Assert.DoesNotContain("maxPrice", search.ResetUrl());
    }

    [Theory]
    [InlineData("?minPrice=-1")]
    [InlineData("?minPrice=100&maxPrice=50")]
    [InlineData("?maxPrice=NaN")]
    [InlineData("?maxPrice=5.001")]
    [InlineData("?minPrice=1&minPrice=2")]
    [InlineData("?price=low&maxPrice=50")]
    public void InvalidBudgetsAreRejectedBeforeQuerying(string query)
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString(query);
        Assert.False(
            new StorefrontSearch(http.Request, false, false).IsValid(Mock.Of<IPublishedContent>()));
    }

    [Fact]
    public void EqualBoundsSelectAnExactCentPrice()
    {
        var http = new DefaultHttpContext();

        http.Request.Path = "/en/store";
        http.Request.QueryString = new QueryString("?minPrice=50&maxPrice=50&sort=price-asc");

        var search = new StorefrontSearch(http.Request, false, false);
        DecimalRangeFilterRange range =
            Assert.Single(Assert.IsType<DecimalRangeFilter>(Assert.Single(search.Filters())).Ranges);

        Assert.Equal(50m, range.MinValue);
        Assert.Equal(50.01m, range.MaxValue);
    }

    [Fact]
    public void AnOpenEndedMinimumDoesNotIntroduceAnUpperBound()
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString("?minPrice=50");

        var filter =
            Assert.IsType<DecimalRangeFilter>(
                Assert.Single(new StorefrontSearch(http.Request, false, false).Filters()));

        Assert.Null(Assert.Single(filter.Ranges).MaxValue);
    }

    [Fact]
    public void EmptyBudgetInputsDoNotCreateAnAppliedFilter()
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString("?minPrice=&maxPrice=");

        var search = new StorefrontSearch(http.Request, false, false);

        Assert.False(search.HasFilters);
        Assert.Empty(search.Filters());
    }

    [Fact]
    public void EditorialSearchDoesNotApplyProductFiltersOrFacets()
    {
        var http = new DefaultHttpContext();

        http.Request.QueryString = new QueryString("?q=trail");

        var search = new StorefrontSearch(http.Request, true, false);

        Assert.Empty(search.Filters());
        Assert.Empty(search.Facets());
        Assert.Empty(search.Sorters());
    }
}
