using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentDeletedNotificationNotificationHandler : INotificationAsyncHandler<ContentDeletedNotification>
{
    private readonly IExportContentService _exportContentService;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseContentDeletedNotificationNotificationHandler(IExportContentService exportContentService, RelewiseUmbracoConfiguration configuration)
    {
        _exportContentService = exportContentService;
        _configuration = configuration;
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        ITracker? tracker = _exportContentService.GetTrackerOrNull();

        if (tracker == null)
            return;

        string[] ids = notification.DeletedEntities
            .Where(x => _configuration.CanMap(x))
            .Select(x => x.Id.ToString())
            .ToArray();

        if (ids.Length == 0)
            return;

        var action = new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(ids)),
            ContentAdministrativeAction.UpdateKind.PermanentlyDelete);

        await tracker.TrackAsync(action, cancellationToken);
    }
}