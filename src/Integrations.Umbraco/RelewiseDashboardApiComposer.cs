using System;
using System.Threading.Tasks;
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
                if (context.Description.ActionDescriptor is ControllerActionDescriptor descriptor &&
                    IsRelewiseController(descriptor))
                {
                    operation.OperationId = context.Description.ActionDescriptor.RouteValues["action"];
                }

                return Task.CompletedTask;
            })));
    }

    private static bool IsRelewiseController(ControllerActionDescriptor descriptor)
    {
        return descriptor.ControllerTypeInfo.Namespace?
            .StartsWith("Relewise.Integrations.Umbraco.Controllers", StringComparison.InvariantCultureIgnoreCase) is true;
    }
}
