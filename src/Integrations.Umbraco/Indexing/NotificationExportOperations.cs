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
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using static Relewise.Integrations.Umbraco.Indexing.EntityExportDelivery;
using Content = Relewise.Client.DataTypes.Content;
using PublicAccess = Umbraco.Cms.Core.Constants.Conventions.PublicAccess;

namespace Relewise.Integrations.Umbraco.Indexing;

// Each export owns its service scope; this system controls discovery and reconciliation.
// Rebuilds reconcile source metadata in Relewise without integration-owned database records.
internal sealed class NotificationExportOperations(
    IServiceScopeFactory scopeFactory,
    RelewiseUmbracoConfiguration configuration,
    ILogger<NotificationExportOperations> logger,
    TimeProvider? clock = null) : IEntityIndexingOperations
{
    private long _lastAutomaticVersion;
    private long _lastExportVersion;

    public Task ExportAsync(
        IEnumerable<Guid> keys,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        long? feedVersion = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(keys, false, false, contentUpdateKind, feedVersion, token: cancellationToken);
    }

    public Task RebuildAsync(bool permanentlyDelete = false, CancellationToken cancellationToken = default)
    {
        return RunAsync([], true, permanentlyDelete, token: cancellationToken);
    }

    public Task ApplyChangesAsync(ExportChange[] changes, CancellationToken token)
    {
        return RunAsync([], false, false, changes: changes, token: token);
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
        if (!configuration.Mappings.Values.Any(mapping => mapping.Export))
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
            HashSet<Guid> requested = [.. keys];
            (ExportSourceBatch collected, HashSet<Guid> aggregateChildren) = await CollectAsync(
                services,
                context.UmbracoContext,
                requested,
                changes,
                rebuild,
                token);
            requested.UnionWith(collected.Keys);
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

    private async Task<(ExportSourceBatch Batch, HashSet<Guid> Variants)> CollectAsync(
        IServiceProvider services,
        IUmbracoContext context,
        HashSet<Guid> keys,
        ExportChange[]? changes,
        bool rebuild,
        CancellationToken token)
    {
        var batch = new ExportSourceBatch();
        var variants = new HashSet<Guid>();
        var contentService = services.GetRequiredService<IContentService>();
        if (rebuild)
        {
            foreach (IContent root in contentService.GetRootContent())
            {
                keys.Add(root.Key);
                IncludeDescendants(contentService, root.Id, keys, token);
            }
        }

        foreach (ExportChange change in changes ?? [])
        {
            keys.Add(change.Id);
            if (change.Impact != ExportChangeImpact.Refresh && contentService.GetById(change.Id) is { } editable)
            {
                IncludeDescendants(contentService, editable.Id, keys, token);
            }
        }

        foreach (Guid key in keys.ToArray())
        {
            IPublishedContent? node = context.Content?.GetById(key);
            if (node != null && configuration.Mappings.TryGetValue(
                                 node.ContentType.Alias,
                                 out MappingDefinition? mapping)
                             && mapping.EntityType == typeof(ProductVariant))
            {
                variants.Add(key);
                IPublishedContent? parent = node.Parent();
                if (parent != null)
                {
                    keys.Add(parent.Key);
                }
            }
            else if (node == null && services.GetRequiredService<IContentService>().GetById(key) is { } editable &&
                     configuration.Mappings.TryGetValue(editable.ContentType.Alias, out MappingDefinition? removed) &&
                     removed.EntityType == typeof(ProductVariant) && context.Content?.GetById(editable.ParentId) is
                     { } parent)
            {
                variants.Add(key);
                keys.Add(parent.Key);
            }
        }

        var identities = new Dictionary<(string Type, string Value), Guid?>();
        foreach (Guid key in keys)
        {
            token.ThrowIfCancellationRequested();
            batch.Keys.Add(key);
            IPublishedContent? node = context.Content?.GetById(key);
            if (node == null)
                continue;
            string?[] cultures = node.ContentType.VariesByCulture() ? [.. node.Cultures.Keys] : [null];
            PublicAccessEntry? protection =
                services.GetRequiredService<IPublicAccessService>().GetEntryForContent(node.Path);
            var access = new List<Guid>();
            foreach (PublicAccessRule rule in protection?.Rules ?? [])
            {
                if (rule.RuleValue == null || rule.RuleType == null)
                    continue;
                var identity = (rule.RuleType, rule.RuleValue);
                if (!identities.TryGetValue(identity, out Guid? id))
                {
                    id = rule.RuleType switch
                    {
                        PublicAccess.MemberUsernameRuleType =>
                            services.GetRequiredService<IMemberService>().GetByUsername(rule.RuleValue)?.Key,
                        PublicAccess.MemberRoleRuleType =>
                            (await services.GetRequiredService<IMemberGroupService>().GetByNameAsync(rule.RuleValue))
                            ?.Key,
                        _ => null
                    };
                    identities.Add(identity, id);
                }

                if (id.HasValue)
                    access.Add(id.Value);
            }

            batch.Documents[key] = new ExportSourceDocument(
                cultures,
                [.. cultures.Select(culture => culture?.ToLowerInvariant() ?? "")],
                protection == null ? null : [.. access]);
        }

        return (batch, variants);
    }

    private static void IncludeDescendants(
        IContentService content,
        int parentId,
        HashSet<Guid> keys,
        CancellationToken cancellationToken)
    {
        const int pageSize = 1000;
        int page = 0;
        long total;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            keys.UnionWith(content.GetPagedDescendants(parentId, page++, pageSize, out total).Select(node => node.Key));
        } while ((long)page * pageSize < total);
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
