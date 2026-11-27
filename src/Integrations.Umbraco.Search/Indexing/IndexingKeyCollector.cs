using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Search.Indexing;

internal sealed class IndexingKeyCollector(ExportSourceBatch batch) : IIndexer
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
