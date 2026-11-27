using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Configuration;
using Umbraco.Cms.Core.Search.Indexing;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Relewise.Integrations.Umbraco.Search;

// Installation registers candidates; ConfigureSearch alone selects ingestion and optional query providers.
internal sealed class SearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // The package can be installed without AddRelewise; defer optional Core dependencies until activation.
        builder.Services.TryAddSingleton(services =>
            ActivatorUtilities.CreateInstance<SearchExportOperations>(services));
        builder.Services.TryAddScoped(services =>
            ActivatorUtilities.CreateInstance<UmbracoSearchExportSource>(services));
        builder.Services.TryAddSingleton(services =>
            ActivatorUtilities.CreateInstance<UmbracoSearchExportSystem>(services));
        builder.Services.TryAddSingleton<IDashboardConfigurationContributor, SearchDashboardContributor>();
        builder.Services.TryAddSingleton(services => ActivatorUtilities.CreateInstance<IndexMetadataService>(services));
        builder.Services.TryAddTransient(services => ActivatorUtilities.CreateInstance<MappedEntityIndexer>(services));
        builder.Services.TryAddTransient(services =>
            ActivatorUtilities.CreateInstance<MappedEntityChangeStrategy>(services));
        builder.Services.TryAddTransient(services => ActivatorUtilities.CreateInstance<MappedEntitySearcher>(services));
        builder.Services.AddOptions<IndexOptions>()
            .PostConfigure<IServiceProvider>((indexes, services) =>
            {
                RelewiseUmbracoConfiguration? configuration = services.GetService<RelewiseUmbracoConfiguration>();
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
                    ContentIndexRegistration original =
                        indexes.GetContentIndexRegistration(
                            UmbracoConstants.Search.IndexAliases
                                .PublishedContent)
                        ?? throw new InvalidOperationException(
                            "Umbraco's published-content search registration is required.");

                    // Preserve Examine's indexer and change strategy; only route published queries to Relewise.
                    typeof(SearchComposer).GetMethod(
                            nameof(UsePublishedSearcher),
                            BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(original.Indexer, original.ContentChangeStrategy)
                        .Invoke(null, [indexes, original]);
                }
            });
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
