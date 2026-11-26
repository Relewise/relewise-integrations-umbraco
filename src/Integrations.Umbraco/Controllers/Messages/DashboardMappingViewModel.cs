using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Controllers.Messages;

/// <summary>
/// Read-only description of a configured entity mapping.
/// </summary>
public class DashboardMappingViewModel
{
    /// <summary>
    /// Source Umbraco Document Type alias.
    /// </summary>
    public string DocumentTypeAlias { get; internal set; } = string.Empty;

    /// <summary>
    /// Destination Relewise entity type.
    /// </summary>
    public string EntityType { get; internal set; } = string.Empty;

    /// <summary>
    /// Parent Product Document Type for a child Variant.
    /// </summary>
    public string? ParentDocumentTypeAlias { get; internal set; }

    /// <summary>
    /// Configured source of canonical entity IDs.
    /// </summary>
    public string IdSource { get; internal set; } = string.Empty;

    /// <summary>
    /// Whether published documents are exported.
    /// </summary>
    public bool Export { get; internal set; }

    /// <summary>
    /// Whether automatic views are configured for this mapping.
    /// </summary>
    public bool TrackViews { get; internal set; }

    /// <summary>
    /// Whether publication controls administrative entity state, including the parent Product's policy for Variants.
    /// </summary>
    public bool SynchronizeLifecycle { get; internal set; }

    /// <summary>
    /// Whether the mapping policy clears omitted properties during delta updates, before Content update-kind overrides.
    /// </summary>
    public bool ClearOnDeltaUpdates { get; internal set; }

    /// <summary>
    /// Whether the mapping policy clears omitted properties during full rebuilds, before legacy mapper overrides.
    /// </summary>
    public bool ClearOnFullRebuilds { get; internal set; }

    /// <summary>
    /// Whether existing automatic Content field mapping is selected.
    /// </summary>
    public bool AutoMap { get; internal set; }

    /// <summary>
    /// Whether a custom entity mapper is configured.
    /// </summary>
    public bool CustomMapper { get; internal set; }

    /// <summary>
    /// The registered entity mapper type name, including legacy mapper implementations.
    /// </summary>
    public string? CustomMapperTypeName { get; internal set; }

    /// <summary>
    /// Whether the custom mapper uses the legacy Content compatibility contract.
    /// </summary>
    public bool LegacyMapper { get; internal set; }

    /// <summary>
    /// Inspectable field mappings; custom mapper code is not evaluated.
    /// </summary>
    public IReadOnlyCollection<DashboardFieldMappingViewModel> Fields { get; internal set; } = [];

    /// <summary>
    /// Configured relationships and hierarchy behavior.
    /// </summary>
    public IReadOnlyCollection<string> Relationships { get; internal set; } = [];
}
