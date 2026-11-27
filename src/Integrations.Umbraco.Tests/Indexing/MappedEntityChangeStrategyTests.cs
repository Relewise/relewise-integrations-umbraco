using Moq;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Models;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Indexing;
#endif
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Indexing;
#endif

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class MappedEntityChangeStrategyTests
{
    [Fact]
    public async Task QueuesDraftRemovalsButIgnoresDraftSavesAndUnregisteredObjectTypes()
    {
        Guid removed = Guid.NewGuid(), draftSave = Guid.NewGuid(), published = Guid.NewGuid(), media = Guid.NewGuid();
        ExportChange[]? received = null;
        var exports = new Mock<IExportSystem>();
        exports.Setup(value => value.EnqueueChanges(It.IsAny<IEnumerable<ExportChange>>()))
            .Callback<IEnumerable<ExportChange>>(changes => received = [.. changes]);
        var strategy = new MappedEntityChangeStrategy(exports.Object);

        await strategy.HandleAsync(
            [new ContentIndexInfo("mapped", [UmbracoObjectTypes.Document], null!)],
            [
                ContentChange.Document(removed, ChangeImpact.Remove, ContentState.Draft),
                ContentChange.Document(draftSave, ChangeImpact.Refresh, ContentState.Draft),
                ContentChange.Document(published, ChangeImpact.Remove, ContentState.Published),
                ContentChange.Media(media, ChangeImpact.Remove, ContentState.Draft)
            ],
            default);
        Assert.Equal(new[] { removed, published }.Order(), received!.Select(change => change.Id).Order());
    }
}
