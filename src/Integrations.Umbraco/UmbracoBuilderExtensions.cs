using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Dashboards;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
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
    public static IUmbracoBuilder AddRelewise(this IUmbracoBuilder builder, Action<RelewiseUmbracoOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

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

        builder.Dashboards().Add<RelewiseDashboard>();

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