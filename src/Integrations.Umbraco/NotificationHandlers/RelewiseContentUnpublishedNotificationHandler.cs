using System.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Filters;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Language = Relewise.Client.DataTypes.Language;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentUnpublishedNotificationHandler : INotificationHandler<ContentUnpublishedNotification>
{
    private readonly RelewiseConfiguration _configuration;
    private readonly ITracker _tracker;

    public RelewiseContentUnpublishedNotificationHandler(RelewiseConfiguration configuration, ITracker tracker)
    {
        _configuration = configuration;
        _tracker = tracker;
    }

    public void Handle(ContentUnpublishedNotification notification)
    {
        string[] ids = notification.UnpublishedEntities
            .Where(x => _configuration.IsTrackable(x))
            .Select(x => x.Id.ToString())
            .ToArray();

        if (ids.Length == 0)
            return;

        _tracker.Track(new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(ids)),
            ContentAdministrativeAction.UpdateKind.DisableInRecommendations));
    }
}