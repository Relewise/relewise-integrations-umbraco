using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Relewise.Client;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Dashboards;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;
using Constants = Umbraco.Cms.Core.Constants;

namespace Relewise.Integrations.Umbraco;

public static class UmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddRelewise(this IUmbracoBuilder builder, Action<RelewiseConfigurationBuilder> options)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));
        if (options == null) throw new ArgumentNullException(nameof(options));

        IConfigurationSection relewiseSection = builder.Config.GetSection("Relewise");

        var datasetId = Guid.Parse(relewiseSection["DatasetId"]);
        var apiKey = relewiseSection["ApiKey"];

        var config = new RelewiseConfigurationBuilder();
        options.Invoke(config);

        builder.Services.AddSingleton(config.MappingConfiguration);

        var relewiseConfiguration = new RelewiseConfiguration(
            datasetId,
            apiKey,
            config.MappingConfiguration.AutoMappedDocTypes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>());
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

        builder.Services.AddSingleton<ITracker>(new Tracker(relewiseConfiguration.DatasetId, relewiseConfiguration.ApiKey));
        builder.Services.AddSingleton<IRecommender>(new Recommender(relewiseConfiguration.DatasetId, relewiseConfiguration.ApiKey));

        builder.AddNotificationHandler<ContentPublishedNotification, RelewiseContentPublishedNotificationHandler>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, RelewiseContentUnpublishedNotificationHandler>();
        builder.AddNotificationHandler<ContentDeletedNotification, RelewiseContentDeletedNotificationNotificationHandler>();
        builder.AddNotificationHandler<ContentMovedNotification, RelewiseContentMovedNotificationHandler>();

        builder.Dashboards().Add<RelewiseDashboard>();

        builder.Services.Configure<UmbracoPipelineOptions>(umbPipOptions =>
        {
            umbPipOptions.AddFilter(new UmbracoPipelineFilter(nameof(RelewiseDashboardApiController))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    GlobalSettings globalSettings = app.ApplicationServices.GetRequiredService<IOptions<GlobalSettings>>().Value;
                    IHostingEnvironment hostingEnvironment = app.ApplicationServices.GetRequiredService<IHostingEnvironment>();
                    string backOfficeArea = Constants.Web.Mvc.BackOfficePathSegment;

                    var rootSegment = $"{globalSettings.GetUmbracoMvcArea(hostingEnvironment)}/{backOfficeArea}";
                    endpoints.MapUmbracoRoute<RelewiseDashboardApiController>(
                        rootSegment: rootSegment,
                        areaName: "Relewise",
                        prefixPathSegment: "Relewise");
                })
            });
        });

        return builder;
    }
}

public class RelewiseConfigurationBuilder
{
    internal RelewiseMappingConfiguration MappingConfiguration { get; } = new();

    public RelewiseConfigurationBuilder UseMapping(Action<RelewiseMappingConfiguration> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        options.Invoke(MappingConfiguration);

        return this;
    }
}

public class RelewiseMappingConfiguration
{
    internal HashSet<string>? AutoMappedDocTypes { get; private set; }

    public RelewiseMappingConfiguration AutoMapping(params string[] docTypes)
    {
        if (docTypes == null) throw new ArgumentNullException(nameof(docTypes));

        AutoMappedDocTypes = docTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return this;
    }
}