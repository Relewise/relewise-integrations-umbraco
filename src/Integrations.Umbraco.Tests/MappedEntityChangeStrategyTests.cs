using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.HostedServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests;

public class MappedEntityChangeStrategyTests
{
    [Fact]
    public async Task QueuesDraftRemovalsButIgnoresDraftSavesAndUnregisteredObjectTypes()
    {
        Guid removed = Guid.NewGuid(), draftSave = Guid.NewGuid(), published = Guid.NewGuid(), media = Guid.NewGuid();
        var operations = new Mock<IEntityIndexingOperations>();
        ContentChange[]? received = null;
        operations.Setup(value => value.ApplyChangesAsync(It.IsAny<ContentChange[]>(), It.IsAny<CancellationToken>()))
            .Callback<ContentChange[], CancellationToken>((changes, _) => received = changes).Returns(Task.CompletedTask);
        var queue = new Mock<IBackgroundTaskQueue>();
        Func<CancellationToken, Task>? work = null;
        queue.Setup(value => value.QueueBackgroundWorkItem(It.IsAny<Func<CancellationToken, Task>>()))
            .Callback<Func<CancellationToken, Task>>(callback => work = callback);
        var strategy = new MappedEntityChangeStrategy(new EntityIndexingQueue(operations.Object, queue.Object,
            Mock.Of<IHostApplicationLifetime>(), NullLogger<EntityIndexingQueue>.Instance));
        await strategy.HandleAsync([new ContentIndexInfo("mapped", [UmbracoObjectTypes.Document], null!)],
            [ContentChange.Document(removed, ChangeImpact.Remove, ContentState.Draft),
             ContentChange.Document(draftSave, ChangeImpact.Refresh, ContentState.Draft),
             ContentChange.Document(published, ChangeImpact.Remove, ContentState.Published),
             ContentChange.Media(media, ChangeImpact.Remove, ContentState.Draft)], default);
        Assert.Null(received);
        await work!(CancellationToken.None);
        Assert.Equal(new[] { removed, published }.Order(), received!.Select(change => change.Id).Order());
    }
}
