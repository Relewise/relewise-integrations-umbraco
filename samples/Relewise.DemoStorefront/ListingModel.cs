internal sealed class ListingModel
{
    public string View { get; init; } = "store";
    public bool SearchPage { get; init; }
    public string Path { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public string SuggestionsEndpoint { get; init; } = "";
    public Guid? Collection { get; init; }
    public bool Editorial { get; init; }
    public string Currency { get; init; } = "";
    public string Provider { get; init; } = "";
    public Dictionary<string, string[]> Parameters { get; init; } = [];
    public Dictionary<string, string> Labels { get; init; } = [];
    public ListingFacet[] Facets { get; init; } = [];
    public ListingChip[] Chips { get; init; } = [];
    public ListingCard[] Cards { get; init; } = [];
    public string Heading { get; init; } = "";
    public string ResultLabel { get; init; } = "";
    public string RangeLabel { get; init; } = "";
    public string ShowResultsLabel { get; init; } = "";
    public long Total { get; init; }
    public int Page { get; init; }
    public long LastPage { get; init; }
    public string PageLabel { get; init; } = "";
    public bool Updating { get; init; }
    public string ResetUrl { get; init; } = "";
    public string ClearQueryUrl { get; init; } = "";
    public string? PreviousUrl { get; init; }
    public string? NextUrl { get; init; }
}
