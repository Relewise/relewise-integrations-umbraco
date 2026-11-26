using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Indexing;

// A distinct strategy type isolates the background work from Examine's strategy group.
internal sealed class MappedEntityChangeStrategy(EntityIndexingQueue indexing) : IContentChangeStrategy
{
    public Task HandleAsync(
        IEnumerable<ContentIndexInfo> indexInfos,
        IEnumerable<ContentChange> changes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<UmbracoObjectTypes>
            objectTypes = [.. indexInfos.SelectMany(index => index.ContainedObjectTypes)];

        indexing.EnqueueChanges(
            changes.Where(change => objectTypes.Contains(change.ObjectType)
                                    && (change.ContentState == ContentState.Published ||
                                        change.ChangeImpact == ChangeImpact.Remove)));

        return Task.CompletedTask;
    }

    public Task RebuildAsync(ContentIndexInfo indexInfo, CancellationToken cancellationToken)
    {
        return indexing.RebuildAsync(token: cancellationToken);
    }
}
