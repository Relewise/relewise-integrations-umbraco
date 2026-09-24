using System;
using Microsoft.AspNetCore.Mvc.Controllers;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Composes the Relewise Dashboard API into the Umbraco application.
/// </summary>
public class RelewiseDashboardApiComposer : IComposer
{
    /// <summary>
    /// Composes the Relewise Dashboard API into the Umbraco application.
    /// </summary>
    /// <param name="builder"></param>
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddBackOfficeOpenApiDocument(Constants.ApiName, document => document
            .WithTitle("Relewise Dashboard Backoffice API")
            .WithBackOfficeAuthentication()
            .ConfigureOpenApiOptions(options => options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor &&
                    controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(
                        "Relewise.Integrations.Umbraco.Controllers",
                        comparisonType: StringComparison.InvariantCultureIgnoreCase) is true)
                {
                    operation.OperationId = context.Description.ActionDescriptor.RouteValues["action"];
                }

                return System.Threading.Tasks.Task.CompletedTask;
            })));
    }
}
