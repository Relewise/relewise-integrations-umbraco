using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentPublishedNotificationHandler : INotificationAsyncHandler<ContentPublishedNotification>
{
    private readonly IExportContentService _exportContentService;

    public RelewiseContentPublishedNotificationHandler(IExportContentService exportContentService)
    {
        _exportContentService = exportContentService;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        await _exportContentService.Export(new ExportContent(notification.PublishedEntities.ToArray(), ContentUpdate.UpdateKind.ReplaceProvidedProperties), cancellationToken);
    }
}