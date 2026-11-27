namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>
/// Controls whether publication and removal may change an entity's administrative state.
/// </summary>
public class AdministrativeActionPolicy
{
    internal bool Enabled { get; }

    private AdministrativeActionPolicy(bool enabled)
    {
        Enabled = enabled;
    }

    /// <summary>
    /// Follow publication, unpublication, and removal of the source document.
    /// </summary>
    public static AdministrativeActionPolicy Synchronize()
    {
        return new AdministrativeActionPolicy(enabled: true);
    }

    /// <summary>
    /// Export data without enabling, disabling, or deleting the entity.
    /// </summary>
    public static AdministrativeActionPolicy None()
    {
        return new AdministrativeActionPolicy(enabled: false);
    }
}
