using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relewise.Client;
using Relewise.Client.Extensions;
using Relewise.Client.Search;
using Relewise.Integrations.Umbraco.Controllers.Messages;
using Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;
using Relewise.Integrations.Umbraco.Services;

namespace Relewise.Integrations.Umbraco.Controllers;

/// <summary>
/// Defines endpoints for the Dashboard
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "RelewiseDashboard")]
public class DashboardApiController : RelewiseApiControllerBase
{
    private readonly IExportContentService? _exportContent;
    private readonly IServiceProvider _provider;
    private readonly RelewiseUmbracoConfiguration? _configuration;

    /// <summary>Creates the dashboard even when the integration has not been registered.</summary>
    /// <param name="provider">The request service provider.</param>
    [ActivatorUtilitiesConstructor]
    public DashboardApiController(IServiceProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Constructor for API controller
    /// </summary>
    /// <param name="exportContent"></param>
    /// <param name="provider"></param>
    /// <param name="configuration"></param>
    public DashboardApiController(IExportContentService exportContent, IServiceProvider provider, RelewiseUmbracoConfiguration configuration)
    {
        _exportContent = exportContent;
        _provider = provider;
        _configuration = configuration;
    }

    /// <summary>
    /// Performs a full content export
    /// </summary>
    /// <param name="permanentlyDelete"></param>
    /// <param name="token"></param>
    [HttpPost]
    public async Task<IActionResult> ContentExport([FromQuery] bool permanentlyDelete, CancellationToken token)
    {
        var exporter = _exportContent ?? _provider.GetService<IExportContentService>();
        if (exporter == null)
        {
            return Problem("Register the Umbraco integration before exporting.", statusCode: StatusCodes.Status409Conflict);
        }

        await exporter.ExportAll(new ExportAllContent(permanentlyDelete), token);

        return Ok();
    }

    /// <summary>
    /// Returns the current Relewise configuration
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ConfigurationViewModel>(StatusCodes.Status200OK)]
    public IActionResult Configuration()
    {
        RelewiseUmbracoConfiguration? configuration;
        try
        {
            configuration = _configuration ?? _provider.GetService<RelewiseUmbracoConfiguration>();
        }
        catch (Exception ex)
        {
            _provider.GetService<ILogger<DashboardApiController>>()?.LogError(ex, "Could not load Relewise integration configuration.");
            return Ok(new ConfigurationErrorViewModel("The integration configuration could not be loaded. Check the application logs for details."));
        }
        if (configuration == null)
        {
            return Ok(new ConfigurationErrorViewModel("Register AddRelewise on the Umbraco builder to enable mapping, indexing and tracking.")
            {
                IntegrationRegistered = false
            });
        }

        IRelewiseClientFactory clientFactory;
        DashboardClientViewModel[] integrationClients;

        try
        {
            clientFactory = _provider.GetRequiredService<IRelewiseClientFactory>();

            if (!clientFactory.Contains<ITracker>(Constants.NamedClientName))
                throw new InvalidOperationException("No clients registered.");

            integrationClients =
            [
                new("Tracker", new ClientOptionsViewObject(clientFactory.GetOptions<ITracker>(Constants.NamedClientName))),
                new("Searcher", new ClientOptionsViewObject(clientFactory.GetOptions<ISearcher>(Constants.NamedClientName))),
                new("Data accessor", new ClientOptionsViewObject(clientFactory.GetOptions<IDataAccessor>(Constants.NamedClientName)))
            ];
        }
        catch (Exception ex)
        {
            _provider.GetService<ILogger<DashboardApiController>>()?.LogError(ex, "Could not load Relewise client configuration.");
            return Ok(new ConfigurationErrorViewModel("Relewise client settings are unavailable. Configure the Dataset ID, API key and server URL in application configuration, then restart the site."));
        }

        List<NamedOptionsViewObject> clients = clientFactory.ClientNames
            .Where(x => !Constants.NamedClientName.Equals(x, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                RelewiseClientOptions trackerOptions = clientFactory.GetOptions<ITracker>(name);
                RelewiseClientOptions recommenderOptions = clientFactory.GetOptions<IRecommender>(name);
                RelewiseClientOptions searcherOptions = clientFactory.GetOptions<ISearcher>(name);
                RelewiseClientOptions searchAdministratorOptions = clientFactory.GetOptions<ISearchAdministrator>(name);
                RelewiseClientOptions analyzerOptions = clientFactory.GetOptions<IAnalyzer>(name);
                RelewiseClientOptions dataAccessorOptions = clientFactory.GetOptions<IDataAccessor>(name);

                return new NamedOptionsViewObject(
                    name,
                    new ClientOptionsViewObject(trackerOptions),
                    new ClientOptionsViewObject(recommenderOptions),
                    new ClientOptionsViewObject(searcherOptions),
                    new ClientOptionsViewObject(searchAdministratorOptions),
                    new ClientOptionsViewObject(analyzerOptions),
                    new ClientOptionsViewObject(dataAccessorOptions));
            })
            .ToList();

        try
        {
            clients.Insert(0, new NamedOptionsViewObject(
                "Default",
                new ClientOptionsViewObject(clientFactory.GetOptions<ITracker>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<IRecommender>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<ISearcher>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<ISearchAdministrator>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<IAnalyzer>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<IDataAccessor>()))
            {
                IsDefault = true
            });
        }
        catch (ArgumentException)
        {
            // we'll just ignore this exception as it just means that there no default client has been configured, which is okay
        }

        var result = DashboardConfigurationSnapshot.Create(configuration, clients, RelewiseContentMiddleware.IsEnabled);
        result.IntegrationClients = integrationClients;
        result.Connection = integrationClients[0].Options;
        return Ok(result);
    }
}
