using System;

namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>
/// Controls whether exports preserve omitted entity properties or replace the entity's information.
/// </summary>
public sealed class EntityUpdatePolicy
{
    private readonly bool _deltaUpdates;
    private readonly bool _fullRebuilds;

    private EntityUpdatePolicy(bool deltaUpdates, bool fullRebuilds)
    {
        _deltaUpdates = deltaUpdates;
        _fullRebuilds = fullRebuilds;
    }

    /// <summary>
    /// Replace supplied properties while preserving omitted properties during delta updates and full rebuilds.
    /// This is the default policy.
    /// </summary>
    public static EntityUpdatePolicy ReplaceProvidedProperties()
    {
        return new EntityUpdatePolicy(deltaUpdates: false, fullRebuilds: false);
    }

    /// <summary>
    /// Replace the entity's information, removing omitted Data and built-in properties. Applies to both
    /// delta updates and full rebuilds unless configured otherwise. Requires lifecycle synchronization.
    /// </summary>
    /// <param name="configure">
    /// Select which operations replace the entity. At least one operation must remain enabled.
    /// </param>
    public static EntityUpdatePolicy ClearAndReplace(Action<EntityUpdatePolicyOptions>? configure = null)
    {
        var options = new EntityUpdatePolicyOptions();
        configure?.Invoke(options);

        if (!options.DeltaUpdates && !options.FullRebuilds)
        {
            throw new ArgumentException(
                "ClearAndReplace must apply to delta updates or full rebuilds.",
                nameof(configure));
        }

        return new EntityUpdatePolicy(options.DeltaUpdates, options.FullRebuilds);
    }

    internal bool CanClear => _deltaUpdates || _fullRebuilds;

    internal bool Clears(bool fullRebuild)
    {
        return fullRebuild ? _fullRebuilds : _deltaUpdates;
    }
}
