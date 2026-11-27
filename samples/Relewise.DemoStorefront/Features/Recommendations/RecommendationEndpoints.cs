namespace Relewise.DemoStorefront.Features.Recommendations;

internal static class RecommendationEndpoints
{
    public static void MapRecommendations(this WebApplication app)
    {
        app.MapGet("/{language:regex(^(en|da)$)}/recommendations/{key:guid}", StorefrontRecommendations.GetAsync);
    }
}
