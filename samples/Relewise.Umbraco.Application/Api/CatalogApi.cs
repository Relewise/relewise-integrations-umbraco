using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
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
using Relewise.Umbraco.Application.Infrastructure;
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
        builder.MapGet("api/catalog/recommend/products", RecommendProducts);
        builder.MapGet("api/catalog/recommend/basket", RecommendBasket);

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
        [FromQuery] string? productId,
        [FromQuery] string? country)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ProductSearchRequest request = new ProductSearchRequest(
            Taxonomy.Language,
            Taxonomy.Currency,
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

        request.Facets ??= new ProductFacetQuery();
        request.Filters ??= new FilterCollection();
        request.Sorting ??= new ProductSortBySpecification();

        request.Facets.AddCategory(CategorySelectionStrategy.ImmediateParent, category2Id != null ? new[] { category2Id } : null);
        request.Facets.AddDataString(DataSelectionStrategy.Product, "Country", country != null ? new[] { country } : null);

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
            Facets = new
            {
                Category = result.Facets.Category(CategorySelectionStrategy.ImmediateParent).Available.Select(x => new FacetValue(x.Value.Id, x.Value.DisplayName, x.Hits, x.Selected)),
                Country = result.Facets.DataString(DataSelectionStrategy.Product, "Country").Available.Select(x => new FacetValue(x.Value, x.Value, x.Hits, x.Selected))
            },
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
            Taxonomy.Language,
            Taxonomy.Currency,
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
            Taxonomy.Language,
            Taxonomy.Currency,
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
            Taxonomy.Language,
            Taxonomy.Currency,
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

    private static async Task RecommendProducts(
        HttpContext context,
        [FromQuery] string productId)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        Language language = new(Thread.CurrentThread.CurrentUICulture.Name);
        Currency currency = new(Thread.CurrentThread.CurrentUICulture);
        var displayedAt = "Product Page";
        ProductAndVariantId product = new(productId);
        ProductRecommendationRequestSettings settings = new()
        {
            NumberOfRecommendations = 12,
            PrioritizeDiversityBetweenRequests = true,
            SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
        };

        PurchasedWithProductRequest purchasedWith = new PurchasedWithProductRequest(language, currency, displayedAt, user, product)
        {
            Settings = settings
        };

        ProductsViewedAfterViewingProductRequest viewedAfter = new ProductsViewedAfterViewingProductRequest(language, currency, displayedAt, user, product)
        {
            Settings = settings
        };

        ProductRecommendationResponseCollection? result = await recommender.RecommendAsync(new ProductRecommendationRequestCollection(true, viewedAfter, purchasedWith), context.RequestAborted);

        await context.Response.WriteAsJsonAsync(new
            {
                ViewedAfter = result.Responses[0],
                PurchasedWith = result.Responses[1]
            },
            JsonSerializerOptions,
            context.RequestAborted);
    }

    private static async Task RecommendBasket(
        HttpContext context,
        BasketRecommendation? request)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        Language language = new(Thread.CurrentThread.CurrentUICulture.Name);
        Currency currency = new(Thread.CurrentThread.CurrentUICulture);
        var displayedAt = "Basket Page";
        ProductRecommendationRequestSettings settings = new()
        {
            NumberOfRecommendations = 12,
            PrioritizeDiversityBetweenRequests = true,
            SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
        };

        ProductRecommendationResponse result;
        if (request is {ProductIds: { }} && request.ProductIds.Any())
        {
            var relewiseRequest = new PurchasedWithMultipleProductsRequest(language, currency, displayedAt, user, request.ProductIds.Select(x => new ProductAndVariantId(x)).ToArray())
            {
                Settings = settings
            };

            result = await recommender.RecommendAsync(relewiseRequest, context.RequestAborted);
        }
        else
        {
            var relewiseRequest = new PurchasedWithCurrentCartRequest(language, currency, displayedAt, user)
            {
                Settings = settings
            };

            result = await recommender.RecommendAsync(relewiseRequest, context.RequestAborted);
        }

        await context.Response.WriteAsJsonAsync(result, JsonSerializerOptions, context.RequestAborted);
    }

    public class BasketRecommendation
    {
        public string[]? ProductIds { get; set; }

        public static ValueTask<BasketRecommendation?> BindAsync(HttpContext context)
        {
            const string productIds = "productIds";

            if(context.Request.Query.Count == 0)
                return ValueTask.FromResult<BasketRecommendation?>(null);

            var result = new BasketRecommendation
            {
                ProductIds = context.Request.Query[productIds],
            };

            return ValueTask.FromResult<BasketRecommendation?>(result);
        }
    }
}