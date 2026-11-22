using System;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>Query settings for mapped Content documents.</summary>
public class ContentSearchProviderOptions
{
    internal ContentSearchProviderOptions()
    {
    }

    internal Action<ContentSearchRequest, SearchRequestContext>? Configure { get; private set; }

    /// <summary>Source Content Document Types to include; null includes all exported Content mappings.</summary>
    public string[]? DocumentTypeAliases { get; set; }

    /// <summary>Stable analytics/merchandising label. Defaults to Umbraco search: followed by the provider
    /// alias.</summary>
    public string? DisplayedAtLocation { get; set; }

    /// <summary>How to handle a requested segment. Defaults to rejecting unsupported segment queries.</summary>
    public UnsupportedSegmentBehavior UnsupportedSegmentBehavior { get; set; } = UnsupportedSegmentBehavior.Throw();

    /// <summary>Customize a fresh request for each execution. Mandatory access, mapping, identity, and paging
    /// constraints remain enforced.</summary>
    public void ConfigureRequest(Action<ContentSearchRequest, SearchRequestContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (Configure != null)
        {
            throw new InvalidOperationException("Request customization has already been configured.");
        }

        Configure = configure;
    }
}
