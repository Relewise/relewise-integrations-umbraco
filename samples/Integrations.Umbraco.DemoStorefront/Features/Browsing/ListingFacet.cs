namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Browsing;

/// <summary>
/// A displayed facet and its available choices.
/// </summary>
public sealed record ListingFacet(string Name, string Label, ListingChoice[] Choices);
