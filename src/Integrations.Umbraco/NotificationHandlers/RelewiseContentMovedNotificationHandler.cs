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
    private readonly IExportContentService _exportContentService;
    private readonly RelewiseUmbracoConfiguration _configuration;

    public RelewiseContentMovedNotificationHandler(IExportContentService exportContentService, RelewiseUmbracoConfiguration configuration)
    {
        _exportContentService = exportContentService;
        _configuration = configuration;
    }

    public async Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken)
    {
        await _exportContentService.Export(new ExportContent(
            notification.MoveInfoCollection.Select(x => x.Entity).ToArray(), 
            ContentUpdate.UpdateKind.ReplaceProvidedProperties), 
            cancellationToken);
    }
}