using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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

public static class UmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddRelewise(this IUmbracoBuilder builder, Action<RelewiseConfigurationBuilder> options)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (options == null) throw new ArgumentNullException(nameof(options));

        builder.Services.AddRelewise(x => x.Named.Add(Constants.NamedClientName, clientOptions =>
        {
            clientOptions.Tracker.Timeout = TimeSpan.FromMinutes(2);
        }));

        var config = new RelewiseConfigurationBuilder();
        options.Invoke(config);

        builder.Services.AddSingleton(config.MappingConfiguration);

        var relewiseConfiguration = new RelewiseConfiguration(config.MappingConfiguration.AutoMappedDocTypes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>());
        builder.Services.AddSingleton(relewiseConfiguration);

        builder.Services.AddSingleton<IContentMapper, RelewiseContentMapper>();
        builder.Services.AddSingleton<IExportContentService, ExportContentService>();
        builder.Services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();

        builder.Services.AddSingleton<IRelewisePropertyValueConverter, TextAreaPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, TextFieldPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, CheckboxPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, IntegerPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, DecimalPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, DateTimePropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, TagsPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, RichTextEditorPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, NestedContentPropertyValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, MediaPickerValueConverter>();
        builder.Services.AddSingleton<IRelewisePropertyValueConverter, ImageCropperValueConverter>();

        builder.AddNotificationHandler<ContentPublishedNotification, RelewiseContentPublishedNotificationHandler>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, RelewiseContentUnpublishedNotificationHandler>();
        builder.AddNotificationHandler<ContentDeletedNotification, RelewiseContentDeletedNotificationNotificationHandler>();
        builder.AddNotificationHandler<ContentMovedNotification, RelewiseContentMovedNotificationHandler>();

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