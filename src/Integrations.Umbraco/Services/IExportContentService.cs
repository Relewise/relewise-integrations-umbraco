using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Services;

public interface IExportContentService
{
    Task<ExportContentResult> Export(ExportContent exportContent, CancellationToken token);

    Task<ExportAllContentResult> ExportAll(ExportAllContent exportAllContent, CancellationToken token);
}