using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Extensions methods for setting up Relewise in a <see cref="IServiceCollection"/>.
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
            .AddValueConverter<NestedContentPropertyValueConverter>()
            .AddValueConverter<BlockListPropertyValueConverter>();

        builder.AddNotificationAsyncHandler<ContentPublishedNotification, RelewiseContentPublishedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentUnpublishedNotification, RelewiseContentUnpublishedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, RelewiseContentDeletedNotificationNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedNotification, RelewiseContentMovedNotificationHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, RelewiseContentMovedToRecycleBinNotificationHandler>();

        return builder;
    }
}