namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>
/// Selects the export operations that use ClearAndReplace. Disabled operations use ReplaceProvidedProperties.
/// </summary>
public sealed class EntityUpdatePolicyOptions
{
    /// <summary>
    /// Replace the entity's information during publication changes and explicit exports of selected nodes.
    /// Defaults to true.
    /// </summary>
    public bool DeltaUpdates { get; set; } = true;

    /// <summary>
    /// Replace the entity's information during a full rebuild or ExportAllAsync. Defaults to true.
    /// </summary>
    public bool FullRebuilds { get; set; } = true;
}
