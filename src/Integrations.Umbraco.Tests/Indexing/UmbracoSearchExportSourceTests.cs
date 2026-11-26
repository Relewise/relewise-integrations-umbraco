using Moq;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class UmbracoSearchExportSourceTests
{
    [Theory]
    [InlineData(ChangeImpact.Refresh, ContentState.Draft)]
    [InlineData(ChangeImpact.Refresh, ContentState.Published)]
    [InlineData(ChangeImpact.RefreshWithDescendants, ContentState.Draft)]
    [InlineData(ChangeImpact.RefreshWithDescendants, ContentState.Published)]
    [InlineData(ChangeImpact.Remove, ContentState.Draft)]
    [InlineData(ChangeImpact.Remove, ContentState.Published)]
    public async Task DiscoveryPreservesImpactStateAndCancellation(ChangeImpact impact, ContentState state)
    {
        var key = Guid.NewGuid();
        var native = ContentChange.Document(key, impact, state);
        using var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;
        var strategy = new Mock<IPublishedContentChangeStrategy>(MockBehavior.Strict);
        strategy.Setup(value => value.HandleAsync(
                It.IsAny<IEnumerable<ContentIndexInfo>>(),
                It.IsAny<IEnumerable<ContentChange>>(),
                cancellationToken))
            .Returns((IEnumerable<ContentIndexInfo> indexes, IEnumerable<ContentChange> changes, CancellationToken _) =>
            {
                ContentIndexInfo index = Assert.Single(indexes);
                Assert.Equal(SearchExportOperations.IndexAlias, index.IndexAlias);
                Assert.Equal(new[] { UmbracoObjectTypes.Document }, index.ContainedObjectTypes);
                ContentChange actual = Assert.Single(changes);
                Assert.Equal(native.Id, actual.Id);
                Assert.Equal(native.ObjectType, actual.ObjectType);
                Assert.Equal(native.ChangeImpact, actual.ChangeImpact);
                Assert.Equal(native.ContentState, actual.ContentState);
                return Task.CompletedTask;
            });

        var source = new UmbracoSearchExportSource(strategy.Object);
        ExportSourceBatch batch = await source.CollectAsync(
            [UmbracoSearchExportSource.FromChange(native)],
            cancellationToken);

        // Even an ignored draft removal must reach lifecycle reconciliation.
        Assert.Equal(impact == ChangeImpact.Remove, batch.Keys.Contains(key));
        Assert.Empty(batch.Documents);
        strategy.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CollectionPreservesCallbackOrderAndSeparatesExportAndSearchCultures(bool rebuild)
    {
        Guid key = Guid.NewGuid(), removed = Guid.NewGuid(), group = Guid.NewGuid();
        var strategy = new Mock<IPublishedContentChangeStrategy>(MockBehavior.Strict);

        async Task Collect(ContentIndexInfo index)
        {
            await index.Indexer.AddOrUpdateAsync(
                index.IndexAlias,
                removed,
                UmbracoObjectTypes.Document,
                [new Variation("en-US", null)],
                [],
                null);
            await index.Indexer.DeleteAsync(index.IndexAlias, [removed]);
            await index.Indexer.AddOrUpdateAsync(
                index.IndexAlias,
                key,
                UmbracoObjectTypes.Document,
                [new Variation(null, null), new Variation("da-DK", null), new Variation("en-US", "vip")],
                [],
                new ContentProtection([group]));
        }

        if (rebuild)
        {
            strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
                .Returns((ContentIndexInfo index, CancellationToken _) => Collect(index));
        }
        else
        {
            strategy.Setup(value => value.HandleAsync(
                    It.IsAny<IEnumerable<ContentIndexInfo>>(),
                    It.IsAny<IEnumerable<ContentChange>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ContentIndexInfo> indexes, IEnumerable<ContentChange> _, CancellationToken _) =>
                    Collect(Assert.Single(indexes)));
        }

        var source = new UmbracoSearchExportSource(strategy.Object);
        ExportSourceBatch batch = rebuild
            ? await source.CollectAllAsync(CancellationToken.None)
            : await source.CollectAsync(
                [new ExportChange(key, ExportChangeImpact.Refresh, true)],
                CancellationToken.None);

        Assert.Equal(new[] { key, removed }.Order(), batch.Keys.Order());
        KeyValuePair<Guid, ExportSourceDocument> entry = Assert.Single(batch.Documents);
        Assert.Equal(key, entry.Key);
        Assert.Equal(new[] { null, "da-DK", "en-US" }, entry.Value.Cultures);
        Assert.Equal(new[] { "", "da-dk" }, entry.Value.SearchCultures);
        Assert.Equal(new[] { group }, entry.Value.AccessIds);
        strategy.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledCollectionDoesNotInvokeTheFramework(bool rebuild)
    {
        var strategy = new Mock<IPublishedContentChangeStrategy>(MockBehavior.Strict);
        var source = new UmbracoSearchExportSource(strategy.Object);
        var token = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rebuild
            ? source.CollectAllAsync(token)
            : source.CollectAsync([], token));
        strategy.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedCollectionIsNotReusedByTheNextOperation()
    {
        var key = Guid.NewGuid();
        int attempt = 0;
        var strategy = new Mock<IPublishedContentChangeStrategy>();
        strategy.Setup(value => value.RebuildAsync(It.IsAny<ContentIndexInfo>(), It.IsAny<CancellationToken>()))
            .Returns(async (ContentIndexInfo index, CancellationToken _) =>
            {
                if (attempt++ == 0)
                {
                    await index.Indexer.DeleteAsync(index.IndexAlias, [key]);
                    throw new InvalidOperationException("Discovery failed");
                }
            });
        var source = new UmbracoSearchExportSource(strategy.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.CollectAllAsync(CancellationToken.None));
        ExportSourceBatch batch = await source.CollectAllAsync(CancellationToken.None);
        Assert.Empty(batch.Keys);
        Assert.Empty(batch.Documents);
    }
}
