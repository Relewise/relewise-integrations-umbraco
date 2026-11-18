using System;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco;

/// <summary>Query settings for the single Product mapping.</summary>
public class ProductSearchProviderOptions
{
    internal ProductSearchProviderOptions()
    {
    }
    internal Action<ProductSearchRequest, SearchRequestContext>? Configure { get; private set; }
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
