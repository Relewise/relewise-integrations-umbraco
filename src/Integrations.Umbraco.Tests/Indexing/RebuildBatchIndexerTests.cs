using Moq;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Models;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Indexing;
#endif
using Umbraco.Cms.Core.Services.Navigation;
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Indexing;
#endif

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class RebuildBatchIndexerTests
{
    [Fact]
    public async Task WritesBoundedBatchesAndAwaitsEachDelivery()
    {
        var batches = new List<ExportSourceBatch>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var indexer = new RebuildBatchIndexer(
            Mock.Of<IDocumentNavigationQueryService>(),
            async (batch, token) =>
            {
                batches.Add(batch);
                await release.Task.WaitAsync(token);
            },
            CancellationToken.None);
        for (var i = 0; i < 99; i++)
        {
            await AddAsync(indexer, Guid.NewGuid());
        }

        Assert.Empty(batches);
        Task delivery = AddAsync(indexer, Guid.NewGuid());
        Assert.False(delivery.IsCompleted);
        Assert.Equal(100, Assert.Single(batches).Documents.Count);
        release.SetResult();
        await delivery;
        await AddAsync(indexer, Guid.NewGuid());
        await indexer.FlushAsync();
        Assert.Equal(new[] { 100, 1 }, batches.Select(batch => batch.Documents.Count));
    }

    [Fact]
    public async Task RemovalAfterDeliveryIncludesDescendantsButAllowsSubsequentReindexing()
    {
        Guid parent = Guid.NewGuid(), child = Guid.NewGuid(), grandchild = Guid.NewGuid();
        IEnumerable<Guid> descendants = [child, grandchild];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetDescendantsKeys(parent, out descendants)).Returns(true);
        var batches = new List<ExportSourceBatch>();
        var indexer = new RebuildBatchIndexer(navigation.Object, (batch, _) =>
        {
            batches.Add(batch);
            return Task.CompletedTask;
        }, CancellationToken.None);
        await AddAsync(indexer, child);
        await AddAsync(indexer, grandchild);
        await indexer.FlushAsync();
        await indexer.DeleteAsync("mapped", [parent]);
        await AddAsync(indexer, child);
        await indexer.FlushAsync();
        Assert.Equal(new[] { parent, grandchild }.Order(), indexer.Removed.Order());
        Assert.Equal(child, Assert.Single(batches[1].Documents).Key);
        Assert.Equal(2, batches[0].Documents.Count);
    }

    [Fact]
    public async Task CancelledFinalBufferIsNeverDelivered()
    {
        using var cancellation = new CancellationTokenSource();
        var writes = 0;
        var indexer = new RebuildBatchIndexer(Mock.Of<IDocumentNavigationQueryService>(), (_, _) =>
        {
            writes++;
            return Task.CompletedTask;
        }, cancellation.Token);
        await AddAsync(indexer, Guid.NewGuid());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => indexer.FlushAsync());
        Assert.Equal(0, writes);
    }

    private static Task AddAsync(RebuildBatchIndexer indexer, Guid key)
    {
        return indexer.AddOrUpdateAsync(
            "mapped",
            key,
            UmbracoObjectTypes.Document,
            [new Variation("en-US", null)],
            [],
            protection: null);
    }
}
