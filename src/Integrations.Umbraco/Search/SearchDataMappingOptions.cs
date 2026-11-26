namespace Relewise.Integrations.Umbraco.Search;

/// <summary>
/// Identifies the existing Relewise Data value used by a query field. This does not export Data.
/// </summary>
public class SearchDataMappingOptions
{
    internal SearchDataMappingOptions(string fieldName)
    {
        DataKey = fieldName;
    }

    /// <summary>
    /// The stored Relewise Data key; defaults to the query field name. Keys are case sensitive.
    /// </summary>
    public string DataKey { get; set; }
}
