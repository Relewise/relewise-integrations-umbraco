using Relewise.Integrations.Umbraco.Search.Indexing;
// Retain legacy converter registration order; the execution pipeline awaits async built-ins.
#pragma warning disable CS0618
using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Search;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Configuration;
using Umbraco.Cms.Core.Search.Indexing;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

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
    public static IUmbracoBuilder AddRelewise(
        this IUmbracoBuilder builder,
        Action<RelewiseUmbracoOptionsBuilder>? configure = null)
    {
        configure ??= _ => { };

        return builder.AddRelewise((optionsBuilder, _) => configure(optionsBuilder));
    }

    /// <summary>
    /// Registers services and configures <see cref="RelewiseUmbracoOptionsBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IUmbracoBuilder"/></param>
    /// <param name="configure">A delegate to configure <see cref="RelewiseUmbracoOptionsBuilder"/> which also includes
    /// the <see cref="IServiceProvider"/> if you need to do service lookups part of this configuration.</param>
    /// <returns>The <see cref="IUmbracoBuilder"/></returns>
    public static IUmbracoBuilder AddRelewise(
        this IUmbracoBuilder builder,
        Action<RelewiseUmbracoOptionsBuilder, IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddRelewise(x => x.Named.Add(
            Constants.ClientNames.Integration,
            clientOptions =>
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
            .AddLegacyValueConverter<TextFieldPropertyValueConverter>()
            .AddLegacyValueConverter<RichTextEditorPropertyValueConverter>()
            .AddLegacyValueConverter<CheckboxPropertyValueConverter>()
            .AddLegacyValueConverter<IntegerPropertyValueConverter>()
            .AddLegacyValueConverter<DecimalPropertyValueConverter>()
            .AddLegacyValueConverter<TagsPropertyValueConverter>()
            .AddLegacyValueConverter<NestedContentPropertyValueConverter>()
            .AddLegacyValueConverter<BlockListPropertyValueConverter>();

        builder.AddNotificationHandler<ContentDeletedNotification, DeletedEntityIndexingHandler>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton<SearchExportOperations>();
        builder.Services.TryAddScoped<UmbracoSearchExportSource>();
        builder.Services.TryAddSingleton<NotificationExportOperations>();
        builder.Services.TryAddSingleton<NotificationExportSystem>();
        builder.Services.TryAddSingleton<UmbracoSearchExportSystem>();
        builder.Services.TryAddSingleton<IExportSystem>(services =>
            services.GetRequiredService<RelewiseUmbracoConfiguration>().ExportSystemConfiguration?.Resolve(services)
            ?? services.GetRequiredService<NotificationExportSystem>());
        builder.AddNotificationHandler<ContentPublishedNotification, ContentExportNotificationHandler>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, ContentExportNotificationHandler>();
        builder.AddNotificationHandler<ContentMovedNotification, ContentExportNotificationHandler>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, ContentExportNotificationHandler>();
        builder.Services.TryAddSingleton<IndexMetadataService>();
        builder.Services.TryAddTransient<MappedEntityIndexer>();
        builder.Services.TryAddTransient<MappedEntityChangeStrategy>();
        builder.Services.TryAddTransient<MappedEntitySearcher>();
        builder.Services.AddOptions<IndexOptions>()
            .PostConfigure<RelewiseUmbracoConfiguration>((
                indexes,
                configuration) =>
            {
                if (!configuration.UsesUmbracoSearch())
                    return;

                indexes.RegisterContentIndex<MappedEntityIndexer, MappedEntitySearcher, MappedEntityChangeStrategy>(
                    SearchExportOperations.IndexAlias,
                    true,
                    UmbracoObjectTypes.Document);

                foreach (string alias in configuration.GetSearchOptions().Providers.Keys)
                {
                    if (indexes.GetIndexRegistration(alias) != null)
                    {
                        throw new InvalidOperationException(
                            $"Search provider alias '{alias}' is already registered by Umbraco.");
                    }

                    indexes.RegisterIndex<MappedEntityIndexer, MappedEntitySearcher>(alias);
                }

                if (configuration.GetSearchOptions().DefaultPublishedContentProviderAlias != null)
                {
                    ContentIndexRegistration original =
                        indexes.GetContentIndexRegistration(
                            UmbracoConstants.Search.IndexAliases
                                .PublishedContent)
                        ?? throw new InvalidOperationException(
                            "Umbraco's published-content search registration is required.");

                    // Preserve Examine's indexer and change strategy; only route published queries to Relewise.
                    typeof(UmbracoBuilderExtensions).GetMethod(
                            nameof(UsePublishedSearcher),
                            BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(original.Indexer, original.ContentChangeStrategy)
                        .Invoke(null, [indexes, original]);
                }
            });

        return builder;
    }

    private static void UsePublishedSearcher<TIndexer, TStrategy>(
        IndexOptions indexes,
        ContentIndexRegistration original)
        where TIndexer : class, IIndexer
        where TStrategy : class, IContentChangeStrategy
    {
        indexes.RegisterContentIndex<TIndexer, MappedEntitySearcher, TStrategy>(
            original.IndexAlias,
            original.SameOriginOnly,
            System.Linq.Enumerable.ToArray(original.ContainedObjectTypes));
    }
}
