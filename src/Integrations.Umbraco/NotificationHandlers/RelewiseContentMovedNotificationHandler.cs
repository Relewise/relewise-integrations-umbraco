using System.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Filters;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentMovedNotificationHandler : INotificationHandler<ContentMovedNotification>
{
    private const int TrashCanId = -1;

    private readonly RelewiseUmbracoConfiguration _configuration;
    private readonly ITracker _tracker;

    public RelewiseContentMovedNotificationHandler(RelewiseUmbracoConfiguration configuration, ITracker tracker)
    {
        _configuration = configuration;
        _tracker = tracker;
    }

    public void Handle(ContentMovedNotification notification)
    {
        string[] ids = notification.MoveInfoCollection
            .Where(x => _configuration.IsTrackable(x.Entity) && x.NewParentId == TrashCanId)
            .Select(x => x.Entity.Id.ToString())
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