using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;

namespace Relewise.Integrations.Umbraco.Services;

/// <summary>
/// Service that exports one of more content pages to Relewise
/// </summary>
public interface IExportContentService
{
    /// <summary>
    /// Exports specific content to Relewise
    /// </summary>
    /// <param name="exportContent"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<ExportContentResult> Export(ExportContent exportContent, CancellationToken token);

    /// <summary>
    /// Export all registered content to Relewise
    /// </summary>
    /// <param name="exportAllContent"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<ExportAllContentResult> ExportAll(ExportAllContent exportAllContent, CancellationToken token);

    /// <summary>
    /// Returns the <see cref="ITracker"/> instance to be used for all integration scenarios.
    /// If instance returned is null, then Relewise has not been configured yet.
    /// </summary>
    ITracker? GetTrackerOrNull();
}