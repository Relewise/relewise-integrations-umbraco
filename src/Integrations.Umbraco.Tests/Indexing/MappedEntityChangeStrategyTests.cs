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
    public async Task ProcessesDraftRemovalsButIgnoresDraftSavesAndUnregisteredObjectTypes()
    {
        Guid removed = Guid.NewGuid(), draftSave = Guid.NewGuid(), published = Guid.NewGuid(), media = Guid.NewGuid();
        ExportChange[]? received = null;
        var exports = new Mock<IExportSystem>();
        exports.Setup(value => value.ApplyChangesAsync(
                It.IsAny<IEnumerable<ExportChange>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ExportChange>, CancellationToken>((changes, _) => received = [.. changes])
            .Returns(Task.CompletedTask);
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
    [Fact]
    public async Task FrameworkCallbackAwaitsDeliveryWithoutEnqueuingAnotherWorker()
    {
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exports = new Mock<IExportSystem>();
        exports.Setup(value => value.ApplyChangesAsync(
                It.IsAny<IEnumerable<ExportChange>>(), It.IsAny<CancellationToken>()))
            .Returns(delivered.Task);
        var strategy = new MappedEntityChangeStrategy(exports.Object);
        Task work = strategy.HandleAsync(
            [new ContentIndexInfo("mapped", [UmbracoObjectTypes.Document], null!)],
            [ContentChange.Document(Guid.NewGuid(), ChangeImpact.Refresh, ContentState.Published)],
            CancellationToken.None);
        Assert.False(work.IsCompleted);
        exports.Verify(value => value.EnqueueChanges(It.IsAny<IEnumerable<ExportChange>>()), Times.Never);
        delivered.SetException(new InvalidOperationException("Delivery failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => work);
    }

    [Fact]
    public async Task ChangesOutsideThePublishedDocumentScopeDoNotEnterTheWriter()
    {
        var exports = new Mock<IExportSystem>(MockBehavior.Strict);
        var strategy = new MappedEntityChangeStrategy(exports.Object);
        await strategy.HandleAsync(
            [new ContentIndexInfo("mapped", [UmbracoObjectTypes.Document], null!)],
            [ContentChange.Document(Guid.NewGuid(), ChangeImpact.Refresh, ContentState.Draft)],
            CancellationToken.None);
        exports.VerifyNoOtherCalls();
    }

}
