using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Filters;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Persistence.Repositories;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Web;
using Content = Relewise.Client.DataTypes.Content;
using UmbracoContextReference = global::Umbraco.Cms.Core.UmbracoContextReference;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco;

// All export entry points share a renewable database lease without holding content/database locks across network calls.
internal sealed class EntityIndexingService(IServiceScopeFactory scopeFactory, RelewiseUmbracoConfiguration configuration)
{
    internal const string IndexAlias = "Relewise_MappedEntities";
    private const string RecordPrefix = "Relewise:Entity:";

    internal async Task ExportAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default, ContentUpdate.UpdateKind? contentUpdateKind = null, long? feedVersion = null)
        => await RunAsync(keys, false, false, cancellationToken, contentUpdateKind, feedVersion);
    internal async Task RebuildAsync(bool permanentlyDelete = false, CancellationToken cancellationToken = default)
    {
        if (!configuration.Mappings.Values.Any(mapping => mapping.Export)) return;
        await using AsyncServiceScope services = scopeFactory.CreateAsyncScope();
        var collector = new IndexingKeyCollector();
        await services.ServiceProvider.GetRequiredService<IPublishedContentChangeStrategy>()
            .RebuildAsync(new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], collector), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await RunAsync(collector.Keys, true, permanentlyDelete, cancellationToken);
    }

    private async Task RunAsync(IEnumerable<Guid> requestedKeys, bool rebuild, bool permanentlyDelete, CancellationToken cancellationToken, ContentUpdate.UpdateKind? contentUpdateKind = null, long? feedVersion = null)
    {
        if (!configuration.Mappings.Values.Any(mapping => mapping.Export)) return;
        await using AsyncServiceScope services = scopeFactory.CreateAsyncScope();
        IScopeProvider scopes = services.ServiceProvider.GetRequiredService<IScopeProvider>();
        IKeyValueService store = services.ServiceProvider.GetRequiredService<IKeyValueService>();
        // Commit the site identifier only after successful work, together with its identity associations.
        ITracker tracker = services.ServiceProvider.GetRequiredService<IRelewiseClientFactory>().GetClient<ITracker>(Constants.NamedClientName);
        await using var lease = await IndexingLease.AcquireAsync(scopeFactory, tracker.DatasetId, cancellationToken);
        cancellationToken = lease.CancellationToken;
        string siteId = store.GetValue("Relewise:SiteId") ?? Guid.NewGuid().ToString("N");
        string prefix = RecordPrefix + tracker.DatasetId.ToString("N") + ":";
        Dictionary<Guid, IndexedEntity> previous = ReadRecords(store, prefix);
        foreach (IndexedEntity record in previous.Values)
            if (configuration.Mappings.TryGetValue(record.DocumentTypeAlias, out MappingDefinition? configured)
                && configured.EntityType.Name == record.EntityType)
                record.Synchronize = configured.AdministrativeActions.Enabled;
        using UmbracoContextReference context = services.ServiceProvider.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        IContentService contentService = services.ServiceProvider.GetRequiredService<IContentService>();
        var affected = new HashSet<Guid>(requestedKeys);
        if (rebuild)
        {
            foreach (Guid key in previous.Keys) affected.Add(key);
        }
        else
        {
            // Expand retained source dependencies, including branches removed from the published cache.
            bool added;
            do
            {
                added = false;
                foreach (IndexedEntity record in previous.Values)
                    if (!affected.Contains(record.NodeKey) && record.Dependencies.Any(affected.Contains))
                        added |= affected.Add(record.NodeKey);
            }
            while (added);
        }
        // Collect current core indexing metadata for every desired document, including dependent Products.
        var searchDocuments = new IndexingKeyCollector();
        await services.ServiceProvider.GetRequiredService<IPublishedContentChangeStrategy>().HandleAsync(
            [new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], searchDocuments)],
            affected.Select(key => ContentChange.Document(key, ChangeImpact.Refresh, ContentState.Published)), cancellationToken);
        affected.UnionWith(searchDocuments.Keys);
        var mapper = new EntityMapper(configuration);
        await using var operation = new MappingOperation(services.ServiceProvider, cancellationToken);
        long version = feedVersion ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var desired = new Dictionary<Guid, MappedEntity>();
        var claims = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (IndexedEntity record in previous.Values)
            claims[Claim(record.EntityType, record.Id)] = record.NodeKey;
        var transferredMetadata = new Dictionary<Guid, string[]>();
        void ClaimSource(MappedEntity entity, string? parentId = null)
        {
            string identity = Claim(entity.Entity.GetType().Name, entity.Id, parentId);
            if (claims.TryGetValue(identity, out Guid other) && other != entity.NodeKey && previous.ContainsKey(other)
                && !desired.ContainsKey(other) && contentService.GetById(other) == null)
            {
                // A permanently removed source may be replaced by another node addressing the same entity.
                affected.Add(other);
                transferredMetadata[entity.NodeKey] = previous[other].SearchDataKeys;
                claims.Remove(identity);
            }
            AddClaim(entity, claims, parentId);
        }
        foreach (Guid key in affected.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            IContent? editable = contentService.GetById(key);
            if (editable == null || !Available(editable, contentService)) continue;
            IPublishedContent? published = context.UmbracoContext.Content?.GetById(key);
            if (published == null || !configuration.Mappings.TryGetValue(published.ContentType.Alias, out MappingDefinition? mapping) || !mapping.Export) continue;
            if (mapping.EntityType == typeof(ProductVariant))
            {
                IPublishedContent? parent = published.Parent();
                if (parent == null || !configuration.Mappings.TryGetValue(parent.ContentType.Alias, out var parentMapping)
                    || parentMapping.EntityType != typeof(Product) || parentMapping.Variant != mapping)
                    throw new InvalidOperationException($"Variant '{published.Key}' must be a direct child of the mapped Product Document Type.");
                continue;
            }
            if (!searchDocuments.Documents.TryGetValue(key, out CoreSearchDocument? document) || document.Variations.Length == 0) continue;
            MappedEntity mapped = await mapper.MapAsync(published, mapping, operation, siteId, version, contentUpdateKind: contentUpdateKind, publishedCultures: EffectiveCultures(document));
            ClaimSource(mapped);
            foreach (MappedEntity variant in mapped.Variants) ClaimSource(variant, mapped.Id);
            desired.Add(key, mapped);
        }
        // A first Variant publication must refresh its parent even before an association has been persisted.
        foreach (Guid key in affected.ToArray())
        {
            IPublishedContent? published = context.UmbracoContext.Content?.GetById(key);
            if (published == null || !configuration.Mappings.TryGetValue(published.ContentType.Alias, out var mapping)
                || mapping.EntityType != typeof(ProductVariant)) continue;
            IPublishedContent? parent = published.Parent();
            if (parent == null || desired.ContainsKey(parent.Key) || !configuration.Mappings.TryGetValue(parent.ContentType.Alias, out var parentMapping) || !parentMapping.Export) continue;
            if (!searchDocuments.Documents.ContainsKey(parent.Key))
                await services.ServiceProvider.GetRequiredService<IPublishedContentChangeStrategy>().HandleAsync(
                    [new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], searchDocuments)],
                    [ContentChange.Document(parent.Key, ChangeImpact.Refresh, ContentState.Published)], cancellationToken);
            if (!searchDocuments.Documents.TryGetValue(parent.Key, out CoreSearchDocument? document) || document.Variations.Length == 0) continue;
            MappedEntity mapped = await mapper.MapAsync(parent, parentMapping, operation, siteId, version, contentUpdateKind: contentUpdateKind, publishedCultures: EffectiveCultures(document));
            ClaimSource(mapped);
            foreach (MappedEntity variant in mapped.Variants) ClaimSource(variant, mapped.Id);
            desired.Add(parent.Key, mapped);
        }
        foreach (MappedEntity entity in desired.Values)
        {
            if (!searchDocuments.Documents.TryGetValue(entity.NodeKey, out CoreSearchDocument? document))
                throw new InvalidOperationException($"Core indexing did not produce a published document for '{entity.NodeKey}'.");
            if (entity.Entity is Content or Product) CoreSearchMetadata.Apply(entity, document);
        }
        var batch = new List<Trackable>();
        var next = new Dictionary<Guid, IndexedEntity>(previous);
        var removed = new HashSet<Guid>();
        var desiredIdentities = desired.Values.Select(entity => Claim(entity.Entity.GetType().Name, entity.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in desired)
        {
            MappedEntity mapped = entry.Value;
            IndexedEntity record = IndexedEntity.From(mapped);
            if (previous.TryGetValue(entry.Key, out IndexedEntity? old))
            {
                if (old.Id != record.Id || old.EntityType != record.EntityType) AddAdministrative(batch, old, true, false);
                foreach (string key in old.SearchDataKeys)
                    if (!EntityMapper.Data(mapped.Entity).ContainsKey(key)) EntityMapper.Data(mapped.Entity)[key] = null;
            }
            if (transferredMetadata.TryGetValue(mapped.NodeKey, out string[]? formerKeys))
                foreach (string key in formerKeys)
                    if (!EntityMapper.Data(mapped.Entity).ContainsKey(key)) EntityMapper.Data(mapped.Entity)[key] = null;
            batch.Add(mapped.Update());
            AddAdministrative(batch, record, false, true);
            next[entry.Key] = record;
        }
        foreach (var entry in previous)
        {
            if (!affected.Contains(entry.Key) || desired.ContainsKey(entry.Key)) continue;
            IContent? current = contentService.GetById(entry.Key);
            bool delete = current == null || (rebuild && permanentlyDelete);
            bool reassigned = current == null && desiredIdentities.Contains(Claim(entry.Value.EntityType, entry.Value.Id));
            if (!reassigned && entry.Value.Active && entry.Value.EntityType is nameof(Content) or nameof(Product))
            {
                // Source-page availability is independent of the externally controlled entity's state.
                Type type = entry.Value.EntityType == nameof(Content) ? typeof(Content) : typeof(Product);
                object entity = EntityMapper.Create(type, entry.Value.Id);
                EntityMapper.Data(entity)["rwUmbracoPublished"] = false;
                batch.Add(new MappedEntity(entry.Key, entry.Value.DocumentTypeAlias, entry.Value.Id, entity, false).Update());
            }
            if (!reassigned) AddAdministrative(batch, entry.Value, delete, false);
            if (current == null)
            {
                next.Remove(entry.Key);
                removed.Add(entry.Key);
                continue;
            }
            // Retain disabled identities so later deletion/ID changes still address the original entity.
            entry.Value.Active = false;
            next[entry.Key] = entry.Value;
        }
        lease.EnsureOwned();
        if (batch.Count > 0) await tracker.TrackAsync(batch, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        lease.EnsureOwned();
        using IScope scope = scopes.CreateScope();
        scope.EagerWriteLock(global::Umbraco.Cms.Core.Constants.Locks.KeyValues);
        var repository = services.ServiceProvider.GetRequiredService<IKeyValueRepository>();
        foreach (Guid key in removed)
            if (repository.Get(prefix + key.ToString("N")) is { } value) repository.Delete(value);
        foreach (var entry in next) store.SetValue(prefix + entry.Key.ToString("N"), JsonSerializer.Serialize(entry.Value));
        var capabilities = next.Values.Where(record => record.Active).GroupBy(record => record.DocumentTypeAlias)
            .ToDictionary(group => group.Key, group => group.SelectMany(record => record.UnsupportedCoreFields).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        store.SetValue(CoreSearchMetadata.CapabilitiesKey(tracker.DatasetId), JsonSerializer.Serialize(capabilities));
        store.SetValue("Relewise:SiteId", siteId);
        scope.Complete();
    }

    private static string[]? EffectiveCultures(CoreSearchDocument document)
        => document.Variations.Any(variation => variation.Culture == null) ? null
            : document.Variations.Select(variation => variation.Culture!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static Dictionary<Guid, IndexedEntity> ReadRecords(IKeyValueService store, string prefix)
    {
        var records = new Dictionary<Guid, IndexedEntity>();
        foreach (string? json in store.FindByKeyPrefix(prefix)?.Values ?? [])
        {
            if (json == null) continue;
            IndexedEntity record = JsonSerializer.Deserialize<IndexedEntity>(json) ?? throw new InvalidOperationException("Invalid persisted Relewise identity association.");
            records.Add(record.NodeKey, record);
        }
        return records;
    }
    private static bool Available(IContent content, IContentService service)
    {
        if (content.Trashed || !content.Published) return false;
        int parentId = content.ParentId;
        while (parentId > 0)
        {
            IContent? parent = service.GetById(parentId);
            if (parent == null || parent.Trashed || !parent.Published) return false;
            parentId = parent.ParentId;
        }
        return true;
    }
    private static string Claim(string entityType, string id, string? parentId = null)
        => entityType + ":" + (parentId?.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-") + ":" + parentId + ":" + (entityType == nameof(Brand) ? id.ToLowerInvariant() : id);
    private static void AddClaim(MappedEntity mapped, Dictionary<string, Guid> claims, string? parentId = null)
    {
        string claim = Claim(mapped.Entity.GetType().Name, mapped.Id, parentId);
        if (claims.TryGetValue(claim, out Guid other) && other != mapped.NodeKey)
            throw new InvalidOperationException($"Documents '{other}' and '{mapped.NodeKey}' claim the same {mapped.Entity.GetType().Name} ID '{mapped.Id}'.");
        claims[claim] = mapped.NodeKey;
    }
    private static void AddAdministrative(List<Trackable> batch, IndexedEntity entity, bool delete, bool enable)
    {
        if (!entity.Synchronize) return;
        switch (entity.EntityType)
        {
            case nameof(Content):
                batch.Add(new ContentAdministrativeAction(null, null, new FilterCollection(new ContentIdFilter(entity.Id)),
                    enable ? ContentAdministrativeAction.UpdateKind.Enable : delete ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable));
                break;
            case nameof(Product):
                batch.Add(new ProductAdministrativeAction(null, null, entity.Id,
                    enable ? ProductAdministrativeAction.UpdateKind.Enable : delete ? ProductAdministrativeAction.UpdateKind.Delete : ProductAdministrativeAction.UpdateKind.Disable));
                break;
            case nameof(Brand):
                batch.Add(new BrandAdministrativeAction(null, null, new FilterCollection(new BrandIdFilter(entity.Id)),
                    enable ? BrandAdministrativeAction.UpdateKind.Enable : delete ? BrandAdministrativeAction.UpdateKind.Delete : BrandAdministrativeAction.UpdateKind.Disable));
                break;
            case nameof(ProductCategory):
                batch.Add(new ProductCategoryAdministrativeAction(null, null, entity.Id,
                    enable ? CategoryAdministrativeAction.UpdateKind.Enable : delete ? CategoryAdministrativeAction.UpdateKind.Delete : CategoryAdministrativeAction.UpdateKind.Disable));
                break;
            case nameof(ContentCategory):
                batch.Add(new ContentCategoryAdministrativeAction(null, null, entity.Id,
                    enable ? CategoryAdministrativeAction.UpdateKind.Enable : delete ? CategoryAdministrativeAction.UpdateKind.Delete : CategoryAdministrativeAction.UpdateKind.Disable));
                break;
        }
    }
}

internal sealed class IndexedEntity
{
    public Guid NodeKey { get; set; }
    public string Id { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string DocumentTypeAlias { get; set; } = string.Empty;
    public bool Synchronize { get; set; }
    public bool Active { get; set; } = true;
    public List<Guid> Dependencies { get; set; } = new();
    public string[] SearchDataKeys { get; set; } = [];
    public string[] UnsupportedCoreFields { get; set; } = [];
    internal static IndexedEntity From(MappedEntity entity) => new()
    {
        NodeKey = entity.NodeKey, Id = entity.Id, EntityType = entity.Entity.GetType().Name,
        DocumentTypeAlias = entity.DocumentTypeAlias, Synchronize = entity.Synchronize, Dependencies = entity.Dependencies,
        UnsupportedCoreFields = entity.UnsupportedCoreFields,
        SearchDataKeys = EntityMapper.Data(entity.Entity).Keys.Where(key => key.StartsWith("rwUmbracoField_", StringComparison.Ordinal)).ToArray()
    };
}
