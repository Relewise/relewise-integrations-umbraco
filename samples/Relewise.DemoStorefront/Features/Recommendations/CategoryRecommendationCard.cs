using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Recommendations;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal sealed record CategoryRecommendationCard(
    Guid Key,
    string EntityId,
    string Url,
    string Title,
    string ImageHtml);
