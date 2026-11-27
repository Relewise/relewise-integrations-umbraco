using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

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

        indexing.EnqueueChanges(
            changes.Where(change => change.ObjectType == UmbracoObjectTypes.Document &&
                                    objectTypes.Contains(change.ObjectType) &&
                                    (change.ContentState == ContentState.Published ||
                                     change.ChangeImpact == ChangeImpact.Remove))
                .Select(UmbracoSearchExportSource.FromChange));

        return Task.CompletedTask;
    }

    public Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken token)
    {
        return indexing.RebuildAsync(permanentlyDelete: false, token);
    }
}
