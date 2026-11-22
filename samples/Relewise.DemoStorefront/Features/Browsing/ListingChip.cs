using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Features.Browsing;

/// <summary>An applied filter and its removal link.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed record ListingChip(string Label, string Url);
