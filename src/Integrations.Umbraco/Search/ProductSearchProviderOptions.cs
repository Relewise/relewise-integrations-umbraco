using System;
using System.Collections.Generic;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco;

/// <summary>Query settings for the single Product mapping.</summary>
public class ProductSearchProviderOptions
{
    internal ProductSearchProviderOptions()
    {
    }
    internal Action<ProductSearchRequest, SearchRequestContext>? Configure { get; private set; }
    internal Dictionary<string, string> BooleanDataFields { get; } = new(StringComparer.Ordinal);
    /// <summary>Bind an Umbraco keyword query field directly to Boolean Product Data, without an indexed search projection.</summary>
    /// <param name="fieldName">The field used by Umbraco KeywordFilter and KeywordFacet queries.</param>
    /// <param name="dataKey">The Relewise Data key; defaults to <paramref name="fieldName"/>.</param>
    /// <remarks>Filter values must be true or false. Missing Data matches neither positive value; negated filters include missing values. This binding does not export Data or support sorting.</remarks>
    public void MapBooleanData(string fieldName, string? dataKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        dataKey ??= fieldName;
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKey);
        if (!BooleanDataFields.TryAdd(fieldName, dataKey))
        {
            throw new InvalidOperationException($"Boolean query field '{fieldName}' has already been bound.");
        }
    }
    /// <summary>Require an available published Umbraco page. Defaults to true; disabling does not change Product administrative state.</summary>
    /// <remarks>Callers disabling this requirement must handle results without a published page.</remarks>
    public bool RequirePublishedPage { get; set; } = true;
    /// <summary>Stable analytics/merchandising label. Defaults to Umbraco search: followed by the provider alias.</summary>
    public string? DisplayedAtLocation { get; set; }
    /// <summary>How to handle a requested segment. Defaults to rejecting unsupported segment queries.</summary>
    public UnsupportedSegmentBehavior UnsupportedSegmentBehavior { get; set; } = UnsupportedSegmentBehavior.Throw();
    /// <summary>Customize a fresh request for each execution. Mandatory access, mapping, identity, and paging constraints remain enforced.</summary>
    public void ConfigureRequest(Action<ProductSearchRequest, SearchRequestContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (Configure != null)
        {
            throw new InvalidOperationException("Request customization has already been configured.");
        }

        Configure = configure;
    }
}
