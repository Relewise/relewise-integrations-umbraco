using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Relewise Package configuration model
/// </summary>
public class ConfigurationViewModel
{
    /// <summary>
    /// Contains all contentType aliases that are being tracked
    /// </summary>
    public IReadOnlyCollection<string> TrackedContentTypes { get; }

    /// <summary>
    /// Contains all contentType aliases that are being exported
    /// </summary>
    public IReadOnlyCollection<string> ExportedContentTypes { get; }

    /// <summary>
    /// Contains all named sets of Relewise client options
    /// </summary>
    public List<NamedOptionsViewObject> Named { get; }

    /// <summary>
    /// Indicates whether the Relewise content middleware is enabled
    /// </summary>
    public bool ContentMiddlewareEnabled { get; }

    /// <summary>Configured entity mappings, including child Variants.</summary>
    public IReadOnlyCollection<DashboardMappingViewModel> Mappings { get; internal set; } = [];

    /// <summary>Named Content and Product search providers.</summary>
    public IReadOnlyCollection<DashboardSearchProviderViewModel> SearchProviders { get; internal set; } = [];

    /// <summary>Relewise provider selected for ordinary published-content queries, if configured.</summary>
    public string? DefaultPublishedContentProviderAlias { get; internal set; }

    /// <summary>Connection used by the integration's export and view tracking client.</summary>
    public ClientOptionsViewObject? Connection { get; internal set; }

    /// <summary>Effective connections for the integration's tracking/export, search and entity-read clients.</summary>
    public IReadOnlyCollection<DashboardClientViewModel> IntegrationClients { get; internal set; } = [];

    /// <summary>
    /// Relewise Package configuration model
    /// </summary>
    public ConfigurationViewModel(IReadOnlyCollection<string> trackedContentTypes, IReadOnlyCollection<string> exportedContentTypes, List<NamedOptionsViewObject> named, bool contentMiddlewareEnabled)
    {
        TrackedContentTypes = trackedContentTypes;
        ExportedContentTypes = exportedContentTypes;
        Named = named;
        ContentMiddlewareEnabled = contentMiddlewareEnabled;
    }
}
