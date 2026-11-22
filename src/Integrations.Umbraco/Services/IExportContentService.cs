using System;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client;
using Relewise.Integrations.Umbraco.Infrastructure;

namespace Relewise.Integrations.Umbraco.Services;

/// <summary>Exports configured Umbraco content to Relewise.</summary>
public interface IExportContentService
{
    /// <summary>Export the supplied content through the shared indexing pipeline.</summary>
    /// <param name="exportContent">The content and update options to export.</param>
    /// <param name="cancellationToken">Cancellation of this export operation.</param>
    /// <returns>The result after export processing completes.</returns>
    /// <remarks>Implement this method for new services. Existing services may implement only Export.</remarks>
    /// <exception cref="InvalidOperationException">Neither method for this export operation is implemented.</exception>
    Task<ExportContentResult> ExportAsync(ExportContent exportContent, CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // Bridge existing custom service implementations without changing their contract.
        AsyncApiCompatibility.RequireLegacyImplementation(
            this,
            nameof(Export),
            nameof(ExportAsync));

        return Export(exportContent, cancellationToken);
#pragma warning restore CS0618
    }

    /// <summary>Export all configured content through the shared indexing pipeline.</summary>
    /// <param name="exportAllContent">The full-export and reconciliation options.</param>
    /// <param name="cancellationToken">Cancellation of this export operation.</param>
    /// <returns>The result after export processing completes.</returns>
    /// <remarks>Implement this method for new services. Existing services may implement only ExportAll.</remarks>
    /// <exception cref="InvalidOperationException">Neither method for this export operation is implemented.</exception>
    Task<ExportAllContentResult> ExportAllAsync(
        ExportAllContent exportAllContent,
        CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // Bridge existing custom service implementations without changing their contract.
        AsyncApiCompatibility.RequireLegacyImplementation(
            this,
            nameof(ExportAll),
            nameof(ExportAllAsync));

        return ExportAll(exportAllContent, cancellationToken);
#pragma warning restore CS0618
    }

    /// <summary>Compatibility entry point forwarding to ExportAsync for new implementations.</summary>
    /// <param name="exportContent">The content and update options to export.</param>
    /// <param name="token">Cancellation of this export operation.</param>
    /// <returns>The result after export processing completes.</returns>
    [Obsolete("Use ExportAsync instead. This member will be removed in the next major version.")]
    Task<ExportContentResult> Export(ExportContent exportContent, CancellationToken token)
    {
        return ExportAsync(exportContent, token);
    }

    /// <summary>Compatibility entry point forwarding to ExportAllAsync for new implementations.</summary>
    /// <param name="exportAllContent">The full-export and reconciliation options.</param>
    /// <param name="token">Cancellation of this export operation.</param>
    /// <returns>The result after export processing completes.</returns>
    [Obsolete("Use ExportAllAsync instead. This member will be removed in the next major version.")]
    Task<ExportAllContentResult> ExportAll(ExportAllContent exportAllContent, CancellationToken token)
    {
        return ExportAllAsync(exportAllContent, token);
    }

    /// <summary>
    /// Returns the Tracker used by the integration, or null when no Tracker is configured.
    /// </summary>
    ITracker? GetTrackerOrNull();
}
