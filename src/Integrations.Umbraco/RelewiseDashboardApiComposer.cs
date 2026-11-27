using System;
#if !UMBRACO17
using System.Threading.Tasks;
#endif
using Microsoft.AspNetCore.Mvc.Controllers;
#if UMBRACO17
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
#endif
#if !UMBRACO17
using Umbraco.Cms.Api.Common.OpenApi;
#endif
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
#if UMBRACO17
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(Constants.Api.Name, new OpenApiInfo
            {
                Title = "Relewise Dashboard Backoffice API",
                Version = "1.0"
            });
            options.OperationFilter<DashboardOperationFilter>();
            options.OperationFilter<DashboardSecurityFilter>();
        });
#else
        builder.AddBackOfficeOpenApiDocument(
            Constants.Api.Name,
            document => document
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
#endif
    }

    private static bool IsRelewiseController(ControllerActionDescriptor descriptor)
    {
        return descriptor.ControllerTypeInfo.Namespace?
            .StartsWith(
                "Relewise.Integrations.Umbraco.Controllers",
                StringComparison.InvariantCultureIgnoreCase) is true;
    }

#if UMBRACO17
    private sealed class DashboardSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.Api.Name;
    }

    private sealed class DashboardOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor descriptor &&
                IsRelewiseController(descriptor))
            {
                operation.OperationId = descriptor.ActionName;
            }
        }
    }
#endif
}
