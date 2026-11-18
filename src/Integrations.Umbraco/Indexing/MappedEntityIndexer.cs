using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco;

internal sealed class MappedEntityIndexer(EntityIndexingService indexing, IServiceScopeFactory scopes, RelewiseUmbracoConfiguration configuration) : IIndexer
{
    public Task AddOrUpdateAsync(string indexAlias, Guid id, UmbracoObjectTypes objectType, IEnumerable<Variation> variations, IEnumerable<IndexField> fields, ContentProtection? protection)
        => indexing.ExportAsync([id]);
    public Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids) => indexing.ExportAsync(ids);
    // Rebuild reconciliation belongs to the strategy, not a destructive reset of the remote Dataset.
    public Task ResetAsync(string indexAlias) => Task.CompletedTask;
    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        if (!configuration.Mappings.Values.Any(mapping => mapping.Export)) return Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
        using IServiceScope scope = scopes.CreateScope();
        ITracker tracker = scope.ServiceProvider.GetRequiredService<IRelewiseClientFactory>().GetClient<ITracker>(Constants.NamedClientName);
        var records = EntityIndexingService.ReadRecords(scope.ServiceProvider.GetRequiredService<IKeyValueService>(), "Relewise:Entity:" + tracker.DatasetId.ToString("N") + ":");
        IEnumerable<IndexedEntity> selected = records.Values.Where(record => record.Active);
        if (configuration.SearchProviders.Providers.TryGetValue(indexAlias, out SearchProviderDefinition? provider))
        {
            selected = selected.Where(record => record.EntityType == provider.EntityType.Name);
            if (provider.ContentOptions?.DocumentTypeAliases is { } aliases)
                selected = selected.Where(record => aliases.Contains(record.DocumentTypeAlias, StringComparer.OrdinalIgnoreCase));
        }
        int count = selected.Count();
        return Task.FromResult(new IndexMetadata(count, count == 0 ? HealthStatus.Empty : HealthStatus.Healthy, "Relewise"));
    }
}

// A distinct strategy type isolates the background work from Examine's strategy group.
internal sealed class MappedEntityChangeStrategy(EntityIndexingService indexing, IPublishedContentChangeStrategy publishedStrategy) : IContentChangeStrategy
{
    public async Task HandleAsync(IEnumerable<ContentIndexInfo> indexInfos, IEnumerable<ContentChange> changes, CancellationToken cancellationToken)
    {
        IndexingKeyCollector collector = await CollectChangesAsync(indexInfos, changes, cancellationToken);
        if (collector.Keys.Count > 0) await indexing.ExportAsync(collector.Keys, cancellationToken);
    }
    internal async Task<IndexingKeyCollector> CollectChangesAsync(IEnumerable<ContentIndexInfo> indexInfos, IEnumerable<ContentChange> changes, CancellationToken cancellationToken)
    {
        ContentChange[] requested = changes.ToArray();
        var collector = new IndexingKeyCollector();
        foreach (ContentIndexInfo indexInfo in indexInfos)
            await publishedStrategy.HandleAsync([new ContentIndexInfo(indexInfo.IndexAlias, indexInfo.ContainedObjectTypes, collector)], requested, cancellationToken);
        // Permanent deletion of already-unpublished content is emitted on the draft flow only.
        // Reconcile that removal through the same writer, without exporting draft saves.
        collector.Keys.UnionWith(requested.Where(change => change.ObjectType == UmbracoObjectTypes.Document && change.ChangeImpact == ChangeImpact.Remove).Select(change => change.Id));
        return collector;
    }
    public Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken cancellationToken)
        => indexing.RebuildAsync(cancellationToken: cancellationToken);
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
        foreach (Guid id in ids) Keys.Add(id);
        return Task.CompletedTask;
    }
    public Task ResetAsync(string indexAlias) => Task.CompletedTask;
    public Task<IndexMetadata> GetMetadataAsync(string indexAlias) => Task.FromResult(new IndexMetadata(0, HealthStatus.Empty, "Relewise"));
}

internal sealed record CoreSearchDocument(Variation[] Variations, IndexField[] Fields, ContentProtection? Protection);
