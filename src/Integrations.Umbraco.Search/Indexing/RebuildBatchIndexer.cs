using System;
using System.Collections.Generic;
using System.Threading;
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

internal sealed class RebuildBatchIndexer(
    IDocumentNavigationQueryService navigation,
    Func<ExportSourceBatch, CancellationToken, Task> write,
    CancellationToken token) : IIndexer
{
    private const int BatchSize = 100;
    private ExportSourceBatch _batch = new();
    private readonly HashSet<Guid> _removed = [];

    public IReadOnlyCollection<Guid> Removed => _removed;

    public async Task AddOrUpdateAsync(
        string indexAlias,
        Guid id,
        UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations,
        IEnumerable<IndexField> fields,
        ContentProtection? protection)
    {
        token.ThrowIfCancellationRequested();
        _removed.Remove(id);
        _batch.Keys.Add(id);
        _batch.Documents[id] = IndexingKeyCollector.CreateDocument(variations, protection);

        if (_batch.Documents.Count >= BatchSize)
        {
            await FlushAsync();
        }
    }

    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        token.ThrowIfCancellationRequested();

        foreach (Guid id in ids)
        {
            _removed.Add(id);
            _batch.Documents.Remove(id);
            _batch.Keys.Remove(id);

            if (!navigation.TryGetDescendantsKeys(id, out IEnumerable<Guid> descendants))
                continue;

            foreach (Guid descendant in descendants)
            {
                token.ThrowIfCancellationRequested();
                _removed.Add(descendant);
                _batch.Documents.Remove(descendant);
                _batch.Keys.Remove(descendant);
            }
        }

        return Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        token.ThrowIfCancellationRequested();
        if (_batch.Documents.Count == 0)
            return;

        ExportSourceBatch batch = _batch;
        _batch = new ExportSourceBatch();
        await write(batch, token);
    }

    public Task ResetAsync(string indexAlias) => Task.CompletedTask;

    public Task<IndexMetadata> GetMetadataAsync(string indexAlias) =>
        Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
}
