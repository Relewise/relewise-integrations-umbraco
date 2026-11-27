using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.Extensions;
using Relewise.Integrations.Umbraco.Indexing;

namespace Relewise.Integrations.Umbraco.Services;

internal class ExportContentService(
    IExportSystem exports,
    IServiceProvider services) : IExportContentService
{
    public async Task<ExportContentResult> ExportAsync(
        ExportContent exportContent,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(exportContent);
        await exports.ExportAsync(
            exportContent.Contents.Select(content => content.Key),
            exportContent.UpdateKind,
            exportContent.Version,
            token);

        return new ExportContentResult();
    }

    public async Task<ExportAllContentResult> ExportAllAsync(
        ExportAllContent exportAllContent,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(exportAllContent);
        await exports.RebuildAsync(exportAllContent.PermanentlyDelete, token);

        return new ExportAllContentResult();
    }

    public ITracker? GetTrackerOrNull()
    {
        try
        {
            return services.GetRequiredService<IRelewiseClientFactory>()
                .GetClient<ITracker>(
                    Constants.ClientNames.Integration);
        }
        catch (InvalidOperationException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
