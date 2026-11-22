using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco.NotificationHandlers;

internal class RelewiseContentMovedNotificationHandler : INotificationAsyncHandler<ContentMovedNotification>
{
    private readonly IExportContentService _exportContentService;

    public RelewiseContentMovedNotificationHandler(IExportContentService exportContentService)
    {
        _exportContentService = exportContentService;
    }

    public async Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken)
    {
        await _exportContentService.Export(new ExportContent(
                [.. notification.MoveInfoCollection.Select(x => x.Entity)],
                ContentUpdate.UpdateKind.ReplaceProvidedProperties),
            cancellationToken);
    }
}
