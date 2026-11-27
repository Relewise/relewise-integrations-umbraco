using Moq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.Indexing;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class EntityExportDeliveryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task OnlyPermanentlyDeletedOwnedEntitiesSkipAvailabilityReads(
        bool product,
        bool delete,
        bool synchronize)
    {
        var reader = new Mock<IDataAccessor>(MockBehavior.Strict);
        reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([new ProductResultDetails("entity")], 1));
        reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentDetailsCollectionResponse([new ContentResultDetails("entity")], 1));
        var sent = new List<Trackable>();
        var tracker = new Mock<ITracker>();
        tracker.Setup(value => value.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => sent.AddRange(items))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IRelewiseClientFactory>();
        clients.Setup(value => value.GetClient<IDataAccessor>(It.IsAny<string>())).Returns(reader.Object);
        clients.Setup(value => value.GetClient<ITracker>(It.IsAny<string>())).Returns(tracker.Object);
        var request = new EntityReconciliationRequest(
            product ? typeof(Product) : typeof(Content),
            new FilterCollection(product ? new ProductIdFilter("entity") : new ContentIdFilter("entity")),
            Synchronize: synchronize,
            Delete: delete);

        await EntityExportDelivery.ReconcileAsync(clients.Object, [request], CancellationToken.None);

        bool read = !synchronize || !delete;
        Assert.Equal(read ? 1 : 0, reader.Invocations.Count);
        Assert.Equal((read ? 1 : 0) + (synchronize ? 1 : 0), sent.Count);
        if (read)
        {
            Dictionary<string, DataValue?>? data = product
                ? Assert.IsType<ProductUpdate>(sent[0]).Product.Data
                : Assert.IsType<ContentUpdate>(sent[0]).Content.Data;
            Assert.False(data![RelewiseDataKeys.Published]!.ValueAs<bool>());
        }

        if (synchronize)
        {
            if (product)
            {
                Assert.Equal(
                    delete
                        ? ProductAdministrativeAction.UpdateKind.Delete
                        : ProductAdministrativeAction.UpdateKind.Disable,
                    Assert.IsType<ProductAdministrativeAction>(sent[^1]).ProductUpdateKind);
            }
            else
            {
                Assert.Equal(
                    delete
                        ? ContentAdministrativeAction.UpdateKind.Delete
                        : ContentAdministrativeAction.UpdateKind.Disable,
                    Assert.IsType<ContentAdministrativeAction>(sent[^1]).Kind);
            }
        }
    }
}
