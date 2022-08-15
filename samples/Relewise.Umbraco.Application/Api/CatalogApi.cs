using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.DataTypes.Search.Sorting.Enums;
using Relewise.Client.DataTypes.Search.Sorting.Product;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Relewise.Integrations.Umbraco;
using Relewise.Umbraco.Application.Models;

namespace Relewise.Umbraco.Application.Api;

public static class CatalogApi
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapCatalogRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/catalog/search", Search);
        builder.MapGet("api/catalog/recommend/popular", RecommendPopular);
        builder.MapGet("api/catalog/recommend/viewedafter", RecommendViewedAfter);
        builder.MapGet("api/catalog/recommend/purchasedwith", RecommendPurchasedWith);

        return builder;
    }

    private static async Task Search(
        HttpContext context,
        [FromQuery] int pageSize,
        [FromQuery] int page,
        [FromQuery] string? q,
        [FromQuery] string displayedAt,
        [FromQuery] string? categoryId,
        [FromQuery] string? category2Id,
        [FromQuery] string? sortBy,
        [FromQuery] string? productId)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ProductSearchRequest request = new ProductSearchRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            new Currency(Thread.CurrentThread.CurrentUICulture),
            user,
            displayedAt,
            q,
            skip: (page - 1) * pageSize,
            take: pageSize)
        {
            Settings = new ProductSearchSettings
            {
                SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
            }
        };

        request.Facets.AddCategory(CategorySelectionStrategy.ImmediateParent, category2Id != null ? new[] { category2Id } : null);

        if (categoryId != null)
            request.Filters.Add(new ProductCategoryIdFilter(categoryId, CategoryScope.Ancestor));

        if (productId != null)
            request.Filters.Add(new ProductIdFilter(productId));

        if (sortBy != null)
        {
            request.Sorting
                .SortBy(new ProductAttributeSorting(ProductAttributeSorting.SortableAttribute.SalesPrice, sortBy == "priceLow" ? SortOrder.Ascending : SortOrder.Descending))
                .ThenBy(new ProductRelevanceSorting());
        }

        ProductSearchResponse result = await searcher.SearchAsync(request, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(new
        {
            Facets = result.Facets?.Items?.ToDictionary(x => x.Field.ToString(), facetResult =>
            {
                return facetResult switch
                {
                    CategoryFacetResult categoryFacetResult => categoryFacetResult.Available.Select(x => new FacetValue(x.Value.Id, x.Value.DisplayName, x.Hits, x.Selected)),
                    _ => null
                };
            }),
            result.Results,
            result.Hits
        }, JsonSerializerOptions, context.RequestAborted);
    }

    private static async Task RecommendPopular(
        HttpContext context)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        PopularProductsRequest request = new PopularProductsRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            new Currency(Thread.CurrentThread.CurrentUICulture),
            "HomePage",
            user,
            PopularityTypes.MostViewed,
            DateTimeOffset.UtcNow.AddDays(-30))
        {
            Settings = new ProductRecommendationRequestSettings
            {
                NumberOfRecommendations = 12,
                SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
            }
        };

        ProductRecommendationResponse result = await recommender.RecommendAsync(request, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Recommendations, JsonSerializerOptions, context.RequestAborted);
    }

    private static async Task RecommendPurchasedWith(
        HttpContext context,
        [FromQuery] string productId)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        PurchasedWithProductRequest request = new PurchasedWithProductRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            new Currency(Thread.CurrentThread.CurrentUICulture),
            "Product Page",
            user,
            new ProductAndVariantId(productId))
        {
            Settings = new ProductRecommendationRequestSettings
            {
                NumberOfRecommendations = 12,
                PrioritizeDiversityBetweenRequests = true,
                SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
            }
        };

        ProductRecommendationResponse result = await recommender.RecommendAsync(request, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Recommendations, JsonSerializerOptions, context.RequestAborted);
    }

    private static async Task RecommendViewedAfter(
        HttpContext context,
        [FromQuery] string productId)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ProductsViewedAfterViewingProductRequest request = new ProductsViewedAfterViewingProductRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            new Currency(Thread.CurrentThread.CurrentUICulture),
            "Product Page",
            user,
            new ProductAndVariantId(productId))
        {
            Settings = new ProductRecommendationRequestSettings
            {
                NumberOfRecommendations = 12,
                PrioritizeDiversityBetweenRequests = true,
                SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
            }
        };

        ProductRecommendationResponse result = await recommender.RecommendAsync(request, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Recommendations, JsonSerializerOptions, context.RequestAborted);
    }
}