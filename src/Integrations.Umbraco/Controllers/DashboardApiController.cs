using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.Extensions;
using Relewise.Client.Search;
using Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Web.BackOffice.Filters;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;

namespace Relewise.Integrations.Umbraco.Controllers;

/// <summary>
/// Defines endpoints for the Dashboard
/// </summary>
[JsonCamelCaseFormatter]
[PluginController("Relewise")]
public class DashboardApiController : UmbracoAuthorizedController
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
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> ContentExport([FromQuery] bool permanentlyDelete, CancellationToken token)
    {
        await _exportContent.ExportAll(new ExportAllContent(permanentlyDelete), token);

        return Ok();
    }

    /// <summary>
    /// Returns the current Relewise configuration
    /// </summary>
    /// <returns></returns>
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

        return Ok(new
        {
            _configuration.TrackedContentTypes,
            _configuration.ExportedContentTypes,
            Named = clients,
            ContentMiddlewareEnabled = RelewiseContentMiddleware.IsEnabled
        });
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private class NamedOptionsViewObject
    {
        public NamedOptionsViewObject(string name, 
            ClientOptionsViewObject tracker, 
            ClientOptionsViewObject recommender, 
            ClientOptionsViewObject searcher,
            ClientOptionsViewObject searchAdministrator,
            ClientOptionsViewObject analyzer,
            ClientOptionsViewObject dataAccessor)
        {
            Name = name;

            Tracker = tracker;
            Recommender = recommender;
            Searcher = searcher;
            SearchAdministrator = searchAdministrator;
            Analyzer = analyzer;
            DataAccessor = dataAccessor;
        }

        public string Name { get; }

        public ClientOptionsViewObject Tracker { get; }
        public ClientOptionsViewObject Recommender { get; }
        public ClientOptionsViewObject Searcher { get; }
        public ClientOptionsViewObject SearchAdministrator { get; }
        public ClientOptionsViewObject Analyzer { get; }
        public ClientOptionsViewObject DataAccessor { get; }
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private class ClientOptionsViewObject
    {
        public ClientOptionsViewObject(RelewiseClientOptions options)
        {
            DatasetId = options.DatasetId;
            Timeout = options.Timeout.TotalSeconds;
        }

        public Guid DatasetId { get; }
        public double Timeout { get; }
    }
}