using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using static Relewise.Integrations.Umbraco.Tests.Mapping.ContentIdentityTestSupport;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class ContentLifecycleTests
{
    [Theory]
    [InlineData(false, "unpublish")]
    [InlineData(true, "unpublish")]
    [InlineData(false, "recycle")]
    [InlineData(true, "recycle")]
    [InlineData(false, "delete")]
    [InlineData(true, "delete")]
    public async Task LifecycleActionsTargetConfiguredIdentityAndIgnoreUnregisteredTypes(
        bool useKeys,
        string operation)
    {
        RelewiseUmbracoConfiguration configuration = Configuration(useKeys.ToString());
        var tracker = new Mock<ITracker>();
        ContentAdministrativeAction? captured = null;
        using var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;

        tracker.Setup(x => x.TrackAsync(It.IsAny<ContentAdministrativeAction>(), cancellationToken))
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
                    .HandleAsync(new ContentUnpublishedNotification(content, messages), cancellationToken);
                break;
            case "recycle":
                await new RelewiseContentMovedToRecycleBinNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(
                        new ContentMovedToRecycleBinNotification(
                            content.Select(x => new MoveToRecycleBinEventInfo<IContent>(x, "-1")),
                            messages),
                        cancellationToken);
                break;
            case "delete":
                await new RelewiseContentDeletedNotificationNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentDeletedNotification(content[0], messages), cancellationToken);
                await new RelewiseContentDeletedNotificationNotificationHandler(exporter.Object, configuration)
                    .HandleAsync(new ContentDeletedNotification(content[1], messages), cancellationToken);
                break;
        }

        Assert.NotNull(captured);

        JObject actionJson = JObject.Parse(JsonConvert.SerializeObject(captured));
        string[] ids = actionJson.SelectTokens("$..ContentIds[*]").Values<string>().ToArray()!;

        Assert.Equal([useKeys ? SharedKey.ToString("D") : "1234"], ids);
        Assert.Equal(
            operation == "delete"
                ? ContentAdministrativeAction.UpdateKind.Delete
                : ContentAdministrativeAction.UpdateKind.Disable,
            captured.Kind);
        tracker.Verify(x => x.TrackAsync(It.IsAny<ContentAdministrativeAction>(), cancellationToken), Times.Once);
    }
}
