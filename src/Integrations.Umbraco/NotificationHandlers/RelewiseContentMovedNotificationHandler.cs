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

internal class RelewiseContentMovedNotificationHandler : INotificationAsyncHandler<ContentMovedNotification>
{
    private const int TrashCanId = -1;

    private readonly IExportContentService _exportContentService;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseContentMovedNotificationHandler(IExportContentService exportContentService, RelewiseUmbracoConfiguration configuration)
    {
        _exportContentService = exportContentService;
        _configuration = configuration;
    }

    public async Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken)
    {
        ITracker? tracker = _exportContentService.GetTrackerOrNull();

        if (tracker == null)
            return;

        string[] ids = notification.MoveInfoCollection
            .Where(x => _configuration.CanMap(x.Entity) && x.NewParentId == TrashCanId)
            .Select(x => x.Entity.Id.ToString())
            .ToArray();

        if (ids.Length == 0)
            return;

        var action = new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(ids)),
            ContentAdministrativeAction.UpdateKind.Disable);

        await tracker.TrackAsync(action, cancellationToken);
    }
}