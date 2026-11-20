using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco;

internal sealed class IndexingKeyCollector : IIndexer
{
    internal HashSet<Guid> Keys { get; } = new();
    internal Dictionary<Guid, CoreSearchDocument> Documents { get; } = new();
    public Task AddOrUpdateAsync(string indexAlias, Guid id, UmbracoObjectTypes objectType, IEnumerable<Variation> variations, IEnumerable<IndexField> fields, ContentProtection? protection)
    {
        Keys.Add(id);
        Documents[id] = new CoreSearchDocument(variations.ToArray(), fields.ToArray(), protection);
        return Task.CompletedTask;
    }
    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        foreach (Guid id in ids)
        {
            Keys.Add(id);
            Documents.Remove(id);
        }
        return Task.CompletedTask;
    }
    public Task ResetAsync(string indexAlias) => Task.CompletedTask;
    public Task<IndexMetadata> GetMetadataAsync(string indexAlias) => Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
}
