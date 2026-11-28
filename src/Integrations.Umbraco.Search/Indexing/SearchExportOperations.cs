using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using static Relewise.Integrations.Umbraco.Indexing.EntityExportDelivery;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Search.Indexing;

// Each export owns its service scope; this system controls discovery and reconciliation.
// Rebuilds reconcile source metadata in Relewise without integration-owned database records.
internal sealed class SearchExportOperations(
    IServiceScopeFactory scopeFactory,
    RelewiseUmbracoConfiguration configuration,
    ILogger<SearchExportOperations> logger,
    TimeProvider? clock = null) : IEntityIndexingOperations
{
    public const string IndexAlias = "Relewise_MappedEntities";
    private long _lastAutomaticVersion;
    private long _lastExportVersion;

    public Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        long? feedVersion = null,
        CancellationToken token = default)
    {
        return RunAsync(keys, rebuild: false, permanentlyDelete: false, contentUpdateKind, feedVersion, token: token);
    }

    public Task RebuildAsync(bool permanentlyDelete = false, CancellationToken token = default)
    {
        return RunAsync([], rebuild: true, permanentlyDelete: permanentlyDelete, token: token);
    }

    public Task ApplyChangesAsync(ExportChange[] changes, CancellationToken token)
    {
        return RunAsync([], rebuild: false, permanentlyDelete: false, changes: changes, token: token);
    }

    public async Task DeleteAsync(
        IEnumerable<DeletedEntity> deleted,
        CancellationToken token)
    {
        DeletedEntity[] snapshots = [.. deleted];

        if (snapshots.Length == 0)
        {
            return;
        }

        token.ThrowIfCancellationRequested();

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        var clients = scope.ServiceProvider.GetRequiredService<IRelewiseClientFactory>();
        var tracker = clients.GetClient<ITracker>(Constants.ClientNames.Integration);

        var actions = new List<Trackable>();

        foreach (DeletedEntity entity in snapshots)
        {
            if (entity.Synchronize)
            {
                actions.Add(DeleteIdentity(entity.EntityType, entity.Id));
            }
            else if (entity.EntityType == typeof(Content) || entity.EntityType == typeof(Product))
            {
                var filters = new FilterCollection(
                    DataFilter(
                        entity.EntityType,
                        RelewiseDataKeys.NodeKey,
                        new EqualsCondition(entity.NodeKey.ToString("D"))));

                await MarkUnavailableAsync(
                    clients.GetClient<IDataAccessor>(Constants.ClientNames.Integration),
                    tracker,
                    entity.EntityType,
                    filters,
                    token);
            }
        }

        if (actions.Count > 0)
        {
            await tracker.TrackAsync(actions, token);
        }
    }

    private async Task RunAsync(
        IEnumerable<Guid> keys,
        bool rebuild,
        bool permanentlyDelete,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        long? feedVersion = null,
        ExportChange[]? changes = null,
        CancellationToken token = default)
    {
        if (!configuration.Mappings.HasExports)
        {
            return;
        }

        token.ThrowIfCancellationRequested();

        long started = Stopwatch.GetTimestamp();
        var mappedCount = 0;

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;

            using UmbracoContextReference context = services
                .GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();

            var clients = services.GetRequiredService<IRelewiseClientFactory>();
            var tracker = clients.GetClient<ITracker>(Constants.ClientNames.Integration);
            var exportSource = services.GetRequiredService<UmbracoSearchExportSource>();

            HashSet<Guid> requested = [.. keys];
            ExportSourceBatch collected = rebuild
                ? await exportSource.CollectAllAsync(token)
                : await exportSource.CollectAsync(
                    changes ??
                    [
                        .. requested.Select(key => new ExportChange(key, ExportChangeImpact.Refresh, Published: true))
                    ],
                    token);

            requested.UnionWith(collected.Keys);

            var aggregateChildren = new HashSet<Guid>();
            var navigation = services.GetRequiredService<IDocumentNavigationQueryService>();

            // A child Variant contributes to its parent Product, rather than creating an independent entity.
            foreach (Guid key in requested.ToArray())
            {
                IPublishedContent? node = context.UmbracoContext.Content?.GetById(key);
                IPublishedContent? parent;

                if (node == null)
                {
                    IContent? editable = services.GetRequiredService<IContentService>().GetById(key);

                    if (editable == null ||
                        !configuration.Mappings.TryGetMapping(
                            editable.ContentType.Alias,
                            out MappingDefinition? removedMapping) ||
                        removedMapping.EntityType != typeof(ProductVariant))
                    {
                        continue;
                    }

                    parent = context.UmbracoContext.Content?.GetById(editable.ParentId);
                }
                else
                {
                    if (!configuration.Mappings.TryGetMapping(node.ContentType.Alias, out MappingDefinition? mapping) ||
                        mapping.EntityType != typeof(ProductVariant))
                    {
                        continue;
                    }

                    // Resolve by key: background exports have no active frontend culture.
                    parent = navigation.TryGetParentKey(node.Key, out Guid? parentKey) && parentKey.HasValue
                        ? context.UmbracoContext.Content?.GetById(preview: false, parentKey.Value)
                        : null;
                }

                aggregateChildren.Add(key);

                if (parent != null && !collected.Documents.ContainsKey(parent.Key))
                {
                    collected.Include(
                        await exportSource.CollectAsync(
                            [new ExportChange(parent.Key, ExportChangeImpact.Refresh, Published: true)],
                            token));
                }
            }

            await using var operation = new MappingOperation(services, token) { FullRebuild = rebuild };

            long version = feedVersion ?? NextVersion();
            _lastExportVersion = version;

            string[] siteCultures = (await services.GetRequiredService<ILanguageService>().GetAllAsync())
                .Select(language => language.IsoCode)
                .ToArray();

            MappedExportBatch mappedBatch = await EntityExportMapping.MapAsync(
                configuration,
                context.UmbracoContext,
                operation,
                collected.Documents,
                siteCultures,
                version,
                contentUpdateKind,
                token);
            MappedEntity[] desired = mappedBatch.Entities;
            HashSet<Guid> unmapped = mappedBatch.Unmapped;
            mappedCount = desired.Length;

            List<Trackable> updates = EntityExportRequests.Updates(desired);

            if (updates.Count > 0)
            {
                await tracker.TrackAsync(updates, token);
            }

            token.ThrowIfCancellationRequested();

            // Only a completed full export may reconcile entities missing from its version.
            Guid[] unavailable =
            [
                .. requested.Except(desired.Select(entity => entity.NodeKey))
                    .Except(unmapped)
                    .Except(aggregateChildren)
            ];

            if (rebuild || unavailable.Length > 0)
            {
                (Guid[] Keys, bool Delete)[] removals = rebuild
                    ? [(Keys: unavailable, Delete: permanentlyDelete)]
                    :
                    [
                        .. unavailable.GroupBy(key => services.GetRequiredService<IContentService>()
                                .GetById(key) == null)
                            .Select(group => (Keys: group.ToArray(), Delete: group.Key))
                    ];

                await EntityExportDelivery.ReconcileAsync(
                    clients,
                    EntityExportRequests.Reconcile(configuration, removals, rebuild ? version : null),
                    token);
            }
        }
        finally
        {
            logger.LogDebug(
                "Relewise export finished: rebuild={Rebuild}, mapped={Mapped}, " +
                "elapsedMilliseconds={ElapsedMilliseconds}",
                rebuild,
                mappedCount,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private long NextVersion()
    {
        long now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();

        // This system serializes operations; clock resolution must not reuse an export marker.
        _lastAutomaticVersion = Math.Max(now, _lastAutomaticVersion + 1);

        if (_lastAutomaticVersion == _lastExportVersion)
        {
            _lastAutomaticVersion++;
        }

        return _lastAutomaticVersion;
    }
}
