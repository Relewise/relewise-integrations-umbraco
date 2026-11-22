namespace Relewise.DemoStorefront.Features.Browsing;

/// <summary>A facet choice with its result count and selection state.</summary>
public sealed record ListingChoice(string Value, string Label, long Count, bool Selected, string? QueryName = null);
