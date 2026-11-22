using JetBrains.Annotations;

namespace Relewise.DemoStorefront.Site;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal sealed record StorefrontLink(string Label, string Url, bool Active, bool Enabled = true);
