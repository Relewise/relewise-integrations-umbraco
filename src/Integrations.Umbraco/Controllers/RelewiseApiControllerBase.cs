using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace Relewise.Integrations.Umbraco.Controllers;

/// <summary>
/// Base controller for endpoints
/// </summary>
[ApiController]
[BackOfficeRoute("relewisedashboard/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
public class RelewiseApiControllerBase : ControllerBase
{
}