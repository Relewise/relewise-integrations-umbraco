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

[JsonCamelCaseFormatter]
[PluginController("Relewise")]
public class DashboardApiController : UmbracoAuthorizedController
{
    private readonly IExportContentService _exportContent;
    private readonly IServiceProvider _provider;
    private readonly RelewiseConfiguration _configuration;

    public DashboardApiController(IExportContentService exportContent, IServiceProvider provider, RelewiseConfiguration configuration)
    {
        _exportContent = exportContent;
        _provider = provider;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> ContentExport(CancellationToken token)
    {
        await _exportContent.ExportAll(new ExportAllContent(), token);

        return Ok();
    }

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
            Mapping = new { _configuration.TrackableDocTypes },
            Named = named
        });
    }

    public class NamedOptionsViewObject
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

    public class ClientOptionsViewObject
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