using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Shared;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;

internal static class RecommendationRequests
{
    public const int VisibleCount = 4;

    public static ProductRecommendationRequestCollection CreateRequests(
        User user,
        DemoMarket market,
        string productId,
        string? variantId,
        string categoryId,
        string productType)
    {
        var similar = new SimilarProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            "Product page: Similar products",
            user,
            new ProductAndVariantId(productId, variantId),
            new SimilarProductsEvaluationSettings
            {
                SignificanceOfSimilaritiesInDisplayName = 4,
                SignificanceOfSimilarListPrice = 0.15,
                SignificanceOfSimilarSalesPrice = 0.15,
                SignificanceOfCommonImmediateParentCategories = 0,
                SignificanceOfCommonParentsParentCategories = 0,
                SignificanceOfCommonAncestorCategories = 0,
                SignificanceOfSimilarBrand = 0,
                SignificanceOfIdenticalProductDataValues = 0,
                SignificantProductDataFields =
                [
                    new SignificantDataValue(
                        PropertyAliases.Capacity,
                        SignificantDataValue.SignificantDataValueComparer.StringSimilarity),
                    new SignificantDataValue(
                        PropertyAliases.WeightGrams,
                        SignificantDataValue.SignificantDataValueComparer.NumericPercentDifference,
                        0.25
                    )
                ]
            }
        );

        similar.Filters.Add(new ProductDataFilter(DataKeys.ProductType, new EqualsCondition(productType)));
        similar.Filters.Add(new ProductCategoryIdFilter(categoryId, CategoryScope.ImmediateParent));
        similar.Filters.Add(new ProductDataFilter(DataKeys.InStock, new EqualsCondition(true)));
        ConfigureRequest(similar, market, productId);

        // The sections serve different purposes; an actual recently viewed item may also be a similar Product.
        return new ProductRecommendationRequestCollection(
            false,
            similar,
            CreateRecentlyViewed(
                user,
                market,
                productId
            )
        );
    }

    public static RecentlyViewedProductsRequest CreateRecentlyViewed(
        User user,
        DemoMarket market,
        string? excludedProductId = null,
        string? displayedAtLocation = null)
    {
        var request = new RecentlyViewedProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            displayedAtLocation ?? (excludedProductId == null
                ? "Home page: Recently viewed"
                : "Product page: Recently viewed"),
            user
        );

        ConfigureRequest(request, market, excludedProductId);

        return request;
    }

    public static PopularProductsRequest CreatePopularProducts(
        User user,
        DemoMarket market,
        bool emptyCart = false)
    {
        var request = new PopularProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            emptyCart ? "Empty cart: Popular products" : "Home page: Popular products",
            user,
            PopularityTypes.MostViewed,
            TimeSpan.FromDays(30)
        );

        ConfigureRequest(request, market, null);
        request.Filters.Add(new ProductDataFilter(DataKeys.InStock, new EqualsCondition(true)));

        return request;
    }

    public static PopularProductCategoriesRecommendationRequest CreatePopularCategories(
        User user,
        DemoMarket market,
        IEnumerable<string> categoryIds)
    {
        var request = new PopularProductCategoriesRecommendationRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            "Home page: Popular categories",
            user,
            new ProductCategoryRecommendationWeights(1, 1, 0),
            TimeSpan.FromDays(30)
        );

        // Only the published browsing categories on this site may be recommended.
        request.Filters.Add(new ProductCategoryIdFilter(categoryIds, CategoryScope.ImmediateParent));
        request.Settings.NumberOfRecommendations = VisibleCount;
        request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations = false;
        request.Settings.AllowReplacingOfRecentlyShownRecommendations = false;

        return request;
    }

    private static void ConfigureRequest(
        ProductRecommendationRequest request,
        DemoMarket market,
        string? excludedProductId)
    {
        if (excludedProductId != null)
        {
            // Exclude the current Product, including its other Variants.
            request.Filters.Add(new ProductIdFilter(excludedProductId, true));
        }

        request.Filters.Add(
            new ProductDataFilter(
                RelewiseDataKeys.DocumentTypeAlias,
                new EqualsCondition(DocumentTypeAliases.Product)));
        request.Filters.Add(new ProductDataFilter(RelewiseDataKeys.Published, new EqualsCondition(true)));
        request.Filters.Add(new ProductDataFilter(RelewiseDataKeys.Search.Protected, new EqualsCondition(false)));
        request.Filters.Add(
            new ProductDataFilter(
                RelewiseDataKeys.Search.Culture,
                new ContainsCondition(market.Culture.ToLowerInvariant())));
        request.Settings.NumberOfRecommendations = VisibleCount;
        // Keep browsing history genuine: no fill or novelty replacement.
        request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations = false;
        request.Settings.AllowReplacingOfRecentlyShownRecommendations = false;
        request.Settings.AllowProductsCurrentlyInCart = true;
        request.Settings.VariantRequestSettings = new VariantRecommendationRequestSettings
        {
            MaxVariantsPerProduct = 0
        };
        request.Settings.SelectedProductProperties = new SelectedProductPropertiesSettings
        {
            DataKeys = [RelewiseDataKeys.NodeKey]
        };
    }
}
