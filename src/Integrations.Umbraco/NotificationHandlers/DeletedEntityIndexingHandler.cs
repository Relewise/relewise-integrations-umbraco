using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

// Capture terminal deletions while IContent still carries the configured ID, then use the shared writer after commit.
internal sealed class DeletedEntityIndexingHandler(
    RelewiseUmbracoConfiguration configuration,
    IContentService content,
    ICoreScopeProvider scopes,
    EntityIndexingQueue indexing,
    ILogger<DeletedEntityIndexingHandler> logger) : INotificationHandler<ContentDeletedNotification>
{
    public void Handle(ContentDeletedNotification notification)
    {
        var work = new RemovalWork();

        foreach (IContent deleted in notification.DeletedEntities)
        {
            if (!configuration.Mappings.TryGetValue(deleted.ContentType.Alias, out MappingDefinition? mapping) ||
                !mapping.Export)
            {
                continue;
            }

            if (mapping.EntityType == typeof(ProductVariant))
            {
                IContent? parent = content.GetById(deleted.ParentId);

                if (parent != null)
                {
                    work.Parents.Add(parent.Key);
                }

                continue;
            }

            try
            {
                work.Entities.Add(
                    new DeletedEntity(
                        mapping.EntityType,
                        configuration.GetContentId(deleted),
                        deleted.Key,
                        mapping.AdministrativeActions.Enabled));
            }
            catch (InvalidOperationException error)
            {
                // Deleting an invalid draft must remain possible; a full export can reconcile its previous identity.
                logger.LogWarning(
                    error,
                    "Could not resolve the deleted node {NodeKey}; rebuild Relewise exports to reconcile " +
                    "its previous identity.",
                    deleted.Key);
            }
        }

        if (work.Entities.Count == 0 && work.Parents.Count == 0)
        {
            return;
        }

        if (scopes.Context is { } context)
        {
            RemovalWork? pending = context.Enlist(
                "Relewise:DeletedSources",
                () => new RemovalWork(),
                (completed, items) =>
                {
                    if (completed && items != null)
                    {
                        Queue(items);
                    }
                });

            pending!.Entities.AddRange(work.Entities);
            pending.Parents.UnionWith(work.Parents);
        }
        else
        {
            Queue(work);
        }
    }

    private void Queue(RemovalWork work)
    {
        DeletedEntity[] entities = [.. work.Entities.DistinctBy(entity => (entity.EntityType, entity.Id))];
        Guid[] parents = [.. work.Parents];

        indexing.EnqueueDeletion(entities, parents);
    }

    private sealed class RemovalWork
    {
        internal List<DeletedEntity> Entities { get; } = new();
        internal HashSet<Guid> Parents { get; } = new();
    }
}
