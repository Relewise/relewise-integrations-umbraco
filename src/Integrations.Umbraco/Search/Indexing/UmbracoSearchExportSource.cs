using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Search.Indexing;

internal sealed class UmbracoSearchExportSource(IPublishedContentChangeStrategy strategy)
{
    public async Task<ExportSourceBatch> CollectAsync(ExportChange[] changes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var batch = new ExportSourceBatch();
        var collector = new IndexingKeyCollector(batch);

        await strategy.HandleAsync(
            [new ContentIndexInfo(SearchExportOperations.IndexAlias, [UmbracoObjectTypes.Document], collector)],
            changes.Select(ToChange),
            token);

        // Permanent deletions can arrive through the draft flow and be ignored by the published strategy.
        batch.Keys.UnionWith(
            changes.Where(change => change.Impact == ExportChangeImpact.Remove)
                .Select(change => change.Id));

        return batch;
    }

    public async Task<ExportSourceBatch> CollectAllAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var batch = new ExportSourceBatch();
        var collector = new IndexingKeyCollector(batch);

        await strategy.RebuildAsync(
            new ContentIndexInfo(SearchExportOperations.IndexAlias, [UmbracoObjectTypes.Document], collector),
            token);

        return batch;
    }

    internal static ExportChange FromChange(ContentChange change)
    {
        ExportChangeImpact impact = change.ChangeImpact switch
        {
            ChangeImpact.Refresh => ExportChangeImpact.Refresh,
            ChangeImpact.RefreshWithDescendants => ExportChangeImpact.RefreshWithDescendants,
            ChangeImpact.Remove => ExportChangeImpact.Remove,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        return new ExportChange(change.Id, impact, change.ContentState == ContentState.Published);
    }

    private static ContentChange ToChange(ExportChange change)
    {
        ChangeImpact impact = change.Impact switch
        {
            ExportChangeImpact.Refresh => ChangeImpact.Refresh,
            ExportChangeImpact.RefreshWithDescendants => ChangeImpact.RefreshWithDescendants,
            ExportChangeImpact.Remove => ChangeImpact.Remove,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };

        return ContentChange.Document(
            change.Id,
            impact,
            change.Published ? ContentState.Published : ContentState.Draft);
    }
}
