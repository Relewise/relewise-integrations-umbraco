using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.HostedServices;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed class NotificationExportSystem : IExportSystem
{
    private readonly EntityIndexingQueue _queue;
    public bool UsesContentNotifications => true;

    public NotificationExportSystem(
        NotificationExportOperations operations,
        IBackgroundTaskQueue queue,
        IHostApplicationLifetime lifetime,
        ILogger<EntityIndexingQueue> logger)
    {
        _queue = new EntityIndexingQueue(operations, queue, lifetime, logger);
    }

    public Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? updateKind,
        long? version,
        CancellationToken token) => _queue.ExportAsync(keys, updateKind, version, token);

    public Task RebuildAsync(bool permanentlyDelete, CancellationToken token) =>
        _queue.RebuildAsync(permanentlyDelete, token);

    public void EnqueueChanges(IEnumerable<ExportChange> changes) => _queue.EnqueueChanges(changes);

    public void EnqueueDeletion(DeletedEntity[] entities, Guid[] parents) => _queue.EnqueueDeletion(entities, parents);
}
