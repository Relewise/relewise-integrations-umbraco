namespace Relewise.Umbraco.Application.Models;

public class FacetValue
{
    public FacetValue(string value, string displayName, int hits, bool selected = false)
    {
        Value = value;
        DisplayName = displayName;
        Hits = hits;
        Selected = selected;
    }

    public string Value { get; set; }
    public string DisplayName { get; set; }
    public int Hits { get; set; }
    public bool Selected { get; set; }
}