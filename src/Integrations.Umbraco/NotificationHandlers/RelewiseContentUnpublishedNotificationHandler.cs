using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Filters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Language = Relewise.Client.DataTypes.Language;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentUnpublishedNotificationHandler : INotificationAsyncHandler<ContentUnpublishedNotification>
{
    private readonly IExportContentService _exportContentService;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseContentUnpublishedNotificationHandler(IExportContentService exportContentService, RelewiseUmbracoConfiguration configuration)
    {
        _exportContentService = exportContentService;
        _configuration = configuration;
    }

    public async Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        ITracker? tracker = _exportContentService.GetTrackerOrNull();

        if (tracker == null)
            return;

        string[] ids = notification.UnpublishedEntities
            .Where(x => _configuration.CanMap(x))
            .Select(x => x.Id.ToString())
            .ToArray();

        if (ids.Length == 0)
            return;

        var action = new ContentAdministrativeAction(
            Language.Undefined,
            Currency.Undefined,
            new FilterCollection(new ContentIdFilter(ids)),
            ContentAdministrativeAction.UpdateKind.DisableInRecommendations);

        await tracker.TrackAsync(action, cancellationToken);
    }
}