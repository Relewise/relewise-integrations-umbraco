namespace Relewise.DemoStorefront.Features.Browsing;

/// <summary>A displayed facet and its available choices.</summary>
public sealed record ListingFacet(string Name, string Label, ListingChoice[] Choices);
