using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Relewise.Integrations.Umbraco;

namespace Relewise.Umbraco.Application.Api;

public static class SearchApi
{
    private const string DisplayedAtLocation = "Search Overlay";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapSearchRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/global/search", Search);

        return builder;
    }

    private static async Task Search(
        HttpContext context,
        [FromQuery] string? q)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ProductSearchRequest productSearchRequest = new ProductSearchRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            new Currency(Thread.CurrentThread.CurrentUICulture),
            user,
            DisplayedAtLocation,
            q,
            skip: 0,
            take: 6)
        {
            Settings = new ProductSearchSettings
            {
                SelectedProductProperties = new SelectedProductPropertiesSettings().SetAllTo(true)
            }
        };

        SearchTermPredictionRequest predictionRequest = new SearchTermPredictionRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            DisplayedAtLocation,
            q ?? string.Empty,
            take: 5)
        {
            Settings = new SearchTermPredictionSettings
            {
                TargetEntityTypes = new List<EntityType> {EntityType.Content, EntityType.Product}
            }
        };

        ContentSearchRequest contentSearchRequest = new ContentSearchRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            DisplayedAtLocation,
            q ?? string.Empty,
            skip: 0,
            take: 5)
        {
            Settings = new ContentSearchSettings
            {
                SelectedContentProperties = new SelectedContentPropertiesSettings().SetAllTo(true)
            }
        };

        SearchResponseCollection? result = await searcher.BatchAsync(new SearchRequestCollection(productSearchRequest, predictionRequest, contentSearchRequest), context.RequestAborted);

        await context.Response.WriteAsJsonAsync(new
        {
            Products = result.Responses[0] as ProductSearchResponse,
            Predictions = result.Responses[1] as SearchTermPredictionResponse,
            Contents = result.Responses[2] as ContentSearchResponse,
        }, JsonSerializerOptions, context.RequestAborted);
    }
}