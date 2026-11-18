using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco;

internal interface IEntityIndexingOperations
{
    Task ApplyChangesAsync(ContentChange[] changes, CancellationToken token);
    Task ExportAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default,
        ContentUpdate.UpdateKind? contentUpdateKind = null, long? feedVersion = null);
    Task RebuildAsync(bool permanentlyDelete = false, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<DeletedEntity> deleted, CancellationToken token);
}
