using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests;

public class MappedEntityChangeStrategyTests
{
    [Fact]
    public async Task DraftRemovalStillReconcilesWhenPublishedStrategyIgnoresIt()
    {
        Guid removed = Guid.NewGuid(), draftSave = Guid.NewGuid(), published = Guid.NewGuid(), media = Guid.NewGuid();
        var upstream = new Mock<IPublishedContentChangeStrategy>();
        upstream.Setup(strategy => strategy.HandleAsync(It.IsAny<IEnumerable<ContentIndexInfo>>(), It.IsAny<IEnumerable<ContentChange>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IEnumerable<ContentIndexInfo> indexes, IEnumerable<ContentChange> changes, CancellationToken _) =>
            {
                // The native published strategy intentionally ignores every draft change.
                foreach (var index in indexes)
                    foreach (var change in changes.Where(change => change.ContentState == ContentState.Published))
                        await index.Indexer.DeleteAsync(index.IndexAlias, [change.Id]);
            });
        var strategy = new MappedEntityChangeStrategy(null!, upstream.Object);
        var collected = await strategy.CollectChangesAsync([new ContentIndexInfo("mapped", [UmbracoObjectTypes.Document], null!)],
            [ContentChange.Document(removed, ChangeImpact.Remove, ContentState.Draft),
             ContentChange.Document(draftSave, ChangeImpact.Refresh, ContentState.Draft),
             ContentChange.Document(published, ChangeImpact.Remove, ContentState.Published),
             ContentChange.Media(media, ChangeImpact.Remove, ContentState.Draft)], default);
        Assert.Equal(new[] { removed, published }.Order(), collected.Keys.Order());
    }
}
