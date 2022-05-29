using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client;
using Relewise.Client.Extensions;
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

    public DashboardApiController(IExportContentService exportContent, IServiceProvider provider)
    {
        _exportContent = exportContent;
        _provider = provider;
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
        RelewiseClientOptions globalOptions = clientFactory.GetOptions<ITracker>();

        return Ok(new
        {
            DatasetId = globalOptions.DatasetId,
            Timeout = globalOptions.Timeout.TotalSeconds,
            Named = new (string Name, Guid DatasetId, double TimeoutInSeconds)[]
            {
                new ("Integration", globalOptions.DatasetId, globalOptions.Timeout.TotalSeconds)
            }
        });
    }
}