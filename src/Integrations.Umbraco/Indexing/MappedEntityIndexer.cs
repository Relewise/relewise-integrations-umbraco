using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

// A distinct strategy type isolates the background work from Examine's strategy group.
internal sealed class MappedEntityChangeStrategy(EntityIndexingQueue indexing) : IContentChangeStrategy
{
    public Task HandleAsync(IEnumerable<ContentIndexInfo> indexInfos, IEnumerable<ContentChange> changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var objectTypes = indexInfos.SelectMany(index => index.ContainedObjectTypes).ToHashSet();
        indexing.EnqueueChanges(changes.Where(change => objectTypes.Contains(change.ObjectType)
            && (change.ContentState == ContentState.Published || change.ChangeImpact == ChangeImpact.Remove)));
        return Task.CompletedTask;
    }
    public Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken cancellationToken)
        => indexing.RebuildAsync(token: cancellationToken);
}

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
        foreach (Guid id in ids) { Keys.Add(id); Documents.Remove(id); }
        return Task.CompletedTask;
    }
    public Task ResetAsync(string indexAlias) => Task.CompletedTask;
    public Task<IndexMetadata> GetMetadataAsync(string indexAlias) => Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
}

internal sealed record CoreSearchDocument(Variation[] Variations, IndexField[] Fields, ContentProtection? Protection);
