using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class NotificationExportOperationsTests
{
    [Fact]
    public async Task FullExportWorksWithoutUmbracoSearchAndReconcilesMappedEntities()
    {
        await using var fixture = new Fixture();
        await fixture.Exports.RebuildAsync();
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(fixture.Sent));
        Assert.Equal(ContentAdministrativeAction.UpdateKind.Disable, action.Kind);
        fixture.Content.Verify(value => value.GetRootContent(), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingPublishedContentIsDisabledOrDeletedAccordingToSourceAvailability(bool deleted)
    {
        await using var fixture = new Fixture();
        var key = Guid.NewGuid();
        if (!deleted)
        {
            fixture.Content.Setup(value => value.GetById(key))
                .Returns(Mock.Of<IContent>(value => value.ContentType.Alias == "article"));
        }

        await fixture.Exports.ExportAsync([key]);
        var action = Assert.IsType<ContentAdministrativeAction>(Assert.Single(fixture.Sent));
        Assert.Equal(
            deleted ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable,
            action.Kind);
    }

    [Fact]
    public async Task CancelledExportDoesNotReadContentOrSendUpdates()
    {
        await using var fixture = new Fixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Exports.RebuildAsync(cancellationToken: new CancellationToken(true)));
        fixture.Content.VerifyNoOtherCalls();
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentServicesAreResolvedAndAsynchronouslyDisposedPerOperation(bool fail)
    {
        var disposals = new List<bool>();
        await using var fixture = new Fixture(services => services.AddScoped<IContentService>(_ =>
        {
            int index = disposals.Count;
            disposals.Add(false);
            var content = new Mock<IContentService>();
            content.Setup(value => value.GetRootContent())
                .Returns(() =>
                {
                    Assert.False(disposals[index]);
                    if (fail)
                        throw new InvalidOperationException("Source failure");
                    return [];
                });
            content.As<IAsyncDisposable>()
                .Setup(value => value.DisposeAsync())
                .Returns(async () =>
                {
                    await Task.Yield();
                    disposals[index] = true;
                });
            return content.Object;
        }));

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (fail)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Exports.RebuildAsync());
            }
            else
            {
                await fixture.Exports.RebuildAsync();
            }

            Assert.Equal(attempt + 1, disposals.Count);
            Assert.All(disposals, disposed => Assert.True(disposed));
        }

        if (fail)
            Assert.Empty(fixture.Sent);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        internal Mock<IContentService> Content { get; } = new();
        internal List<Trackable> Sent { get; } = [];
        internal NotificationExportOperations Exports { get; }

        internal Fixture(Action<IServiceCollection>? configure = null)
        {
            Content.Setup(value => value.GetRootContent()).Returns([]);
            var languages = new Mock<ILanguageService>();
            languages.Setup(value => value.GetAllAsync()).ReturnsAsync(Array.Empty<ILanguage>());
            var tracker = new Mock<ITracker>();
            tracker.Setup(value => value.TrackAsync(It.IsAny<IEnumerable<Trackable>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Trackable>, CancellationToken>((items, _) => Sent.AddRange(items))
                .Returns(Task.CompletedTask);
            var reader = new Mock<IDataAccessor>();
            reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], 0));
            var clients = new Mock<IRelewiseClientFactory>();
            clients.Setup(value => value.GetClient<ITracker>(It.IsAny<string>())).Returns(tracker.Object);
            clients.Setup(value => value.GetClient<IDataAccessor>(It.IsAny<string>())).Returns(reader.Object);
            var contexts = new Mock<IUmbracoContextFactory>();
            contexts.Setup(value => value.EnsureUmbracoContext())
                .Returns(() =>
                    new UmbracoContextReference(Mock.Of<IUmbracoContext>(), false, Mock.Of<IUmbracoContextAccessor>()));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
                options.MapContent("article"));
            services.AddSingleton<RelewiseUmbracoConfiguration>();
            services.AddSingleton(Content.Object);
            services.AddSingleton(languages.Object);
            services.AddSingleton(clients.Object);
            services.AddSingleton(contexts.Object);
            services.AddSingleton<NotificationExportOperations>();
            configure?.Invoke(services);
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            Exports = _services.GetRequiredService<NotificationExportOperations>();
        }

        public ValueTask DisposeAsync() => _services.DisposeAsync();
    }
}
