using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Services;

internal interface IExportContentService
{
    // NOTE: Bør tage imod et objekt som wrapper nedenstående parametre
    // NOTE: Bør også tage imod en token
    Task Export(ExportContent exportContent, CancellationToken token);

    // NOTE: Bør tageimod et objekt - også selvom det er tomt
    Task ExportAll(CancellationToken token);

    // NOTE: Begge metoder bør returnere et objekt - også selvom du smider noget ind i objektet
}