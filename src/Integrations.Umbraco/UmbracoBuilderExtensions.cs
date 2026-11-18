#pragma warning disable CS0618 // Retain legacy converter registration order; the execution pipeline awaits async built-ins.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.NotificationHandlers;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Search.Configuration;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Models;

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
            .AddValueConverter<RichTextEditorPropertyValueConverter>()
            .AddValueConverter<CheckboxPropertyValueConverter>()
            .AddValueConverter<IntegerPropertyValueConverter>()
            .AddValueConverter<DecimalPropertyValueConverter>()
            .AddValueConverter<TagsPropertyValueConverter>()
            .AddValueConverter<NestedContentPropertyValueConverter>()
            .AddValueConverter<BlockListPropertyValueConverter>();

        builder.AddNotificationHandler<ContentDeletedNotification, DeletedEntityIndexingHandler>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton<EntityIndexingService>();
        builder.Services.TryAddSingleton<IEntityIndexingOperations>(services => services.GetRequiredService<EntityIndexingService>());
        builder.Services.TryAddSingleton<EntityIndexingQueue>();
        builder.Services.TryAddTransient<MappedEntityIndexer>();
        builder.Services.TryAddTransient<MappedEntityChangeStrategy>();
        builder.Services.TryAddTransient<MappedEntitySearcher>();
        builder.Services.AddOptions<IndexOptions>().PostConfigure<RelewiseUmbracoConfiguration>((indexes, configuration) =>
        {
            indexes.RegisterContentIndex<MappedEntityIndexer, MappedEntitySearcher, MappedEntityChangeStrategy>(
                EntityIndexingService.IndexAlias, true, UmbracoObjectTypes.Document);
            foreach (string alias in configuration.SearchProviders.Providers.Keys)
            {
                if (indexes.GetIndexRegistration(alias) != null)
                    throw new InvalidOperationException($"Search provider alias '{alias}' is already registered by Umbraco.");
                indexes.RegisterIndex<MappedEntityIndexer, MappedEntitySearcher>(alias);
            }
            if (configuration.SearchProviders.DefaultPublishedContentProviderAlias != null)
            {
                var original = indexes.GetContentIndexRegistration(global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent)
                    ?? throw new InvalidOperationException("Umbraco's published-content search registration is required.");
                // Preserve Examine's indexer and change strategy; only route published queries to Relewise.
                typeof(UmbracoBuilderExtensions).GetMethod(nameof(UsePublishedSearcher), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(original.Indexer, original.ContentChangeStrategy).Invoke(null, [indexes, original]);
            }
        });

        return builder;
    }
    private static void UsePublishedSearcher<TIndexer, TStrategy>(IndexOptions indexes, ContentIndexRegistration original)
        where TIndexer : class, global::Umbraco.Cms.Core.Search.IIndexer
        where TStrategy : class, IContentChangeStrategy
        => indexes.RegisterContentIndex<TIndexer, MappedEntitySearcher, TStrategy>(original.IndexAlias, original.SameOriginOnly,
            System.Linq.Enumerable.ToArray(original.ContainedObjectTypes));
}
