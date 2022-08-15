using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Relewise.Integrations.Umbraco;

namespace Relewise.Umbraco.Application.Api;

public static class ContentApi
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapContentRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/content/search", Search);
        builder.MapGet("api/content/predict", Predict);
        builder.MapGet("api/content/recommend/popular", RecommendPopular);

        return builder;
    }

    private static async Task Search(HttpContext context, [FromQuery] string q)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ContentSearchResponse result = await searcher.SearchAsync(new ContentSearchRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            "Search Overlay",
            q,
            skip: 0,
            take: 10)
        {
            Settings = new ContentSearchSettings
            {
                SelectedContentProperties = new SelectedContentPropertiesSettings().SetAllTo(true)
            }
        }, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Results, JsonSerializerOptions);
    }

    private static async Task Predict(HttpContext context, [FromQuery] string q)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        SearchTermPredictionResponse result = await searcher.PredictAsync(new SearchTermPredictionRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            "Search Overlay",
            q,
            take: 10)
        {
            Settings = new SearchTermPredictionSettings
            {
                TargetEntityTypes = new List<EntityType> { EntityType.Content }
            }
        }, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Predictions, JsonSerializerOptions);
    }

    private static async Task RecommendPopular(HttpContext context)
    {
        IRecommender recommender = context.RequestServices.GetRequiredService<IRecommender>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ContentRecommendationResponse result = await recommender.RecommendAsync(new PopularContentsRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            "Frontend page",
            user,
            since: TimeSpan.FromDays(365))
        {
            Settings = new ContentRecommendationRequestSettings
            {
                NumberOfRecommendations = 6,
                SelectedContentProperties = new SelectedContentPropertiesSettings
                {
                    DisplayName = true,
                    DataKeys = new []
                    {
                        "url", 
                        "splashImage_Block"
                    }
                }
            },
            Filters = new FilterCollection(new ContentDataFilter(
                "contentTypeAlias", new ContainsCondition(new DataValue(new List<string>
                {
                    "blogEntry", 
                    "contentPage"
                }), ContainsCondition.CollectionArgumentEvaluationMode.Any)))
        }, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Recommendations.Chunk(2), JsonSerializerOptions);
    }
}