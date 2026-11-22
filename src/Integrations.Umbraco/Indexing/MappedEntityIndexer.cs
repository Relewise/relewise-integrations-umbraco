using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed class MappedEntityIndexer(EntityIndexingQueue indexing, IndexMetadataService metadata) : IIndexer
{
    public Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        return indexing.ExportAsync([id]);
    }

    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        return indexing.ExportAsync(ids);
    }

    // Rebuild reconciliation belongs to the strategy, not a destructive reset of the remote Dataset.
    public Task ResetAsync(string indexAlias)
    {
        return Task.CompletedTask;
    }

    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        return metadata.GetMetadataAsync(indexAlias);
    }
}
