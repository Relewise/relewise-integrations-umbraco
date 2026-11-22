namespace Relewise.DemoStorefront.Site;

/// <summary>A labelled navigation link with its active and enabled states.</summary>
public sealed record StorefrontLink(string Label, string Url, bool Active, bool Enabled = true);
