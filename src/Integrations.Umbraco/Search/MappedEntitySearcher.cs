using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses.Search;
using Relewise.Client.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using Umbraco.Cms.Core.Services;
using static Relewise.Integrations.Umbraco.Search.CoreSearchTranslation;
using Content = Relewise.Client.DataTypes.Content;
using Language = Relewise.Client.DataTypes.Language;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using UmbracoSearcher = Umbraco.Cms.Core.Search.ISearcher;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class MappedEntitySearcher(
    RelewiseUmbracoConfiguration configuration,
    IServiceScopeFactory scopes,
    IHttpContextAccessor http) : UmbracoSearcher
{
    public async Task<SearchResult> SearchAsync(
        string indexAlias,
        string? query = null,
        IEnumerable<UFilter>? filters = null,
        IEnumerable<UFacet>? facets = null,
        IEnumerable<Sorter>? sorters = null,
        string? culture = null,
        string? segment = null,
        AccessContext? accessContext = null,
        int skip = 0,
        int take = 10,
        int maxSuggestions = 0)
    {
        query = string.IsNullOrWhiteSpace(query) ? null : query;
        culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        segment = string.IsNullOrWhiteSpace(segment) ? null : segment;
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSuggestions);

        string alias = indexAlias == UmbracoConstants.Search.IndexAliases.PublishedContent
            ? configuration.GetSearchOptions().DefaultPublishedContentProviderAlias ?? indexAlias
            : indexAlias;

        if (!configuration.GetSearchOptions().Providers.TryGetValue(alias, out SearchProviderDefinition? provider))
        {
            throw new InvalidOperationException($"No Relewise query provider is configured for '{alias}'.");
        }

        if (segment != null)
        {
            UnsupportedSegmentBehavior behavior = provider.Options is ContentSearchProviderOptions content
                ? content.UnsupportedSegmentBehavior
                : ((ProductSearchProviderOptions)provider.Options).UnsupportedSegmentBehavior;

            if (!behavior.IgnoreSegment)
            {
                throw new NotSupportedException(
                    "Segment-specific core queries are not supported. " +
                    "Configure Ignore() to execute without a segment.");
            }
        }

        bool requirePublishedPage = provider.Options is not ProductSearchProviderOptions
        {
            RequirePublishedPage: false
        };

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IServiceProvider services = http.HttpContext?.RequestServices ?? scope.ServiceProvider;

        User user = await services.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();
        string language = culture ?? await services.GetRequiredService<ILanguageService>().GetDefaultIsoCodeAsync();
        var context = new SearchRequestContext(services, user, language);

        UFilter[] supplied = filters?.ToArray() ?? [];
        UFacet[] selectedFacets = facets?.ToArray() ?? [];
        Sorter[] selectedSorters = sorters?.ToArray() ?? [];

        SearchFieldDefinition ResolveField(string name)
        {
            return SearchFieldCatalog.Resolve(name, configuration, provider, services);
        }

        string[] queryFields =
        [
            .. supplied.Select(value => value.FieldName)
                .Concat(selectedFacets.Select(value => value.FieldName))
                .Concat(
                    selectedSorters.Where(value => value is not ScoreSorter)
                        .Select(value => value.FieldName))
                .Distinct()
        ];
        string[] priceFields = [.. queryFields.Where(name => ResolveField(name).Kind == SearchFieldKind.SalesPrice)];

        if (priceFields.Length > 1)
        {
            throw new NotSupportedException("Use one query field for SalesPrice within a search request.");
        }

        string? priceField = priceFields.SingleOrDefault();
        ProductPriceQuery? prices = priceField == null
            ? null
            : new ProductPriceQuery(priceField, supplied, selectedFacets, selectedSorters);
        UFacet[] dataFacets = [.. selectedFacets.Where(facet => prices?.Matches(facet.FieldName) != true)];
        UFilter[] dataFilters = [.. supplied.Where(filter => prices?.Matches(filter.FieldName) != true)];

        string[] sourceAliases = provider.ContentOptions?.DocumentTypeAliases ??
        [
            .. configuration.Mappings
                .Where(mapping => mapping.Export && mapping.EntityType == provider.EntityType)
                .Select(mapping => mapping.DocumentTypeAlias)
        ];
        var client = services.GetRequiredService<IRelewiseClientFactory>()
            .GetClient<ISearcher>(Constants.ClientNames.Integration);

        ContentSearchRequest Create(
            IEnumerable<UFilter> selected,
            int offset,
            int count)
        {
            var request = new ContentSearchRequest(
                new Language(language),
                Currency.Undefined,
                user,
                provider.Location(alias),
                query,
                offset,
                count)
            {
                Filters = new FilterCollection(),
                Facets = new ContentFacetQuery(),
                Sorting = new ContentSortBySpecification(),
                Settings = new ContentSearchSettings
                {
                    SelectedContentProperties = new SelectedContentPropertiesSettings
                    { DataKeys = [RelewiseDataKeys.NodeKey] }
                }
            };

            if (requirePublishedPage)
            {
                request.Filters.Add(Equal(RelewiseDataKeys.Published, true));
            }

            request.Filters!.Add(
                new OrFilter([.. sourceAliases.Select(a => (RFilter)Equal(RelewiseDataKeys.DocumentTypeAlias, a))]));
            request.Filters!.Add(
                new OrFilter(
                [
                    .. (culture == null ? new[] { "" } : ["", culture.ToLowerInvariant()]).Select(value =>
                        (RFilter)new ContentDataFilter(RelewiseDataKeys.Search.Culture, new ContainsCondition(value)))
                ]));

            if (accessContext?.Bypass != true)
            {
                var allowed = new List<RFilter> { Equal(RelewiseDataKeys.Search.Protected, false) };

                if (accessContext != null && accessContext.PrincipalId != Guid.Empty)
                {
                    allowed.AddRange(
                        (accessContext.GroupIds ?? []).Append(accessContext.PrincipalId)
                        .Select(id => new ContentDataFilter(
                            RelewiseDataKeys.Search.Access,
                            new ContainsCondition(id.ToString("D")))));
                }

                request.Filters!.Add(new OrFilter([.. allowed]));
            }

            foreach (UFilter filter in selected)
            {
                request.Filters!.Add(
                    MapFilter(
                        filter,
                        ResolveField(filter.FieldName),
                        provider.EntityType == typeof(Product),
                        culture));
            }

            return request;
        }

        SearchRequest? predictionSource = null;
        CancellationToken cancellation = http.HttpContext?.RequestAborted ?? CancellationToken.None;

        async Task<ContentSearchResponse> Execute(ContentSearchRequest source)
        {
            // Configure one request containing both results and facets.
            int pagingSkip = source.Skip, pagingTake = source.Take;
            RFilter[] mandatory = [.. source.Filters!.Items!];

            if (provider.Options is ContentSearchProviderOptions content)
            {
                var outgoing = new ContentSearchRequest(
                    source.Language,
                    source.Currency,
                    user,
                    provider.Location(alias),
                    query,
                    pagingSkip,
                    pagingTake)
                {
                    Filters = new FilterCollection(),
                    Facets = new ContentFacetQuery(),
                    Sorting = source.Sorting,
                    Settings = new ContentSearchSettings
                    {
                        SelectedContentProperties = new SelectedContentPropertiesSettings
                        { DataKeys = [RelewiseDataKeys.NodeKey] }
                    }
                };

                content.Configure?.Invoke(outgoing, context);
                outgoing.Facets ??= new ContentFacetQuery();
                outgoing.Facets.Items = CoreSearchFacets.Merge(source.Facets!.Items, outgoing.Facets.Items);
                outgoing.Filters =
                    new FilterCollection([.. (outgoing.Filters?.Items ?? []).Concat(mandatory).Distinct()]);
                outgoing.User = user;
                outgoing.Language = new Language(language);
                outgoing.IndexSelector = null;
                outgoing.Term = query;
                outgoing.Skip = pagingSkip;
                outgoing.Take = pagingTake;
                outgoing.Settings ??= new ContentSearchSettings();
                outgoing.Settings.SelectedContentProperties ??= new SelectedContentPropertiesSettings();
                outgoing.Settings.SelectedContentProperties.DataKeys =
                [
                    .. (outgoing.Settings.SelectedContentProperties.DataKeys ?? []).Append(RelewiseDataKeys.NodeKey)
                    .Distinct()
                ];

                if (string.IsNullOrWhiteSpace(outgoing.DisplayedAtLocation))
                {
                    outgoing.DisplayedAtLocation = provider.Location(alias);
                }

                predictionSource ??= outgoing;

                return await client.SearchAsync(outgoing, cancellation);
            }

            var options = (ProductSearchProviderOptions)provider.Options;
            var target = new ProductSearchRequest(
                source.Language,
                source.Currency,
                user,
                provider.Location(alias),
                query,
                source.Skip,
                source.Take)
            {
                Filters = new FilterCollection(),
                Settings = new ProductSearchSettings
                {
                    SelectedProductProperties = new SelectedProductPropertiesSettings
                    { DataKeys = [RelewiseDataKeys.NodeKey] },
                    VariantRequestSettings = new VariantSearchRequestSettings { MaxVariantsPerProduct = 0 }
                }
            };

            ApplySorting(target, selectedSorters, ResolveField);

            options.Configure?.Invoke(target, context);
            target.Facets ??= new ProductFacetQuery();
            target.Facets.Items = CoreSearchFacets.Merge(
                source.Facets!.Items.Select(CoreSearchFacets.ForProduct),
                target.Facets.Items);
            target.Filters =
                new FilterCollection([.. target.Filters?.Items ?? [], .. mandatory.Select(ProductFilter)]);
            prices?.Apply(target);
            target.User = user;
            target.Language = new Language(language);
            target.IndexSelector = null;
            target.Term = query;
            target.Skip = pagingSkip;
            target.Take = pagingTake;
            target.Settings ??= new ProductSearchSettings();

            if (target.Settings.VariantRequestSettings?.MaxVariantsPerProduct > 1)
            {
                throw new NotSupportedException("Umbraco document search requires at most one result per Product.");
            }

            target.Settings.VariantRequestSettings ??= new VariantSearchRequestSettings();
            target.Settings.VariantRequestSettings.MaxVariantsPerProduct ??= 0;
            target.Settings.SelectedProductProperties ??= new SelectedProductPropertiesSettings();
            target.Settings.SelectedProductProperties.DataKeys =
            [
                .. (target.Settings.SelectedProductProperties.DataKeys ?? []).Append(
                    RelewiseDataKeys.NodeKey)
                .Distinct()
            ];

            if (string.IsNullOrWhiteSpace(target.DisplayedAtLocation))
            {
                target.DisplayedAtLocation = provider.Location(alias);
            }

            predictionSource ??= target;

            ProductSearchResponse response = await client.SearchAsync(target, cancellation);

#pragma warning disable CS0618 // Construct an internal adapter response, not an SDK request.
            return new ContentSearchResponse
            {
                Hits = response.Hits,
                Results =
                [
                    .. (response.Results ?? []).Select(result => new ContentResult
                        { ContentId = result.ProductId, Data = result.Data })
                ],
                Facets = new ContentFacetResult
                {
                    Items = [.. (response.Facets?.Items ?? []).Select(CoreSearchFacets.ForContent)]
                }
            };
#pragma warning restore CS0618
        }

        var facetQuery = new ContentFacetQuery();
        UFilter[] remainingFilters = CoreSearchFacets.Configure(
            facetQuery,
            dataFacets,
            dataFilters,
            ResolveField);
        ContentSearchRequest request = Create(remainingFilters, skip, take);
        request.Facets = facetQuery;

        if (provider.EntityType == typeof(Content))
        {
            ApplySorting(request, selectedSorters, ResolveField);
        }

        ContentSearchResponse result = await Execute(request);
        var dataResults = new Queue<UFacetResult>(
            CoreSearchFacets.Read(
                result.Facets,
                dataFacets,
                ResolveField));
        UFacetResult[] facetResults =
        [
            .. selectedFacets.Select(facet =>
                prices?.Matches(facet.FieldName) == true ? prices.Read(result.Facets) : dataResults.Dequeue())
        ];

        string[] suggestions = [];

        if (maxSuggestions > 0 && query != null && predictionSource != null)
        {
            RFilter PredictionFilter(UFilter filter)
            {
                if (prices?.Matches(filter.FieldName) == true)
                {
                    return prices.MapFilter(filter, predictionSource.Currency);
                }

                bool product = provider.EntityType == typeof(Product);
                RFilter mapped = MapFilter(filter, ResolveField(filter.FieldName), product, culture);
                return product ? ProductFilter(mapped) : mapped;
            }

            var prediction = new SearchTermPredictionRequest(
                predictionSource.Language,
                predictionSource.Currency,
                user,
                predictionSource.DisplayedAtLocation!,
                query,
                maxSuggestions)
            {
                // Predictions retain the fully configured query scope, including protection and provider filters.
                Filters = new FilterCollection(
                [
                    .. predictionSource.Filters?.Items ?? [], .. predictionSource.PostFilters?.Items ?? [],
                    .. supplied.Select(PredictionFilter)
                ]),
                RelevanceModifiers = predictionSource.RelevanceModifiers,
                ContextValues = predictionSource.ContextValues,
                Settings = new SearchTermPredictionSettings
                {
                    TargetEntityTypes =
                        [provider.EntityType == typeof(Product) ? EntityType.Product : EntityType.Content]
                }
            };
            SearchTermPredictionResponse predicted = await client.PredictAsync(prediction, cancellation);

            suggestions =
            [
                .. (predicted.Predictions ?? [])
                .Select(value => value.Term)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxSuggestions)
            ];
        }

        return new SearchResult(
            result.Hits,
            take == 0
                ? []
                : (result.Results ?? []).Select(r =>
                    new Document(
                        Guid.Parse(r.Data[RelewiseDataKeys.NodeKey].ValueAs<string>()),
                        UmbracoObjectTypes.Document))
                .ToArray(),
            facetResults,
            suggestions);
    }

    private static RFilter ProductFilter(RFilter filter)
    {
        return filter switch
        {
            ContentDataFilter f => new ProductDataFilter(
                f.Key,
                f.ObjectPath!,
                f.Conditions,
                f.MustMatchAllConditions,
                f.FilterOutIfKeyIsNotFound,
                f.Language,
                f.Currency),
            AndFilter f => new AndFilter([.. f.Filters.Select(ProductFilter)], f.Negated),
            OrFilter f => new OrFilter([.. f.Filters.Select(ProductFilter)], f.Negated),
            ProductIdFilter or BrandIdFilter or ProductCategoryIdFilter => filter,
            _ => throw new NotSupportedException(
                $"Cannot translate core predicate '{filter.GetType().Name}' to Product search.")
        };
    }
}
