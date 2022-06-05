using System.Linq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Filters;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentDeletedNotificationNotificationHandler : INotificationHandler<ContentDeletedNotification>
{
    private readonly RelewiseUmbracoConfiguration _configuration;
    private readonly ITracker _tracker;

    public RelewiseContentDeletedNotificationNotificationHandler(RelewiseUmbracoConfiguration configuration, ITracker tracker)
    {
        _configuration = configuration;
        _tracker = tracker;
    }

    public void Handle(ContentDeletedNotification notification)
    {
        string[] ids = notification.DeletedEntities
            .Where(x => _configuration.IsTrackable(x))
            .Select(x => x.Id.ToString())
            .ToArray();

        if (ids.Length == 0)
            return;

        _tracker.Track(new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(ids)),
            ContentAdministrativeAction.UpdateKind.PermanentlyDelete));
    }
}