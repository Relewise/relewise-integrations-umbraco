using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Mapping;
using static Relewise.Integrations.Umbraco.Indexing.EntityExportDelivery;

namespace Relewise.Integrations.Umbraco.Indexing;

// Builds SDK payloads only. Export systems decide what to discover, when to send and when to reconcile.
internal static class EntityExportRequests
{
    public static List<Trackable> Updates(IEnumerable<MappedEntity> desired)
    {
        var updates = new List<Trackable>();

        foreach (MappedEntity entity in desired)
        {
            updates.Add(entity.Update());

            if (entity.Synchronize)
            {
                updates.Add(
                    Administrative(
                        entity.Entity.GetType(),
                        new FilterCollection(
                            DataFilter(
                                entity.Entity.GetType(),
                                RelewiseDataKeys.NodeKey,
                                new EqualsCondition(entity.NodeKey.ToString("D"))),
                            DataFilter(
                                entity.Entity.GetType(),
                                RelewiseDataKeys.DocumentTypeAlias,
                                new EqualsCondition(entity.DocumentTypeAlias))),
                        delete: false,
                        enable: true));
            }
        }

        return updates;
    }

    public static IEnumerable<EntityReconciliationRequest> Reconcile(
        RelewiseUmbracoConfiguration configuration,
        IEnumerable<(Guid[] Keys, bool Delete)> removals,
        long? version)
    {
        (Guid[] Keys, bool Delete)[] batches = [.. removals];
        foreach (IGrouping<(Type EntityType, bool Enabled), MappingDefinition> group in configuration.Mappings
                     .Where(mapping => mapping.Export && mapping.EntityType != typeof(ProductVariant))
                     .GroupBy(mapping => (mapping.EntityType, mapping.AdministrativeActions.Enabled)))
        {
            Type type = group.Key.EntityType;
            foreach ((Guid[] Keys, bool Delete) removal in batches)
            {
                var filters = new FilterCollection(
                    DataFilter(type, RelewiseDataKeys.NodeKey, new HasValueCondition()),
                    new OrFilter(
                    [
                        .. group.Select(mapping => DataFilter(
                            type,
                            RelewiseDataKeys.DocumentTypeAlias,
                            new EqualsCondition(mapping.DocumentTypeAlias)))
                    ]));
                filters.Add(
                    version.HasValue
                        ? DataFilter(
                            type,
                            RelewiseDataKeys.Version,
                            new EqualsCondition(version.Value, negated: true),
                            filterOutIfKeyIsNotFound: false)
                        : new OrFilter(
                        [
                            .. removal.Keys.Select(key => DataFilter(
                                type,
                                RelewiseDataKeys.NodeKey,
                                new EqualsCondition(key.ToString("D"))))
                        ]));
                yield return new EntityReconciliationRequest(type, filters, group.Key.Enabled, removal.Delete);
            }
        }
    }
}
