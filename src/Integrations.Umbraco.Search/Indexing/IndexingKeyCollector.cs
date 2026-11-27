using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;
#endif
using Umbraco.Cms.Core.Services.Navigation;
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services;
#endif

namespace Relewise.Integrations.Umbraco.Search.Indexing;

internal sealed class IndexingKeyCollector(
    ExportSourceBatch batch,
    IDocumentNavigationQueryService navigation) : IIndexer
{
    public Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        Variation[] snapshot = [.. variations];
        batch.Keys.Add(id);
        batch.Documents[id] = new ExportSourceDocument(
            [.. snapshot.Select(variation => variation.Culture)],
            [
                .. snapshot.Where(variation => variation.Segment == null)
                    .Select(variation => variation.Culture?.ToLowerInvariant() ?? string.Empty)
                    .Distinct()
            ],
            protection == null ? null : [.. protection.AccessIds]);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        foreach (Guid id in ids)
        {
            batch.Keys.Add(id);
            batch.Documents.Remove(id);

            // Umbraco delegates branch removal to the indexer, including descendants still published themselves.
            if (navigation.TryGetDescendantsKeys(id, out IEnumerable<Guid> descendants))
            {
                foreach (Guid descendant in descendants)
                {
                    batch.Keys.Add(descendant);
                    batch.Documents.Remove(descendant);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task ResetAsync(string indexAlias)
    {
        return Task.CompletedTask;
    }

    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        return Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
    }
}
