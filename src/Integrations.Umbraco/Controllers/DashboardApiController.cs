using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using J2N.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.Extensions;
using Relewise.Client.Search;
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
        IRelewiseClientFactory clientFactory = _provider.GetRequiredService<IRelewiseClientFactory>();

        var named = new List<NamedOptionsViewObject>();

        try
        {
            named.Add(new NamedOptionsViewObject(
                "Default",
                new ClientOptionsViewObject(clientFactory.GetOptions<ITracker>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<IRecommender>()),
                new ClientOptionsViewObject(clientFactory.GetOptions<ISearcher>())));
        }
        catch (ArgumentException)
        {
            // we just swallow the exception here as this just means that there is no default configured, which is okay
        }

        foreach (string name in clientFactory.ClientNames.Where(x => !Constants.NamedClientName.Equals(x, StringComparison.OrdinalIgnoreCase)))
        {
            RelewiseClientOptions trackerOptions = clientFactory.GetOptions<ITracker>(name);
            RelewiseClientOptions recommenderOptions = clientFactory.GetOptions<IRecommender>(name);
            RelewiseClientOptions searcherOptions = clientFactory.GetOptions<ISearcher>(name);

            named.Add(new NamedOptionsViewObject(
                name,
                new ClientOptionsViewObject(trackerOptions),
                new ClientOptionsViewObject(recommenderOptions),
                new ClientOptionsViewObject(searcherOptions)));
        }

        return Ok(new
        {
            TrackedContentTypes = _configuration.TrackedContentTypes,
            ExportedContentTypes = _configuration.ExportedContentTypes,
            Named = named
        });
    }

    private class NamedOptionsViewObject
    {
        public NamedOptionsViewObject(string name, ClientOptionsViewObject tracker, ClientOptionsViewObject recommender, ClientOptionsViewObject searcher)
        {
            Name = name;
            Tracker = tracker;
            Recommender = recommender;
            Searcher = searcher;
        }

        public string Name { get; }

        public ClientOptionsViewObject Tracker { get; }
        public ClientOptionsViewObject Recommender { get; }
        public ClientOptionsViewObject Searcher { get; }
    }

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