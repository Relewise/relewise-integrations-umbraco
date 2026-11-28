using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Indexing;
#endif
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Indexing;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
#endif

namespace Relewise.Integrations.Umbraco.Search.Indexing;

// A distinct strategy type isolates the background work from Examine's strategy group.
internal sealed class MappedEntityChangeStrategy(IExportSystem indexing) : IContentChangeStrategy
{
    public Task HandleAsync(
        IEnumerable<ContentIndexInfo> indexInfos,
        IEnumerable<ContentChange> changes,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        HashSet<UmbracoObjectTypes>
            objectTypes = [.. indexInfos.SelectMany(index => index.ContainedObjectTypes)];

        ExportChange[] pending =
        [
            .. changes.Where(change => change.ObjectType == UmbracoObjectTypes.Document &&
                                    objectTypes.Contains(change.ObjectType) &&
                                    (change.ContentState == ContentState.Published ||
                                     change.ChangeImpact == ChangeImpact.Remove))
                .Select(UmbracoSearchExportSource.FromChange)
        ];
        return pending.Length == 0 ? Task.CompletedTask : indexing.ApplyChangesAsync(pending, token);
    }

    public Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken token)
    {
        return indexing.RebuildAsync(permanentlyDelete: false, token);
    }
}
