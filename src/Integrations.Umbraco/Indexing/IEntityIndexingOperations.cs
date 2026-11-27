using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;

namespace Relewise.Integrations.Umbraco.Indexing;

internal interface IEntityIndexingOperations
{
    Task ApplyChangesAsync(ExportChange[] changes, CancellationToken token);

    Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        long? feedVersion = null,
        CancellationToken token = default);

    Task RebuildAsync(bool permanentlyDelete = false, CancellationToken token = default);
    Task DeleteAsync(IEnumerable<DeletedEntity> deleted, CancellationToken token);
}
