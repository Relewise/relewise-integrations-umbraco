using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco;

// The writer keeps no integration-owned database records. Rebuilds reconcile source metadata in Relewise.
internal sealed class EntityIndexingService(IServiceScopeFactory scopeFactory, RelewiseUmbracoConfiguration configuration, ILogger<EntityIndexingService> logger) : IEntityIndexingOperations
{
    internal const string IndexAlias = "Relewise_MappedEntities";
    private readonly SemaphoreSlim _writer = new(1, 1);

    internal static string SiteId(IServiceProvider services)
    {
        string? id = services.GetRequiredService<IOptions<GlobalSettings>>().Value.Id;
        return !string.IsNullOrWhiteSpace(id) ? id
            : throw new InvalidOperationException("Umbraco's installation ID must be configured before indexing.");
    }

    public Task ExportAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default,
        ContentUpdate.UpdateKind? contentUpdateKind = null, long? feedVersion = null)
        => RunAsync(keys, false, false, cancellationToken, contentUpdateKind, feedVersion);

    public Task RebuildAsync(bool permanentlyDelete = false, CancellationToken cancellationToken = default)
        => RunAsync([], true, permanentlyDelete, cancellationToken);

    public Task ApplyChangesAsync(ContentChange[] changes, CancellationToken token)
        => RunAsync([], false, false, token, changes: changes);

    public async Task DeleteAsync(IEnumerable<DeletedEntity> deleted, CancellationToken token)
    {
        DeletedEntity[] snapshots = deleted.ToArray();
        if (snapshots.Length == 0)
        {
            return;
        }

        await _writer.WaitAsync(token);
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IRelewiseClientFactory clients = scope.ServiceProvider.GetRequiredService<IRelewiseClientFactory>();
            ITracker tracker = clients.GetClient<ITracker>(Constants.NamedClientName);
            var actions = new List<Trackable>();
            foreach (DeletedEntity entity in snapshots)
            {
                if (entity.Synchronize)
                {
                    actions.Add(DeleteIdentity(entity.EntityType, entity.Id));
                }
                else if (entity.EntityType == typeof(Content) || entity.EntityType == typeof(Product))
                {
                    var filters = new FilterCollection(DataFilter(entity.EntityType, "rwUmbracoNodeKey", new EqualsCondition(entity.NodeKey.ToString("D"))),
                        DataFilter(entity.EntityType, "rwUmbracoSite", new EqualsCondition(SiteId(scope.ServiceProvider))));
                    await MarkUnavailableAsync(clients.GetClient<IDataAccessor>(Constants.NamedClientName), tracker, entity.EntityType, filters, token);
                }
            }
            if (actions.Count > 0)
            {
                await tracker.TrackAsync(actions, token);
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    private async Task RunAsync(IEnumerable<Guid> keys, bool rebuild, bool permanentlyDelete, CancellationToken token,
        ContentUpdate.UpdateKind? contentUpdateKind = null, long? feedVersion = null, ContentChange[]? changes = null)
    {
        if (!configuration.Mappings.Values.Any(mapping => mapping.Export))
        {
            return;
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        int mappedCount = 0;
        await _writer.WaitAsync(token);
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;
            string site = SiteId(services);
            IRelewiseClientFactory clients = services.GetRequiredService<IRelewiseClientFactory>();
            ITracker tracker = clients.GetClient<ITracker>(Constants.NamedClientName);
            using global::Umbraco.Cms.Core.UmbracoContextReference context = services.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
            IPublishedContentChangeStrategy strategy = services.GetRequiredService<IPublishedContentChangeStrategy>();
            var collected = new IndexingKeyCollector();
            var requested = keys.ToHashSet();
            var aggregateChildren = new HashSet<Guid>();
            if (rebuild)
            {
                await strategy.RebuildAsync(new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], collected), token);
            }
            else
            {
                await strategy.HandleAsync([new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], collected)],
                    changes ?? requested.Select(key => ContentChange.Document(key, ChangeImpact.Refresh, ContentState.Published)).ToArray(), token);
            }
            // Already-unpublished permanent deletions can be reported through the draft flow.
            if (changes != null)
            {
                collected.Keys.UnionWith(changes.Where(change => change.ObjectType == UmbracoObjectTypes.Document && change.ChangeImpact == ChangeImpact.Remove).Select(change => change.Id));
            }

            requested.UnionWith(collected.Keys);
            // A child Variant contributes to its parent Product, rather than creating an independent entity.
            foreach (Guid key in requested.ToArray())
            {
                IPublishedContent? node = context.UmbracoContext.Content?.GetById(key);
                IPublishedContent? parent;
                if (node == null)
                {
                    IContent? editable = services.GetRequiredService<IContentService>().GetById(key);
                    if (editable == null || !configuration.Mappings.TryGetValue(editable.ContentType.Alias, out MappingDefinition? removedMapping) || removedMapping.EntityType != typeof(ProductVariant))
                    {
                        continue;
                    }

                    parent = context.UmbracoContext.Content?.GetById(editable.ParentId);
                }
                else
                {
                    if (!configuration.Mappings.TryGetValue(node.ContentType.Alias, out MappingDefinition? mapping) || mapping.EntityType != typeof(ProductVariant))
                    {
                        continue;
                    }

                    parent = node.Parent();
                }
                aggregateChildren.Add(key);
                if (parent != null && !collected.Documents.ContainsKey(parent.Key))
                {
                    await strategy.HandleAsync([new ContentIndexInfo(IndexAlias, [UmbracoObjectTypes.Document], collected)],
                        [ContentChange.Document(parent.Key, ChangeImpact.Refresh, ContentState.Published)], token);
                }
            }
            string generation = Guid.NewGuid().ToString("N");
            long version = feedVersion ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await using var operation = new MappingOperation(services, token);
            var mapper = new EntityMapper(configuration);
            string[] siteCultures = (await services.GetRequiredService<ILanguageService>().GetAllAsync()).Select(language => language.IsoCode).ToArray();
            var desired = new List<MappedEntity>();
            var claims = new HashSet<string>(StringComparer.Ordinal);
            var unmapped = new HashSet<Guid>();
            foreach (KeyValuePair<Guid, CoreSearchDocument> entry in collected.Documents)
            {
                token.ThrowIfCancellationRequested();
                IPublishedContent? source = context.UmbracoContext.Content?.GetById(entry.Key);
                if (source == null || entry.Value.Variations.Length == 0)
                {
                    continue;
                }

                if (!configuration.Mappings.TryGetValue(source.ContentType.Alias, out MappingDefinition? mapping) || !mapping.Export)
                {
                    unmapped.Add(entry.Key);
                    continue;
                }
                if (mapping.EntityType == typeof(ProductVariant))
                {
                    continue;
                }

                string[]? cultures = entry.Value.Variations.Any(value => value.Culture == null) ? null
                    : entry.Value.Variations.Select(value => value.Culture!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                // Invariant values are shared across the site's languages, rather than belonging only to its default language.
                if (!source.ContentType.VariesByCulture())
                {
                    cultures = siteCultures;
                }

                MappedEntity mapped = await mapper.MapAsync(source, mapping, operation, site, version, contentUpdateKind: contentUpdateKind, publishedCultures: cultures);
                string claim = mapping.EntityType.Name + ":" + (mapping.EntityType == typeof(Brand) ? mapped.Id.ToLowerInvariant() : mapped.Id);
                if (!claims.Add(claim))
                {
                    throw new InvalidOperationException($"Multiple documents export the same {mapping.EntityType.Name} ID '{mapped.Id}'.");
                }

                if (mapped.Entity is Content or Product)
                {
                    CoreSearchMetadata.Apply(mapped, entry.Value, siteCultures);
                }

                EntityMapper.Data(mapped.Entity)["rwUmbracoExportGeneration"] = generation;
                desired.Add(mapped);
            }
            mappedCount = desired.Count;
            var updates = new List<Trackable>();
            foreach (MappedEntity entity in desired)
            {
                updates.Add(entity.Update());
                if (entity.Synchronize)
                {
                    updates.Add(Administrative(entity.Entity.GetType(), new FilterCollection(DataFilter(entity.Entity.GetType(), "rwUmbracoNodeKey", new EqualsCondition(entity.NodeKey.ToString("D"))), DataFilter(entity.Entity.GetType(), "rwUmbracoSite", new EqualsCondition(site))), false, true));
                }
            }
            if (updates.Count > 0)
            {
                await tracker.TrackAsync(updates, token);
            }

            token.ThrowIfCancellationRequested();

            // Only a completed full export may reconcile entities missing from its generation.
            Guid[] unavailable = requested.Except(desired.Select(entity => entity.NodeKey)).Except(unmapped).Except(aggregateChildren).ToArray();
            if (rebuild || unavailable.Length > 0)
            {
                var cleanup = new List<Trackable>();
                (Guid[] Keys, bool Delete)[] removals = rebuild ? new[] { (Keys: unavailable, Delete: permanentlyDelete) }
                    : unavailable.GroupBy(key => services.GetRequiredService<IContentService>().GetById(key) == null)
                        .Select(group => (Keys: group.ToArray(), Delete: group.Key)).ToArray();
                foreach (IGrouping<(Type EntityType, bool Enabled), MappingDefinition> group in configuration.Mappings.Values.Where(mapping => mapping.Export && mapping.EntityType != typeof(ProductVariant)).GroupBy(mapping => (mapping.EntityType, mapping.AdministrativeActions.Enabled)))
                {
                    Type type = group.Key.EntityType;
                    foreach ((Guid[] Keys, bool Delete) removal in removals)
                    {
                        var filters = new FilterCollection(DataFilter(type, "rwUmbracoSite", new EqualsCondition(site)),
                            new OrFilter(group.Select(mapping => DataFilter(type, "contentTypeAlias", new EqualsCondition(mapping.DocumentTypeAlias))).ToArray()));
                        filters.Add(rebuild ? DataFilter(type, "rwUmbracoExportGeneration", new EqualsCondition(generation, negated: true), filterOutIfKeyIsNotFound: false)
                            : new OrFilter(removal.Keys.Select(key => DataFilter(type, "rwUmbracoNodeKey", new EqualsCondition(key.ToString("D")))).ToArray()));
                        if (type == typeof(Content) || type == typeof(Product))
                        {
                            await MarkUnavailableAsync(clients.GetClient<IDataAccessor>(Constants.NamedClientName), tracker, type, filters, token);
                        }

                        if (group.Key.Enabled)
                        {
                            cleanup.Add(Administrative(type, filters, removal.Delete, false));
                        }
                    }
                }
                if (cleanup.Count > 0)
                {
                    await tracker.TrackAsync(cleanup, token);
                }
            }
        }
        finally
        {
            _writer.Release();
            logger.LogDebug("Relewise export finished: rebuild={Rebuild}, mapped={Mapped}, elapsedMilliseconds={ElapsedMilliseconds}",
                rebuild, mappedCount, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static async Task MarkUnavailableAsync(IDataAccessor reader, ITracker tracker, Type type, FilterCollection filters, CancellationToken token)
    {
        // Only lifecycle/rebuild reconciliation reads remote entities; ordinary publication sends no lookup requests.
        var updates = new List<Trackable>();
        if (type == typeof(Product))
        {
            var query = new ProductQuery(filters, excludeProductsWithNoVariants: false, pageSize: 500)
            {
                IncludeDisabledProducts = true,
                ResultSettings = new ProductQuery.SelectedPropertiesSettings { SelectedProductDetailsProperties = new() }
            };
            do
            {
                ProductDetailsCollectionResponse response = await reader.QueryAsync(query, token);
                foreach (ProductResultDetails? product in response.Products ?? [])
                {
                    updates.Add(new ProductUpdate(new Product(product.ProductId) { Assortments = null, CategoryPaths = null, Data = new() { ["rwUmbracoPublished"] = false } }, ProductUpdate.UpdateKind.ReplaceProvidedProperties));
                }

                query.NextPageToken = response.NextPageToken;
            } while (query.NextPageToken != null);
        }
        else
        {
            var query = new ContentQuery(filters, 500) { IncludeDisabledContents = true };
            while (true)
            {
                ContentDetailsCollectionResponse response = await reader.QueryAsync(query, token);
                foreach (ContentResultDetails? content in response.Contents ?? [])
                {
                    updates.Add(new ContentUpdate(new Content(content.ContentId) { Assortments = null, CategoryPaths = null, Data = new() { ["rwUmbracoPublished"] = false } }, ContentUpdate.UpdateKind.ReplaceProvidedProperties));
                }

                if ((response.Contents?.Length ?? 0) < query.NumberOfResults)
                {
                    break;
                }

                query.SkipNumberOfResults += query.NumberOfResults;
            }
        }
        if (updates.Count > 0)
        {
            await tracker.TrackAsync(updates, token);
        }
    }

    internal static Filter DataFilter(Type type, string key, ValueCondition condition, bool filterOutIfKeyIsNotFound = true)
        => type == typeof(Product) ? new ProductDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(Content) ? new ContentDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(Brand) ? new BrandDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : type == typeof(ProductCategory) ? new ProductCategoryDataFilter(key, condition, filterOutIfKeyIsNotFound)
            : new ContentCategoryDataFilter(key, condition, filterOutIfKeyIsNotFound);

    private static Trackable DeleteIdentity(Type type, string id)
        => type == typeof(Product) ? new ProductAdministrativeAction(null, null, id, ProductAdministrativeAction.UpdateKind.Delete)
            : type == typeof(Content) ? new ContentAdministrativeAction(null, null, new FilterCollection(new ContentIdFilter(id)), ContentAdministrativeAction.UpdateKind.Delete)
            : type == typeof(Brand) ? new BrandAdministrativeAction(null, null, new FilterCollection(new BrandIdFilter(id)), BrandAdministrativeAction.UpdateKind.Delete)
            : type == typeof(ProductCategory) ? new ProductCategoryAdministrativeAction(null, null, id, CategoryAdministrativeAction.UpdateKind.Delete)
            : new ContentCategoryAdministrativeAction(null, null, id, CategoryAdministrativeAction.UpdateKind.Delete);

    private static Trackable Administrative(Type type, FilterCollection filters, bool delete, bool enable)
        => type == typeof(Product) ? new ProductAdministrativeAction(null, null, filters, enable ? ProductAdministrativeAction.UpdateKind.Enable : delete ? ProductAdministrativeAction.UpdateKind.Delete : ProductAdministrativeAction.UpdateKind.Disable)
            : type == typeof(Content) ? new ContentAdministrativeAction(null, null, filters, enable ? ContentAdministrativeAction.UpdateKind.Enable : delete ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable)
            : type == typeof(Brand) ? new BrandAdministrativeAction(null, null, filters, enable ? BrandAdministrativeAction.UpdateKind.Enable : delete ? BrandAdministrativeAction.UpdateKind.Delete : BrandAdministrativeAction.UpdateKind.Disable)
            : type == typeof(ProductCategory) ? new ProductCategoryAdministrativeAction(null, null, filters, enable ? CategoryAdministrativeAction.UpdateKind.Enable : delete ? CategoryAdministrativeAction.UpdateKind.Delete : CategoryAdministrativeAction.UpdateKind.Disable)
            : new ContentCategoryAdministrativeAction(null, null, filters, enable ? CategoryAdministrativeAction.UpdateKind.Enable : delete ? CategoryAdministrativeAction.UpdateKind.Delete : CategoryAdministrativeAction.UpdateKind.Disable);
}
