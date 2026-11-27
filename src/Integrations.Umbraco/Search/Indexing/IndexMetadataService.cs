using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.Shared;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Search.Indexing;

internal sealed class IndexMetadataService(
    IServiceScopeFactory scopes,
    RelewiseUmbracoConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<IndexMetadataService> logger,
    TimeProvider? clock = null)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _sync = new();
    private readonly Dictionary<string, CachedMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        if (!string.Equals(indexAlias, SearchExportOperations.IndexAlias, StringComparison.OrdinalIgnoreCase) &&
            !configuration.GetSearchOptions().Providers.ContainsKey(indexAlias))
        {
            return Task.FromResult(Unavailable());
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(indexAlias, out CachedMetadata? cached) &&
                (!cached.Result.IsCompleted || _clock.GetUtcNow() < cached.Expires))
            {
                return cached.Result;
            }

            // Share in-flight reads and cache failures too, so dashboard polling cannot flood the API.
            Task<IndexMetadata> result = ReadAsync(indexAlias);
            _cache[indexAlias] = new CachedMetadata(result, _clock.GetUtcNow() + CacheDuration);

            return result;
        }
    }

    private async Task<IndexMetadata> ReadAsync(string indexAlias)
    {
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
            cancellation.CancelAfter(RequestTimeout);

            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;
            var clients = services.GetRequiredService<IRelewiseClientFactory>();
            var reader = clients.GetClient<IDataAccessor>(Constants.ClientNames.Integration);

            configuration.GetSearchOptions().Providers.TryGetValue(indexAlias, out SearchProviderDefinition? provider);
            MappingDefinition[] mappings =
            [
                .. configuration.Mappings.Where(mapping =>
                    mapping.Export && mapping.EntityType != typeof(ProductVariant) &&
                    (provider == null || mapping.EntityType == provider.EntityType) &&
                    (provider?.ContentOptions?.DocumentTypeAliases == null ||
                     provider.ContentOptions.DocumentTypeAliases.Contains(
                         mapping.DocumentTypeAlias,
                         StringComparer.OrdinalIgnoreCase)))
            ];

            if (mappings.Length == 0)
            {
                return Unavailable();
            }

            long count = 0;

            foreach (IGrouping<Type, MappingDefinition> group in mappings.GroupBy(mapping => mapping.EntityType))
            {
                var filters = new FilterCollection(
                    new OrFilter(
                    [
                        .. group.Select(mapping => EntityExportDelivery.DataFilter(
                            group.Key,
                            RelewiseDataKeys.DocumentTypeAlias,
                            new EqualsCondition(mapping.DocumentTypeAlias)))
                    ]));

                cancellation.Token.ThrowIfCancellationRequested();
                count += await CountAsync(reader, group.Key, filters, cancellation.Token)
                         ?? throw new InvalidOperationException("Relewise did not return the requested entity total.");
            }

            // A successful read establishes accessibility, not search readiness or export freshness.
            return new IndexMetadata(count, HealthStatus.Healthy, "Relewise");
        }
        catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return Unavailable();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read Relewise index metadata for {IndexAlias}.", indexAlias);

            return Unavailable();
        }
    }

    private static async Task<int?> CountAsync(
        IDataAccessor reader,
        Type entityType,
        FilterCollection filters,
        CancellationToken token)
    {
        if (entityType == typeof(Product))
        {
            // The supported Product query constructor requires a positive page size.
            var query = new ProductQuery(filters, excludeProductsWithNoVariants: false, pageSize: 1)
            {
                ReturnTotalNumberOfResults = true,
                IncludeDisabledProducts = true,
                ResultSettings = new ProductQuery.SelectedPropertiesSettings
                {
                    SelectedProductDetailsProperties = new SelectedProductDetailsPropertiesSettings(),
                    SelectedVariantDetailsProperties = new SelectedVariantDetailsPropertiesSettings()
                }
            };

            return (await reader.QueryAsync(query, token)).TotalNumberOfResults;
        }

        if (entityType == typeof(Content))
        {
            var query = new ContentQuery(filters, 0, returnTotalNumberOfResults: true)
            { IncludeDisabledContents = true };

            return (await reader.QueryAsync(query, token)).TotalNumberOfResults;
        }

        if (entityType == typeof(Brand))
        {
            var query = new BrandQuery(filters, 0, returnTotalNumberOfResults: true)
            { IncludeDisabledBrands = true };

            return (await reader.QueryAsync(query, token)).TotalNumberOfResults;
        }

        if (entityType == typeof(ProductCategory))
        {
            var query = new ProductCategoryQuery(filters, 0, returnTotalNumberOfResults: true)
            { IncludeDisabledCategories = true };

            return (await reader.QueryAsync(query, token)).TotalNumberOfResults;
        }

        var categoryQuery = new ContentCategoryQuery(filters, 0, returnTotalNumberOfResults: true)
        { IncludeDisabledCategories = true };

        return (await reader.QueryAsync(categoryQuery, token)).TotalNumberOfResults;
    }

    private static IndexMetadata Unavailable()
    {
        return new IndexMetadata(0, HealthStatus.Unknown, "Relewise");
    }

    private sealed record CachedMetadata(Task<IndexMetadata> Result, DateTimeOffset Expires);
}
