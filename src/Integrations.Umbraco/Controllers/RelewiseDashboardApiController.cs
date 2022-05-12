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
internal class RelewiseDashboardApiController : UmbracoAuthorizedController
{
    private readonly IExportContentService _exportContent;

    public RelewiseDashboardApiController(IExportContentService exportContent)
    {
        _exportContent = exportContent;
    }

    public async Task<IActionResult> ContentExport(CancellationToken token)
    {
        await _exportContent.ExportAll(new ExportAllContent(), token);

        return Ok();
    }
}