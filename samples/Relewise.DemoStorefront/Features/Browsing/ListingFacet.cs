using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Browsing;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal sealed record ListingFacet(string Name, string Label, ListingChoice[] Choices);
