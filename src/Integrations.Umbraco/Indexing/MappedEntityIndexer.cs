using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco;

internal sealed class MappedEntityIndexer(EntityIndexingQueue indexing) : IIndexer
{
    public Task AddOrUpdateAsync(string indexAlias, Guid id, UmbracoObjectTypes objectType, IEnumerable<Variation> variations, IEnumerable<IndexField> fields, ContentProtection? protection)
        => indexing.ExportAsync([id]);
    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids) => indexing.ExportAsync(ids);
    // Rebuild reconciliation belongs to the strategy, not a destructive reset of the remote Dataset.
    public Task ResetAsync(string indexAlias) => Task.CompletedTask;
    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        return Task.FromResult(new IndexMetadata(0, HealthStatus.Unknown, "Relewise"));
    }
}
