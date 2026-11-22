using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>Read-only description of a named search provider.</summary>
public class DashboardSearchProviderViewModel
{
    /// <summary>Registered provider alias.</summary>
    public string Alias { get; internal set; } = string.Empty;

    /// <summary>Content or Product search.</summary>
    public string EntityType { get; internal set; } = string.Empty;

    /// <summary>Content Document Type restrictions; empty means all mapped Content types.</summary>
    public IReadOnlyCollection<string> DocumentTypeAliases { get; internal set; } = [];

    /// <summary>Exported Document Type aliases selected by the provider's configured entity and restrictions.</summary>
    public IReadOnlyCollection<string> ResolvedDocumentTypeAliases { get; internal set; } = [];

    /// <summary>Configured analytics location or fallback, before per-request customization.</summary>
    public string DisplayedAtLocation { get; internal set; } = string.Empty;

    /// <summary>Whether the analytics location uses the integration's fallback label.</summary>
    public bool UsesFallbackLocation { get; internal set; }

    /// <summary>Whether results require a published Umbraco page.</summary>
    public bool RequirePublishedPage { get; internal set; }

    /// <summary>Whether a per-request customization callback is registered.</summary>
    public bool CustomRequest { get; internal set; }

    /// <summary>Whether segment selection is ignored instead of rejecting segment-specific queries.</summary>
    public bool IgnoreSegments { get; internal set; }
}
