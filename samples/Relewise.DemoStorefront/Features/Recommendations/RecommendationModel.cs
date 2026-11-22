namespace Relewise.DemoStorefront.Features.Recommendations;

/// <summary>
/// A recommendation endpoint and localized component labels.
/// </summary>
public sealed class RecommendationModel
{
    public string Endpoint { get; init; } = "";
    public Dictionary<string, string> Labels { get; init; } = [];
}
