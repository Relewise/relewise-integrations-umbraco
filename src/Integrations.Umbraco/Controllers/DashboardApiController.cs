using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IExportContentService _exportContent;
    private readonly IServiceProvider _provider;
    private readonly RelewiseUmbracoConfiguration _configuration;

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
        await _exportContent.ExportAll(new ExportAllContent(permanentlyDelete), token);

        return Ok();
    }

    /// <summary>
    /// Returns the current Relewise configuration
    /// </summary>
    [HttpGet]
    public IActionResult Configuration()
    {
        IRelewiseClientFactory clientFactory;

        try
        {
            clientFactory = _provider.GetRequiredService<IRelewiseClientFactory>();

            if (!clientFactory.Contains<ITracker>(Constants.NamedClientName))
                throw new InvalidOperationException("No clients registered.");
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                FactoryFailed = true,
                ErrorMessage = ex.Message
            });
        }

        List<NamedOptionsViewObject> clients = clientFactory.ClientNames
            .Where(x => !Constants.NamedClientName.Equals(x, StringComparison.OrdinalIgnoreCase))
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
                new ClientOptionsViewObject(clientFactory.GetOptions<IDataAccessor>())));
        }
        catch (ArgumentException)
        {
            // we'll just ignore this exception as it just means that there no default client has been configured, which is okay
        }

        return Ok(new ConfigurationViewModel(
            _configuration.TrackedContentTypes,
            _configuration.ExportedContentTypes,
            clients,
            RelewiseContentMiddleware.IsEnabled
        ));
    }
}