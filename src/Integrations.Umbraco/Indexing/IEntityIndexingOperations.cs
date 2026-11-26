using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Indexing;

internal interface IEntityIndexingOperations
{
    Task ApplyChangesAsync(ContentChange[] changes, CancellationToken token);

    Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        long? feedVersion = null,
        CancellationToken cancellationToken = default);

    Task RebuildAsync(bool permanentlyDelete = false, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<DeletedEntity> deleted, CancellationToken token);
}
