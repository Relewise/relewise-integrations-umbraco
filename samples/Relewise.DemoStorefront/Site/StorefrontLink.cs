using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Site;

/// <summary>A labelled navigation link with its active and enabled states.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public sealed record StorefrontLink(string Label, string Url, bool Active, bool Enabled = true);
