using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

namespace Relewise.UmbracoV9.Application.Api;

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

    private static async Task Search(HttpContext context)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        ContentSearchResponse result = await searcher.SearchAsync(new ContentSearchRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            "Search Overlay",
            context.Request.Query["q"].First(),
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

    private static async Task Predict(HttpContext context)
    {
        ISearcher searcher = context.RequestServices.GetRequiredService<ISearcher>();
        IRelewiseUserLocator userLocator = context.RequestServices.GetRequiredService<IRelewiseUserLocator>();
        User user = await userLocator.GetUser();

        SearchTermPredictionResponse result = await searcher.PredictAsync(new SearchTermPredictionRequest(
            new Language(Thread.CurrentThread.CurrentUICulture.Name),
            Currency.Undefined,
            user,
            "Search Overlay",
            context.Request.Query["q"].First(),
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
                SelectedContentProperties = new SelectedContentPropertiesSettings
                {
                    DisplayName = true,
                    DataKeys = new []{"url", "splashImage_Block" }
                }
            },
            Filters = new FilterCollection(new ContentDataFilter("ContentTypeAlias", new ContainsCondition(new DataValue(new List<string> {"blogEntry", "contentPage"}))))
        }, context.RequestAborted);

        await context.Response.WriteAsJsonAsync(result.Recommendations.Take(6).Chunk(2), JsonSerializerOptions);
    }
}