using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Browsing;

/// <summary>A displayed facet and its available choices.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed record ListingFacet(string Name, string Label, ListingChoice[] Choices);
