using System.Linq;
using System.Threading;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentPublishedNotificationHandler : INotificationHandler<ContentPublishedNotification>
{
    private readonly IExportContentService _exportContentService;

    public RelewiseContentPublishedNotificationHandler(IExportContentService exportContentService)
    {
        _exportContentService = exportContentService;
    }

    public void Handle(ContentPublishedNotification notification)
    {
        _exportContentService.Export(new ExportContent(notification.PublishedEntities.ToArray()), CancellationToken.None);
    }
}