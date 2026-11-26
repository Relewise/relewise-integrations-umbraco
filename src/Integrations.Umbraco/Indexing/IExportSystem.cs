using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;

namespace Relewise.Integrations.Umbraco.Indexing;

// The selected system owns scheduling, discovery and rebuilds; no particular queue or source is required.
internal interface IExportSystem
{
    bool UsesContentNotifications { get; }

    Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? updateKind,
        long? version,
        CancellationToken cancellationToken);

    Task RebuildAsync(bool permanentlyDelete, CancellationToken cancellationToken);
    void EnqueueChanges(IEnumerable<ExportChange> changes);
    void EnqueueDeletion(DeletedEntity[] entities, Guid[] parents);
}
