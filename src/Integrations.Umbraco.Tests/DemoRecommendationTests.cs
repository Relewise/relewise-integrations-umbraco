using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Recommendations;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoRecommendationTests
{
    [Theory]
    [InlineData("eu", "en-US", "EUR")]
    [InlineData("dk", "da-DK", "DKK")]
    [InlineData("us", "en-US", "USD")]
    public void RecommendationsUseTheTrackedUserMarketCanonicalIdsAndPublicSiteScope(string marketId, string culture, string currency)
    {
        var user = User.ByTemporaryId("shared-visitor");
        var batch = StorefrontRecommendations.CreateRequests(user, DemoMarket.All.Single(market => market.Id == marketId),
            "site-key", "sku", "variant-sku", "category-id", "Backpack");

        Assert.False(batch.RequireDistinctProductsAcrossResults);
        var similar = Assert.IsType<SimilarProductsRequest>(batch.Requests[0]);
        Assert.Equal("sku", similar.ExistingProductId!.ProductId);
        Assert.Equal("variant-sku", similar.ExistingProductId.VariantId);
        Assert.IsType<RecentlyViewedProductsRequest>(batch.Requests[1]);
        Assert.All(batch.Requests, request =>
        {
            Assert.Same(user, request.User);
            Assert.Equal(culture.ToLowerInvariant(), request.Language!.Value);
            Assert.Equal(currency, request.Currency!.Value);
            Assert.Equal(StorefrontRecommendations.VisibleCount, request.Settings.NumberOfRecommendations);
            Assert.False(request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations);
            Assert.False(request.Settings.AllowReplacingOfRecentlyShownRecommendations);
            Assert.Equal(0, request.Settings.VariantRequestSettings!.MaxVariantsPerProduct);
            Assert.Equal(new[] { "rwUmbracoNodeKey" }, request.Settings.SelectedProductProperties!.DataKeys);
            var exclusion = Assert.Single(request.Filters.Items!.OfType<ProductIdFilter>());
            Assert.True(exclusion.Negated);
            Assert.Equal(new[] { "sku" }, exclusion.ProductIds);
            string json = JsonConvert.SerializeObject(request.Filters);
            Assert.Contains("site-key", json);
            Assert.Contains("rwUmbracoPublished", json);
            Assert.Contains("rwUmbracoSearchProtected", json);
            Assert.Contains(culture.ToLowerInvariant(), json);
        });
    }

    [Fact]
    public void SimilarProductsAreAvailableAlternativesWhileRecentlyViewedRetainsTheActualBrowsingHistory()
    {
        var batch = StorefrontRecommendations.CreateRequests(User.ByTemporaryId("visitor"), DemoMarket.All[0], "site", "sku", null, "category", "Camping chair");
        var similar = Assert.IsType<SimilarProductsRequest>(batch.Requests[0]);
        Assert.Equal(new[] { "category" }, Assert.Single(similar.Filters.Items!.OfType<ProductCategoryIdFilter>()).CategoryIds);
        var type = Assert.Single(similar.Filters.Items!.OfType<ProductDataFilter>(), filter => filter.Key == "productType");
        Assert.Equal("Camping chair", Assert.IsType<EqualsCondition>(Assert.Single(type.Conditions!.Items!)).Value.Value);
        var stock = Assert.Single(similar.Filters.Items!.OfType<ProductDataFilter>(), filter => filter.Key == "inStock");
        Assert.Equal(true, Assert.IsType<EqualsCondition>(Assert.Single(stock.Conditions!.Items!)).Value.Value);
        Assert.DoesNotContain(batch.Requests[1].Filters.Items!, filter => filter is ProductCategoryIdFilter || filter is ProductDataFilter { Key: "inStock" });
        Assert.Equal(0, similar.EvaluationSettings!.SignificanceOfIdenticalProductDataValues);
        Assert.DoesNotContain(similar.EvaluationSettings.SignificantProductDataFields!, field => field.Key.StartsWith("rwUmbraco", StringComparison.Ordinal));
    }

    [Fact]
    public void HomePageHistoryDoesNotExcludePreviouslyViewedProductsOrFillAnEmptyHistory()
    {
        var request = StorefrontRecommendations.CreateRecentlyViewed(User.ByTemporaryId("visitor"), DemoMarket.All[0], "site");
        Assert.DoesNotContain(request.Filters.Items!, filter => filter is ProductIdFilter);
        Assert.False(request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations);
        Assert.Equal("Home page: Recently viewed", request.DisplayedAtLocationType);
    }

    [Fact]
    public void EmptyCartHistoryHasItsOwnAnalyticsLocationAndDoesNotExcludeTheRemovedProduct()
    {
        var request = StorefrontRecommendations.CreateRecentlyViewed(User.ByTemporaryId("visitor"), DemoMarket.All[0], "site",
            displayedAtLocation: "Empty cart: Recently viewed");
        Assert.Equal("Empty cart: Recently viewed", request.DisplayedAtLocationType);
        Assert.DoesNotContain(request.Filters.Items!, filter => filter is ProductIdFilter);
        Assert.False(request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations);
    }

    [Theory]
    [InlineData(false, "Home page: Popular products")]
    [InlineData(true, "Empty cart: Popular products")]
    public void PopularProductsUseRecentViewsAndTheSamePublicInStockProductScope(bool emptyCart, string location)
    {
        var user = User.ByTemporaryId("visitor");
        var request = StorefrontRecommendations.CreatePopularProducts(user, DemoMarket.All[0], "site", emptyCart);
        Assert.Same(user, request.User);
        Assert.Equal(Relewise.Client.Requests.Shared.PopularityTypes.MostViewed, request.BasedOn);
        Assert.Equal(30 * 24 * 60, request.SinceMinutesAgo);
        Assert.Equal(location, request.DisplayedAtLocationType);
        Assert.False(request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations);
        Assert.False(request.Settings.AllowReplacingOfRecentlyShownRecommendations);
        Assert.Equal(StorefrontRecommendations.VisibleCount, request.Settings.NumberOfRecommendations);
        Assert.DoesNotContain(request.Filters.Items!, filter => filter is ProductIdFilter);
        var stock = Assert.Single(request.Filters.Items!.OfType<ProductDataFilter>(), filter => filter.Key == "inStock");
        Assert.Equal(true, Assert.IsType<EqualsCondition>(Assert.Single(stock.Conditions!.Items!)).Value.Value);
        Assert.Contains("site", JsonConvert.SerializeObject(request.Filters));
    }

    [Fact]
    public void PopularCategoriesAreRestrictedToTheSitesPublishedBrowsingCategoriesAndUseViewsOnly()
    {
        var user = User.ByTemporaryId("visitor");
        var request = StorefrontRecommendations.CreatePopularCategories(user, DemoMarket.All[1], ["packs", "camping"]);
        Assert.Same(user, request.User);
        Assert.Equal("da-dk", request.Language!.Value);
        Assert.Equal("DKK", request.Currency!.Value);
        Assert.Equal(30 * 24 * 60, request.SinceMinutesAgo);
        Assert.Equal(1, request.Weights.CategoryViews);
        Assert.Equal(1, request.Weights.ProductViews);
        Assert.Equal(0, request.Weights.ProductPurchases);
        Assert.Equal(new[] { "packs", "camping" }, Assert.Single(request.Filters.Items!.OfType<ProductCategoryIdFilter>()).CategoryIds);
        Assert.False(request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations);
        Assert.False(request.Settings.AllowReplacingOfRecentlyShownRecommendations);
    }

}
