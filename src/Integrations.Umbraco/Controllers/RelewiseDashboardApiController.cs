using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Web.BackOffice.Filters;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;

namespace Relewise.Integrations.Umbraco.Controllers;

[JsonCamelCaseFormatter]
[PluginController("Relewise")]
[Route("DashboardApi")]
public class RelewiseDashboardApiController : UmbracoAuthorizedController
{
    private readonly RelewiseConfiguration _configuration;
    private readonly IExportContentService _exportContent;

    public RelewiseDashboardApiController(RelewiseConfiguration configuration, IExportContentService exportContent)
    {
        _configuration = configuration;
        _exportContent = exportContent;
    }

    public IActionResult GetConfiguration()
    {
        return Ok(new { _configuration.ApiKey, _configuration.DatasetId });
    }

    public async Task<IActionResult> ContentExport(CancellationToken token)
    {
        await _exportContent.ExportAll(token);

        return Ok();
    }
}