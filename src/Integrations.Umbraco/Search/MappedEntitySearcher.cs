using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.DataTypes.Search.Sorting.Product;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Requests.Shared;
using Relewise.Client.Responses.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using Umbraco.Cms.Core.Services;
using static Relewise.Integrations.Umbraco.Search.CoreSearchTranslation;
using Language = Relewise.Client.DataTypes.Language;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class MappedEntitySearcher(RelewiseUmbracoConfiguration configuration, IServiceScopeFactory scopes,
    IHttpContextAccessor http) : global::Umbraco.Cms.Core.Search.ISearcher
{
    public async Task<SearchResult> SearchAsync(string indexAlias, string? query = null, IEnumerable<UFilter>? filters = null,
        IEnumerable<UFacet>? facets = null, IEnumerable<Sorter>? sorters = null, string? culture = null, string? segment = null,
        AccessContext? accessContext = null, int skip = 0, int take = 10, int maxSuggestions = 0)
    {
        query = string.IsNullOrWhiteSpace(query) ? null : query;
        culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        segment = string.IsNullOrWhiteSpace(segment) ? null : segment;
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSuggestions);

        string alias = indexAlias == global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent
            ? configuration.SearchProviders.DefaultPublishedContentProviderAlias ?? indexAlias : indexAlias;
        if (!configuration.SearchProviders.Providers.TryGetValue(alias, out var provider))
        {
            throw new InvalidOperationException($"No Relewise query provider is configured for '{alias}'.");
        }

        if (segment != null)
        {
            UnsupportedSegmentBehavior behavior = provider.Options is ContentSearchProviderOptions content ? content.UnsupportedSegmentBehavior : ((ProductSearchProviderOptions)provider.Options).UnsupportedSegmentBehavior;
            if (!behavior.IgnoreSegment)
            {
                throw new NotSupportedException("Segment-specific core queries are not supported. Configure Ignore() to execute without a segment.");
            }

            segment = null;
        }
        bool requirePublishedPage = provider.Options is not ProductSearchProviderOptions { RequirePublishedPage: false };
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IServiceProvider services = http.HttpContext?.RequestServices ?? scope.ServiceProvider;
        User user = await services.GetRequiredService<IRelewiseUserLocator>().GetUser();
        string language = culture ?? (await services.GetRequiredService<ILanguageService>().GetDefaultIsoCodeAsync());
        var context = new SearchRequestContext(services, user, language);
        string site = EntityIndexingService.SiteId(services);
        UFilter[] supplied = filters?.ToArray() ?? [];
        UFacet[] selectedFacets = facets?.ToArray() ?? [];
        Sorter[] selectedSorters = sorters?.ToArray() ?? [];
        IReadOnlyDictionary<string, string> booleanDataFields = provider.Options is ProductSearchProviderOptions productOptions
            ? productOptions.BooleanDataFields : new Dictionary<string, string>();
        foreach (var facet in selectedFacets)
        {
            if (booleanDataFields.ContainsKey(facet.FieldName) && facet is not KeywordFacet)
            {
                throw new NotSupportedException($"Boolean Data binding '{facet.FieldName}' requires a KeywordFacet.");
            }
        }
        foreach (var sorter in selectedSorters)
        {
            if (sorter.FieldName != null && booleanDataFields.ContainsKey(sorter.FieldName))
            {
                throw new NotSupportedException($"Sorting Boolean Data binding '{sorter.FieldName}' is not supported.");
            }
        }
        string[] sourceAliases = provider.ContentOptions?.DocumentTypeAliases ?? configuration.Mappings.Values
            .Where(mapping => mapping.Export && mapping.EntityType == provider.EntityType).Select(mapping => mapping.DocumentTypeAlias).ToArray();
        var client = services.GetRequiredService<IRelewiseClientFactory>().GetClient<global::Relewise.Client.Search.ISearcher>(Constants.NamedClientName);
        ContentSearchRequest Create(IEnumerable<UFilter> selected, int offset, int count)
        {
            var request = new ContentSearchRequest(new Language(language), Currency.Undefined, user, provider.Location(alias), query, offset, count)
            {
                Filters = new FilterCollection(),
                Facets = new ContentFacetQuery(),
                Sorting = new ContentSortBySpecification(),
                Settings = new ContentSearchSettings { SelectedContentProperties = new SelectedContentPropertiesSettings { DataKeys = ["rwUmbracoNodeKey"] } }
            };
            request.Filters!.Add(Equal("rwUmbracoSite", site));
            if (requirePublishedPage)
            {
                request.Filters.Add(Equal("rwUmbracoPublished", true));
            }

            request.Filters!.Add(new OrFilter(sourceAliases.Select(a => Equal("contentTypeAlias", a)).ToArray()));
            request.Filters!.Add(new OrFilter((culture == null ? new[] { "" } : new[] { "", culture.ToLowerInvariant() })
                .Select(value => (RFilter)new ContentDataFilter(CultureKey, new ContainsCondition(value))).ToArray()));
            request.Filters!.Add(new OrFilter((segment == null ? new[] { "" } : new[] { "", segment })
                .Select(value => (RFilter)new ContentDataFilter(SegmentsKey, new ContainsCondition(value))).ToArray()));
            if (accessContext?.Bypass != true)
            {
                var allowed = new List<RFilter> { Equal(ProtectedKey, false) };
                if (accessContext != null && accessContext.PrincipalId != Guid.Empty)
                {
                    allowed.AddRange((accessContext.GroupIds ?? []).Append(accessContext.PrincipalId)
                        .Select(id => new ContentDataFilter(AccessKey, new ContainsCondition(id.ToString("D")))));
                }

                request.Filters!.Add(new OrFilter(allowed.ToArray()));
            }
            foreach (UFilter filter in selected)
            {
                request.Filters!.Add(MapFilter(filter, culture, booleanDataFields));
            }

            return request;
        }
        SearchRequest? predictionSource = null;
        CancellationToken cancellation = http.HttpContext?.RequestAborted ?? CancellationToken.None;
        async Task<ContentSearchResponse> Execute(ContentSearchRequest source)
        {
            // A fresh typed SDK request is configured for every execution, including facet queries.
            int pagingSkip = source.Skip, pagingTake = source.Take;
            RFilter[] mandatory = source.Filters!.Items!.ToArray();
            if (provider.Options is ContentSearchProviderOptions content)
            {
                var outgoing = new ContentSearchRequest(source.Language, source.Currency, user, provider.Location(alias), query, pagingSkip, pagingTake)
                {
                    Filters = new FilterCollection(),
                    Facets = new ContentFacetQuery { Items = source.Facets?.Items?.ToList() ?? [] },
                    Sorting = source.Sorting,
                    Settings = new ContentSearchSettings { SelectedContentProperties = new SelectedContentPropertiesSettings { DataKeys = ["rwUmbracoNodeKey"] } }
                };
                content.Configure?.Invoke(outgoing, context);
                outgoing.Filters = new FilterCollection((outgoing.Filters?.Items ?? []).Concat(mandatory).Distinct().ToArray());
                outgoing.User = user;
                outgoing.Language = new Language(language);
                outgoing.IndexSelector = null;
                outgoing.Term = query;
                outgoing.Skip = pagingSkip;
                outgoing.Take = pagingTake;
                outgoing.Settings ??= new ContentSearchSettings();
                outgoing.Settings.SelectedContentProperties ??= new SelectedContentPropertiesSettings();
                outgoing.Settings.SelectedContentProperties.DataKeys = (outgoing.Settings.SelectedContentProperties.DataKeys ?? []).Append("rwUmbracoNodeKey").Distinct().ToArray();
                if (string.IsNullOrWhiteSpace(outgoing.DisplayedAtLocation))
                {
                    outgoing.DisplayedAtLocation = provider.Location(alias);
                }

                predictionSource ??= outgoing;
                return await client.SearchAsync(outgoing, cancellation);
            }
            var options = (ProductSearchProviderOptions)provider.Options;
            var target = new ProductSearchRequest(source.Language, source.Currency, user, provider.Location(alias), query, source.Skip, source.Take)
            {
                Filters = new FilterCollection(),
                Settings = new ProductSearchSettings
                {
                    SelectedProductProperties = new SelectedProductPropertiesSettings { DataKeys = ["rwUmbracoNodeKey"] },
                    VariantRequestSettings = new VariantSearchRequestSettings { MaxVariantsPerProduct = 0 }
                }
            };
            if (source.Sorting?.Value != null)
            {
                target.Sorting!.Value = ProductSort(source.Sorting.Value);
            }

            foreach (ContentDataStringValueFacet facet in source.Facets!.Items)
            {
                if (booleanDataFields.Values.Contains(facet.Key))
                {
                    target.Facets!.AddDataBoolean(DataSelectionStrategy.Product, facet.Key);
                }
                else
                {
                    target.Facets!.AddDataString(DataSelectionStrategy.Product, facet.Key);
                }
            }

            int originalSkip = source.Skip, originalTake = source.Take;
            options.Configure?.Invoke(target, context);
            target.Filters = new FilterCollection((target.Filters?.Items ?? []).Concat(mandatory.Select(ProductFilter)).ToArray());
            target.User = user;
            target.Language = new Language(language);
            target.IndexSelector = null;
            target.Term = query;
            target.Skip = originalSkip;
            target.Take = originalTake;
            target.Settings ??= new ProductSearchSettings();
            if (target.Settings.VariantRequestSettings?.MaxVariantsPerProduct > 1)
            {
                throw new NotSupportedException("Umbraco document search requires at most one result per Product.");
            }

            target.Settings.VariantRequestSettings ??= new VariantSearchRequestSettings();
            target.Settings.VariantRequestSettings.MaxVariantsPerProduct ??= 0;
            target.Settings.SelectedProductProperties ??= new SelectedProductPropertiesSettings();
            target.Settings.SelectedProductProperties.DataKeys = (target.Settings.SelectedProductProperties.DataKeys ?? []).Append("rwUmbracoNodeKey").Distinct().ToArray();
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
                Results = (response.Results ?? []).Select(result => new ContentResult { ContentId = result.ProductId, Data = result.Data }).ToArray(),
                Facets = new ContentFacetResult
                {
                    Items = (response.Facets?.Items ?? []).Select(f => f switch
                    {
                        ProductDataStringValueFacetResult text => (global::Relewise.Client.DataTypes.Search.Facets.Result.FacetResult)new ContentDataStringValueFacetResult(text.Key, text.Selected, text.CollectionFilterType, text.Available),
                        ProductDataBooleanValueFacetResult boolean => new ContentDataStringValueFacetResult(boolean.Key,
                            (boolean.Selected ?? []).Select(value => value ? "true" : "false").ToList(), null,
                            (boolean.Available ?? []).Select(value => new AvailableFacetValue<string>(value.Value ? "true" : "false", value.Selected, value.Hits)).ToList()),
                        _ => null
                    }).OfType<global::Relewise.Client.DataTypes.Search.Facets.Result.FacetResult>().ToList()
                }
            };
#pragma warning restore CS0618
        }
        Task<ContentSearchResponse> Run(ContentSearchRequest request) => Execute(request);
        ContentSearchRequest request = Create(supplied, skip, take);
        ApplySorting(request, selectedSorters, culture);
        ContentSearchResponse result = await Run(request);
        var facetResults = new List<UFacetResult>();
        foreach (UFacet facet in selectedFacets)
        {
            facetResults.Add(await ComputeFacet(Create(supplied.Where(f => f.FieldName != facet.FieldName), 0, 0), facet, Run, culture, booleanDataFields));
        }

        string[] suggestions = [];
        if (maxSuggestions > 0 && query != null && predictionSource != null)
        {
            var prediction = new SearchTermPredictionRequest(predictionSource.Language, predictionSource.Currency, user,
                predictionSource.DisplayedAtLocation!, query, maxSuggestions)
            {
                // Predictions retain the fully configured query scope, including protection and provider filters.
                Filters = new FilterCollection((predictionSource.Filters?.Items ?? [])
                    .Concat(predictionSource.PostFilters?.Items ?? []).ToArray()),
                RelevanceModifiers = predictionSource.RelevanceModifiers,
                ContextValues = predictionSource.ContextValues,
                Settings = new SearchTermPredictionSettings
                {
                    TargetEntityTypes = [provider.EntityType == typeof(Product) ? EntityType.Product : EntityType.Content]
                }
            };
            SearchTermPredictionResponse predicted = await client.PredictAsync(prediction, cancellation);
            suggestions = (predicted.Predictions ?? [])
                .Select(value => value.Term)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxSuggestions)
                .ToArray();
        }

        return new SearchResult(result.Hits, take == 0 ? [] : (result.Results ?? []).Select(r =>
            new Document(Guid.Parse(r.Data["rwUmbracoNodeKey"].ValueAs<string>()), UmbracoObjectTypes.Document)).ToArray(), facetResults, suggestions);
    }
    private static RFilter ProductFilter(RFilter filter) => filter switch
    {
        ContentDataFilter f => new ProductDataFilter(f.Key, f.ObjectPath!, f.Conditions, f.MustMatchAllConditions, f.FilterOutIfKeyIsNotFound, f.Language, f.Currency),
        AndFilter f => new AndFilter(f.Filters.Select(ProductFilter).ToArray(), f.Negated),
        OrFilter f => new OrFilter(f.Filters.Select(ProductFilter).ToArray(), f.Negated),
        _ => throw new NotSupportedException($"Cannot translate core predicate '{filter.GetType().Name}' to Product search.")
    };
    private static ProductSorting ProductSort(ContentSorting sorting)
    {
        ProductSorting target = sorting switch
        {
            ContentDataSorting s => new ProductDataSorting(s.Key, DataSelectionStrategy.Product, s.Order, s.Mode),
            ContentRelevanceSorting s => new ProductRelevanceSorting(s.Order),
            _ => throw new NotSupportedException($"Unsupported core sorting '{sorting.GetType().Name}'.")
        };
        if (sorting.ThenBy != null)
        {
            target.ThenBy = ProductSort(sorting.ThenBy);
        }

        return target;
    }
    private static async Task<UFacetResult> ComputeFacet(ContentSearchRequest request, UFacet facet, Func<ContentSearchRequest, Task<ContentSearchResponse>> execute, string? culture = null,
        IReadOnlyDictionary<string, string>? booleanDataFields = null)
    {
        var values = new List<FacetValue>();
        switch (facet)
        {
            case KeywordFacet:
            case IntegerExactFacet:
            case DecimalExactFacet:
            case DateTimeOffsetExactFacet:
                string kind = facet switch
                {
                    KeywordFacet => "keyword",
                    IntegerExactFacet => "integerExact",
                    DecimalExactFacet => "decimalExact",
                    _ => "dateExact"
                };
                string dataKey = booleanDataFields != null && booleanDataFields.TryGetValue(facet.FieldName, out string? directKey)
                    ? directKey : Field(facet.FieldName, kind, culture);
                request.Facets!.AddDataString(dataKey);
                var response = await execute(request);
                foreach (var bucket in response.Facets?.DataString(dataKey)?.Available ?? [])
                {
                    values.Add(facet switch
                    {
                        KeywordFacet => new KeywordFacetValue(bucket.Value, bucket.Hits),
                        IntegerExactFacet => new IntegerExactFacetValue(checked((int)ParseNumber(bucket.Value)), bucket.Hits),
                        DecimalExactFacet => new DecimalExactFacetValue((decimal)ParseNumber(bucket.Value), bucket.Hits),
                        _ => new DateTimeOffsetExactFacetValue(DateTimeOffset.FromUnixTimeMilliseconds(checked((long)ParseNumber(bucket.Value))), bucket.Hits)
                    });
                }
                break;
            case IntegerRangeFacet f:
                foreach (var range in f.Ranges)
                {
                    values.Add(new IntegerRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, execute, NumberRange(f.FieldName, "integer", range.MinValue, range.MaxValue, culture))));
                }

                break;
            case DecimalRangeFacet f:
                foreach (var range in f.Ranges)
                {
                    values.Add(new DecimalRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, execute, NumberRange(f.FieldName, "decimal", (double?)range.MinValue, (double?)range.MaxValue, culture))));
                }

                break;
            case DateTimeOffsetRangeFacet f:
                foreach (var range in f.Ranges)
                {
                    values.Add(new DateTimeOffsetRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, execute, NumberRange(f.FieldName, "date", range.MinValue.HasValue ? DateNumber(range.MinValue.Value) : null,
                        range.MaxValue.HasValue ? DateNumber(range.MaxValue.Value) : null, culture))));
                }

                break;
            default:
                throw new NotSupportedException($"The Relewise provider does not support facet '{facet.GetType().Name}'.");
        }
        return new UFacetResult(facet.FieldName, values);
    }

    private static async Task<long> CountRange(ContentSearchRequest request, Func<ContentSearchRequest, Task<ContentSearchResponse>> execute, RFilter filter)
    {
        request.Filters!.Items!.Add(filter);
        try
        {
            return (await execute(request)).Hits;
        }
        finally
        {
            request.Filters!.Items!.Remove(filter);
        }
    }

}
