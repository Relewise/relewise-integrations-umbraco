using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.Tests;

public class ContentIdentityTests
{
    private static readonly Guid SharedKey = Guid.Parse("1a234567-89ab-4cde-8f01-23456789abcd");

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void EnvironmentConfigurationSelectsIdentityForPublishedAndUnpublishedContent(string? setting, bool useKeys)
    {
        var configuration = Configuration(setting);
        var published = PublishedContent(1234, SharedKey);
        var editable = EditableContent(1234, SharedKey);
        string expected = useKeys ? SharedKey.ToString("D") : "1234";

        Assert.Equal(useKeys, configuration.UseContentKeysAsIds);
        Assert.Equal(expected, configuration.GetContentId(published));
        Assert.Equal(expected, configuration.GetContentId(editable));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegistrationCanOverrideEnvironmentConfiguration(bool useKeys)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Relewise:Umbraco:UseContentKeysAsIds"] = (!useKeys).ToString() }).Build());
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.UseContentKeysAsIds = useKeys);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(useKeys, new RelewiseUmbracoConfiguration(provider).UseContentKeysAsIds);
    }

    [Fact]
    public void SharedKeyMatchesAcrossDifferentLocalIdsOnlyWhenOptedIn()
    {
        var first = PublishedContent(1234, SharedKey);
        var second = PublishedContent(9876, SharedKey);
        var keys = Configuration("true");
        var numeric = Configuration(null);

        Assert.Equal(keys.GetContentId(first), keys.GetContentId(second));
        Assert.NotEqual(numeric.GetContentId(first), numeric.GetContentId(second));
        Assert.NotEqual(keys.GetContentId(first), keys.GetContentId(PublishedContent(1234, Guid.NewGuid())));
    }

    [Theory]
    [InlineData(false, "unpublish")]
    [InlineData(true, "unpublish")]
    [InlineData(false, "recycle")]
    [InlineData(true, "recycle")]
    [InlineData(false, "delete")]
    [InlineData(true, "delete")]
    public async Task LifecycleActionsTargetConfiguredIdentityAndIgnoreUnregisteredTypes(bool useKeys, string operation)
    {
        var configuration = Configuration(useKeys.ToString());
        var tracker = new Mock<ITracker>();
        ContentAdministrativeAction? captured = null;
        using var cancellation = new CancellationTokenSource();
        tracker.Setup(x => x.TrackAsync(It.IsAny<ContentAdministrativeAction>(), cancellation.Token))
            .Callback<ContentAdministrativeAction, CancellationToken>((action, _) => captured = action)
            .Returns(Task.CompletedTask);
        var exporter = new Mock<IExportContentService>();
        exporter.Setup(x => x.GetTrackerOrNull()).Returns(tracker.Object);
        IContent[] content = [EditableContent(1234, SharedKey), EditableContent(7777, Guid.NewGuid(), "unregistered")];
        var messages = new EventMessages();

        switch (operation)
        {
            case "unpublish":
                await new RelewiseContentUnpublishedNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentUnpublishedNotification(content, messages), cancellation.Token);
                break;
            case "recycle":
                await new RelewiseContentMovedToRecycleBinNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentMovedToRecycleBinNotification(content.Select(x => new MoveToRecycleBinEventInfo<IContent>(x, "-1")), messages), cancellation.Token);
                break;
            case "delete":
                await new RelewiseContentDeletedNotificationNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentDeletedNotification(content[0], messages), cancellation.Token);
                await new RelewiseContentDeletedNotificationNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentDeletedNotification(content[1], messages), cancellation.Token);
                break;
        }

        Assert.NotNull(captured);
        JObject actionJson = JObject.Parse(JsonConvert.SerializeObject(captured));
        string[] ids = actionJson.SelectTokens("$..ContentIds[*]").Values<string>().ToArray()!;
        Assert.Equal([useKeys ? SharedKey.ToString("D") : "1234"], ids);
        Assert.Equal(operation == "delete" ? ContentAdministrativeAction.UpdateKind.Delete : ContentAdministrativeAction.UpdateKind.Disable, captured.Kind);
        tracker.Verify(x => x.TrackAsync(It.IsAny<ContentAdministrativeAction>(), cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutomaticViewsUseExportIdentityAndDoNotTrackPreview(bool useKeys, bool preview)
    {
        var published = new Mock<IPublishedContent>();
        published.SetupGet(x => x.Id).Returns(1234);
        published.SetupGet(x => x.Key).Returns(SharedKey);
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns("article");
        published.SetupGet(x => x.ContentType).Returns(contentType.Object);
        var request = new Mock<IPublishedRequest>();
        request.SetupGet(x => x.PublishedContent).Returns(published.Object);
        var umbraco = new Mock<IUmbracoContext>();
        umbraco.SetupGet(x => x.PublishedRequest).Returns(request.Object);
        umbraco.SetupGet(x => x.InPreviewMode).Returns(preview);
        var factory = new Mock<IUmbracoContextFactory>();
        factory.Setup(x => x.EnsureUmbracoContext()).Returns(() => new UmbracoContextReference(umbraco.Object, false, Mock.Of<IUmbracoContextAccessor>()));
        var tracker = new Mock<ITracker>();
        ContentView? captured = null;
        tracker.Setup(x => x.TrackAsync(It.IsAny<ContentView>(), It.IsAny<CancellationToken>()))
            .Callback<ContentView, CancellationToken>((view, _) => captured = view).Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(factory.Object);
        services.AddSingleton(tracker.Object);
        using var provider = services.BuildServiceProvider();
        var userLocator = new Mock<IRelewiseUserLocator>();
        userLocator.Setup(x => x.GetUser()).ReturnsAsync(User.Anonymous());
        bool nextCalled = false;
        var middleware = new RelewiseContentMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, Configuration(useKeys.ToString()), userLocator.Object);

        await middleware.InvokeAsync(new DefaultHttpContext { RequestServices = provider });

        Assert.True(nextCalled);
        if (preview)
            Assert.Null(captured);
        else
        {
            Assert.NotNull(captured);
            Assert.Equal(useKeys ? SharedKey.ToString("D") : "1234", captured.Content.Id);
        }
    }

    private static RelewiseUmbracoConfiguration Configuration(string? value)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Relewise:Umbraco:UseContentKeysAsIds"] = value }).Build());
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => options.AddContentType("article", type => type.AutoMap()));
        using var provider = services.BuildServiceProvider();
        return new RelewiseUmbracoConfiguration(provider);
    }

    private static IPublishedContent PublishedContent(int id, Guid key)
    {
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Key).Returns(key);
        return content.Object;
    }

    private static IContent EditableContent(int id, Guid key, string alias = "article")
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Alias).Returns(alias);
        var content = new Mock<IContent>();
        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        return content.Object;
    }
}
