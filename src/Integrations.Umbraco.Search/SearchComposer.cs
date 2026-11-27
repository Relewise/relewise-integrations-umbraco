using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Configuration;
using Umbraco.Cms.Core.Search.Indexing;
#endif
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Configuration;
using Umbraco.Cms.Search.Core.DependencyInjection;
using Umbraco.Cms.Search.Core.Models.Configuration;
using Umbraco.Cms.Search.Core.Services;
using Umbraco.Cms.Search.Core.Services.ContentIndexing;
#endif
#if !UMBRACO_SEARCH_ADDON
using SearchConstants = Umbraco.Cms.Core.Constants.Search;
#endif
#if UMBRACO_SEARCH_ADDON
using SearchConstants = Umbraco.Cms.Search.Core.Constants;
#endif

namespace Relewise.Integrations.Umbraco.Search;

// Installation registers candidates; ConfigureSearch alone selects ingestion and optional query providers.
internal sealed class SearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
#if UMBRACO_SEARCH_ADDON
        builder.AddSearchCore();
#endif
        ConfigureIntegration(builder);
    }

    public static void ConfigureIntegration(IUmbracoBuilder builder)
    {
        RegisterServices(builder.Services);
        builder.Services.AddOptions<IndexOptions>()
            .PostConfigure<IServiceProvider>((indexes, services) =>
                ConfigureIndexes(indexes, services.GetService<RelewiseUmbracoConfiguration>()));
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // ConfigureSearch runs during configuration resolution, after DI registration.
        // Defer integration-only services so installing Search without AddRelewise remains valid.
        services.TryAddSingleton(provider => new SearchExportOperations(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<RelewiseUmbracoConfiguration>(),
            provider.GetRequiredService<ILogger<SearchExportOperations>>(),
            clock: provider.GetService<TimeProvider>()));
        services.TryAddScoped<UmbracoSearchExportSource>();
        services.TryAddSingleton<UmbracoSearchExportSystem>();
        services.TryAddSingleton(provider => new IndexMetadataService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<RelewiseUmbracoConfiguration>(),
            provider.GetRequiredService<IHostApplicationLifetime>(),
            provider.GetRequiredService<ILogger<IndexMetadataService>>(),
            clock: provider.GetService<TimeProvider>()));

        services.TryAddTransient(provider => new MappedEntityIndexer(
            provider.GetRequiredService<IExportSystem>(),
            provider.GetRequiredService<IndexMetadataService>()));
        services.TryAddTransient(provider => new MappedEntityChangeStrategy(
            provider.GetRequiredService<IExportSystem>()));
        services.TryAddTransient(provider => new MappedEntitySearcher(
            provider.GetRequiredService<RelewiseUmbracoConfiguration>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpContextAccessor>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDashboardConfigurationContributor, SearchDashboardContributor>());
    }

    private static void ConfigureIndexes(IndexOptions indexes, RelewiseUmbracoConfiguration? configuration)
    {
        if (configuration == null || !configuration.UsesUmbracoSearch())
            return;

        indexes.RegisterContentIndex<MappedEntityIndexer, MappedEntitySearcher, MappedEntityChangeStrategy>(
            SearchExportOperations.IndexAlias,
            sameOriginOnly: true,
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
            ContentIndexRegistration? original =
                indexes.GetContentIndexRegistration(
                    SearchConstants.IndexAliases
                        .PublishedContent);
            if (original == null)
            {
                // A query-only alias leaves all automatic ingestion on the mapped-entities index.
                indexes.RegisterIndex<MappedEntityIndexer, MappedEntitySearcher>(
                    SearchConstants.IndexAliases.PublishedContent);
                return;
            }

            // Umbraco's registration API is generic; the existing indexer/strategy types are known only at runtime.
            // Retain them and replace only query routing.
            typeof(SearchComposer).GetMethod(
                    nameof(UsePublishedSearcher),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(original.Indexer, original.ContentChangeStrategy)
                .Invoke(null, [indexes, original]);
        }
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
            original.ContainedObjectTypes.ToArray());
    }
}
