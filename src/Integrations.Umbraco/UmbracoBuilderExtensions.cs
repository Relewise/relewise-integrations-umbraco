using System;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Extensions methods for setting up Relewise in an <see cref="IServiceCollection"/>.
/// </summary>
public static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Registers services and configures <see cref="RelewiseUmbracoOptionsBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IUmbracoBuilder"/></param>
    /// <param name="configure">A delegate to configure <see cref="RelewiseUmbracoOptionsBuilder"/></param>
    /// <returns>The <see cref="IUmbracoBuilder"/></returns>
    public static IUmbracoBuilder AddRelewise(this IUmbracoBuilder builder, Action<RelewiseUmbracoOptionsBuilder>? configure = null)
    {
        configure ??= _ => { };

        return builder.AddRelewise((optionsBuilder, _) => configure(optionsBuilder));
    }

    /// <summary>
    /// Registers services and configures <see cref="RelewiseUmbracoOptionsBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IUmbracoBuilder"/></param>
    /// <param name="configure">A delegate to configure <see cref="RelewiseUmbracoOptionsBuilder"/> which also includes the <see cref="IServiceProvider"/> if you need to do service lookups part of this configuration.</param>
    /// <returns>The <see cref="IUmbracoBuilder"/></returns>
    public static IUmbracoBuilder AddRelewise(this IUmbracoBuilder builder, Action<RelewiseUmbracoOptionsBuilder, IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        builder.Services.AddRelewise(x => x.Named.Add(Constants.NamedClientName, clientOptions =>
        {
            clientOptions.Tracker.Timeout = TimeSpan.FromMinutes(2);
        }));

        builder.Services.AddSingleton(new RelewiseUmbracoConfiguration.Configure(configure));

        builder.Services.TryAddSingleton(s => new RelewiseUmbracoConfiguration(s));

        builder.Services.TryAddSingleton<IContentMapper, ContentMapper>();
        builder.Services.TryAddSingleton<IExportContentService, ExportContentService>();
        builder.Services.TryAddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();
        builder.Services.TryAddSingleton<IRelewiseUserLocator, DefaultAnonymousUserLocator>();

        builder.Services
            .AddValueConverter<TextFieldPropertyValueConverter>()
            .AddValueConverter<CheckboxPropertyValueConverter>()
            .AddValueConverter<IntegerPropertyValueConverter>()
            .AddValueConverter<DecimalPropertyValueConverter>()
            .AddValueConverter<TagsPropertyValueConverter>()
            .AddValueConverter<NestedContentPropertyValueConverter>();

        builder.AddNotificationAsyncHandler<ContentPublishedNotification, RelewiseContentPublishedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentUnpublishedNotification, RelewiseContentUnpublishedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, RelewiseContentDeletedNotificationNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedNotification, RelewiseContentMovedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, RelewiseContentMovedToRecycleBinNotificationHandler>();

        //builder.Dashboards().Add<RelewiseDashboard>();

        builder.Services.Configure<UmbracoPipelineOptions>(umbPipOptions =>
        {
            umbPipOptions.AddFilter(new UmbracoPipelineFilter(nameof(DashboardApiController))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    GlobalSettings globalSettings = app.ApplicationServices.GetRequiredService<IOptions<GlobalSettings>>().Value;
                    IHostingEnvironment hostingEnvironment = app.ApplicationServices.GetRequiredService<IHostingEnvironment>();
                    string backOfficeArea = global::Umbraco.Cms.Core.Constants.Web.Mvc.BackOfficePathSegment;

                    var rootSegment = $"{globalSettings.GetUmbracoMvcArea(hostingEnvironment)}/{backOfficeArea}";
                    endpoints.MapUmbracoRoute<DashboardApiController>(
                        rootSegment: rootSegment,
                        areaName: "Relewise",
                        prefixPathSegment: "Relewise");
                })
            });
        });

        return builder;
    }
}


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
        builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            // Related documentation:
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/adding-a-custom-swagger-document
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/versioning-your-api
            // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/access-policies

            // Configure the Swagger generation options
            // Add in a new Swagger API document solely for our own package that can be browsed via Swagger UI
            // Along with having a generated swagger JSON file that we can use to auto generate a TypeScript client
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Custom Welcome Dashboard Backoffice API",
                Version = "1.0",
                // Contact = new OpenApiContact
                // {
                //     Name = "Some Developer",
                //     Email = "you@company.com",
                //     Url = new Uri("https://company.com")
                // }
            });

            // Enable Umbraco authentication for the "Example" Swagger document
            // PR: https://github.com/umbraco/Umbraco-CMS/pull/15699
            opt.OperationFilter<CustomWelcomeDashboardOperationSecurityFilter>();
        });
    }

    private class CustomWelcomeDashboardOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }

    // This is used to generate nice operation IDs in our swagger json file
    // So that the gnerated TypeScript client has nice method names and not too verbose
    // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/umbraco-schema-and-operation-ids#operation-ids
    private class CustomOperationHandler : OperationIdHandler
    {
        public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
        {
        }

        protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
        {
            return controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("CustomWelcomeDashboard.Controllers", comparisonType: StringComparison.InvariantCultureIgnoreCase) is true;
        }

        public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
    }
}