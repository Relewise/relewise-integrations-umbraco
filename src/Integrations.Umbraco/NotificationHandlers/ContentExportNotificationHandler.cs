using System.Collections.Generic;
using System.Linq;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal sealed class ContentExportNotificationHandler(IExportSystem exports, ICoreScopeProvider scopes) :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>,
    INotificationHandler<ContentMovedNotification>,
    INotificationHandler<ContentMovedToRecycleBinNotification>
{
    public void Handle(ContentPublishedNotification notification) =>
        Queue(notification.PublishedEntities, ExportChangeImpact.RefreshWithDescendants);

    public void Handle(ContentUnpublishedNotification notification) =>
        Queue(notification.UnpublishedEntities, ExportChangeImpact.Remove);

    public void Handle(ContentMovedNotification notification) =>
        Queue(notification.MoveInfoCollection.Select(item => item.Entity), ExportChangeImpact.RefreshWithDescendants);

    public void Handle(ContentMovedToRecycleBinNotification notification) =>
        Queue(notification.MoveInfoCollection.Select(item => item.Entity), ExportChangeImpact.Remove);

    private void Queue(IEnumerable<IContent> contents, ExportChangeImpact impact)
    {
        if (!exports.UsesContentNotifications)
            return;
        ExportChange[] changes =
            [.. contents.Select(content => new ExportChange(content.Key, impact, content.Published))];
        if (scopes.Context is { } context)
        {
            // Wait for commit and published-cache updates; retain identities, not mutable IContent instances.
            List<ExportChange>? pending = context.Enlist(
                "Relewise:ContentExports",
                () => new List<ExportChange>(),
                (completed, items) =>
                {
                    if (completed && items != null)
                        exports.EnqueueChanges(items);
                });
            pending!.AddRange(changes);
        }
        else
        {
            exports.EnqueueChanges(changes);
        }
    }
}
