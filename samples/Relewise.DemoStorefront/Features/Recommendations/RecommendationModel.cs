using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Recommendations;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal sealed class RecommendationModel
{
    public string Endpoint { get; init; } = "";
    public Dictionary<string, string> Labels { get; init; } = [];
}
