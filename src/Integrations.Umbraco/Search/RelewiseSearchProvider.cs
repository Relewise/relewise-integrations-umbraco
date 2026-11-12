using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Options;
using Relewise.Client;
using Language = Relewise.Client.DataTypes.Language;
using Content = Relewise.Client.DataTypes.Content;
using Umbraco.Cms.Core;
using Relewise.Client.Extensions;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.DataTypes.Search.Sorting.Enums;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Filters.DataObjects;
using Relewise.Client.Requests.Filters.DataObjects.Conditions;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>Indexes and searches Umbraco's typed core search documents in Relewise.</summary>
public sealed class RelewiseSearchProvider : global::Umbraco.Cms.Core.Search.IIndexer, global::Umbraco.Cms.Core.Search.ISearcher
{
    internal const string OwnerKey = "rwUmbracoSearchOwner";
    internal const string IndexKey = "rwUmbracoSearchIndex";
    internal const string IdKey = "rwUmbracoSearchId";
    internal const string TypeKey = "rwUmbracoSearchType";
    internal const string CultureKey = "rwUmbracoSearchCulture";
    internal const string SegmentKey = "rwUmbracoSearchSegment";
    internal const string SegmentsKey = "rwUmbracoSearchSegments";
    internal const string AccessKey = "rwUmbracoSearchAccess";
    internal const string ProtectedKey = "rwUmbracoSearchProtected";
    internal const string GenerationKey = "rwUmbracoSearchGeneration";
    private readonly IRelewiseClientFactory _clients;
    private readonly RelewiseSearchOptions _options;
    private static readonly SemaphoreSlim[] WriteLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    /// <summary>Creates the provider using the configured named Relewise client.</summary>
    public RelewiseSearchProvider(IRelewiseClientFactory clients, IOptions<RelewiseSearchOptions> options)
    {
        _clients = clients;
        _options = options.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Language);
    }

    /// <inheritdoc />
    public async Task AddOrUpdateAsync(string indexAlias, Guid id, UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations, IEnumerable<IndexField> fields, ContentProtection? protection)
    {
        EnsureIndex(indexAlias);
        SemaphoreSlim gate = WriteLock(indexAlias);
        await gate.WaitAsync();
        try
        {
            string generation = Guid.NewGuid().ToString("N");
            ContentUpdate[] updates = MapDocuments(indexAlias, id, objectType, variations, fields, protection, generation).ToArray();
            ITracker tracker = _clients.GetClient<ITracker>(_options.ClientName);
            var batch = new List<Trackable>(updates);
            if (updates.Length > 0)
            {
                batch.Add(new ContentAdministrativeAction(Language.Undefined, Currency.Undefined,
                    new FilterCollection(new ContentIdFilter(updates.Select(x => x.Content.Id))), ContentAdministrativeAction.UpdateKind.Enable));
            }

            // Keep updates and cleanup in one ordered tracking batch, like a Relewise full import.
            // Separate requests may be processed independently by the remote ingestion pipeline.
            batch.Add(DeleteAction(indexAlias,
                Equal(IdKey, id.ToString("D")), new ContentDataFilter(GenerationKey, new EqualsCondition(generation, negated: true))));
            await tracker.TrackAsync(batch);
        }
        finally { gate.Release(); }
    }

    internal IEnumerable<ContentUpdate> MapDocuments(string indexAlias, Guid id, UmbracoObjectTypes objectType,
        IEnumerable<Variation> variations, IEnumerable<IndexField> fields, ContentProtection? protection, string generation)
    {
        IndexField[] allFields = fields.ToArray();
        foreach (var cultureGroup in variations.Distinct().GroupBy(v => v.Culture))
        {
            string[] segments = cultureGroup.Select(v => v.Segment ?? string.Empty).Distinct().ToArray();
            foreach (string segment in segments)
            {
                // Unsegmented fields are retained, as in the built-in provider. Culture invariant fields
                // accompany every culture. Each remote entity is a single searchable variation.
                IndexField[] selected = allFields.Where(f => (f.Culture is null || f.Culture == cultureGroup.Key)
                    && (f.Segment is null || f.Segment == segment)).ToArray();
                var data = new Dictionary<string, DataValue?>
                {
                    [OwnerKey] = _options.Scope, [IndexKey] = indexAlias, [IdKey] = id.ToString("D"),
                    [TypeKey] = objectType.ToString(), [CultureKey] = cultureGroup.Key ?? string.Empty,
                    [SegmentKey] = segment, [SegmentsKey] = new DataValue(segments),
                    [ProtectedKey] = protection is not null,
                    [AccessKey] = new DataValue(protection?.AccessIds.Select(x => x.ToString("D")) ?? []),
                    [GenerationKey] = generation
                };
                foreach (var group in selected.GroupBy(f => f.FieldName))
                {
                    IndexValue[] values = group.Select(f => f.Value).ToArray();
                    StoreStrings(data, group.Key, "keyword", values.SelectMany(v => v.Keywords ?? []));
                    StoreStrings(data, group.Key, "text", values.SelectMany(Texts));
                    StoreNumbers(data, group.Key, "integer", values.SelectMany(v => v.Integers ?? []).Select(v => (double)v));
                    StoreNumbers(data, group.Key, "decimal", values.SelectMany(v => v.Decimals ?? []).Select(v => (double)v));
                    StoreNumbers(data, group.Key, "date", values.SelectMany(v => v.DateTimeOffsets ?? []).Select(DateNumber));
                }
                string text = string.Join(" ", selected.SelectMany(f => Texts(f.Value)));
                var content = new Content($"umbraco-search:{Encode(_options.Scope)}:{Encode(indexAlias)}:{id:D}:{Encode(cultureGroup.Key ?? string.Empty)}:{Encode(segment)}")
                {
                    // The default Relewise index searches DisplayName. Keep searchable text independent
                    // of application-specific Dataset Data Key configuration.
                    DisplayName = new Multilingual(new Multilingual.Value(_options.Language, text)),
                    Data = data
                };
                yield return new ContentUpdate(content, ContentUpdate.UpdateKind.ClearAndReplace);
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string indexAlias, IEnumerable<Guid> ids)
    {
        EnsureIndex(indexAlias);
        Guid[] keys = ids.Distinct().ToArray();
        if (keys.Length == 0) return;
        SemaphoreSlim gate = WriteLock(indexAlias);
        await gate.WaitAsync();
        try { await _clients.GetClient<ITracker>(_options.ClientName)
            .TrackAsync(DeleteAction(indexAlias, new OrFilter(keys.Select(id => Equal(IdKey, id.ToString("D"))).ToArray()))); }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public async Task ResetAsync(string indexAlias)
    {
        EnsureIndex(indexAlias);
        SemaphoreSlim gate = WriteLock(indexAlias);
        await gate.WaitAsync();
        try { await _clients.GetClient<ITracker>(_options.ClientName).TrackAsync(DeleteAction(indexAlias)); }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public async Task<IndexMetadata> GetMetadataAsync(string indexAlias)
    {
        ContentSearchRequest request = CreateRequest(indexAlias, null, 0, 0);
        ContentSearchResponse response = await Execute(request);
        return new IndexMetadata(response.Hits, response.Hits == 0 ? HealthStatus.Empty : HealthStatus.Healthy, "Relewise");
    }

    /// <inheritdoc />
    public async Task<SearchResult> SearchAsync(string indexAlias, string? query = null, IEnumerable<UFilter>? filters = null,
        IEnumerable<UFacet>? facets = null, IEnumerable<Sorter>? sorters = null, string? culture = null, string? segment = null,
        AccessContext? accessContext = null, int skip = 0, int take = 10, int maxSuggestions = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSuggestions);
        UFilter[] allFilters = filters?.ToArray() ?? [];
        ContentSearchRequest request = QueryRequest(indexAlias, query, allFilters, culture, segment, accessContext, skip, take);
        ApplySorting(request, sorters ?? []);
        ContentSearchResponse response = await Execute(request);
        Document[] documents = take == 0 ? [] : (response.Results ?? []).Select(result =>
            new Document(Guid.Parse(result.Data[IdKey].ValueAs<string>()),
                Enum.Parse<UmbracoObjectTypes>(result.Data[TypeKey].ValueAs<string>()))).ToArray();
        var facetResults = new List<UFacetResult>();
        foreach (UFacet facet in facets ?? [])
        {
            // A facet's own filter is omitted, matching Umbraco's drill-sideways behavior.
            ContentSearchRequest facetRequest = QueryRequest(indexAlias, query,
                allFilters.Where(f => f.FieldName != facet.FieldName), culture, segment, accessContext, 0, 0);
            facetResults.Add(await ComputeFacet(facetRequest, facet));
        }
        // Suggestions are optional in the core contract; no unrelated product term predictions are returned.
        return new SearchResult(response.Hits, documents, facetResults, []);
    }

    internal ContentSearchRequest QueryRequest(string alias, string? query, IEnumerable<UFilter> filters,
        string? culture, string? segment, AccessContext? access, int skip, int take)
    {
        ContentSearchRequest request = CreateRequest(alias, query, skip, take);
        request.Filters!.Add(new OrFilter((string.IsNullOrEmpty(culture) ? new[] { string.Empty } : new[] { culture, string.Empty })
            .Select(c => Equal(CultureKey, c)).ToArray()));
        request.Filters!.Add(string.IsNullOrEmpty(segment) ? Equal(SegmentKey, string.Empty) : new OrFilter([
            Equal(SegmentKey, segment), new AndFilter([Equal(SegmentKey, string.Empty), Negate(Contains(SegmentsKey, segment))])
        ]));
        if (access?.Bypass != true)
        {
            var allowed = new List<RFilter> { Equal(ProtectedKey, false) };
            if (access is not null)
                allowed.AddRange((access.GroupIds ?? []).Append(access.PrincipalId).Select(id => Contains(AccessKey, id.ToString("D"))));
            request.Filters!.Add(new OrFilter(allowed.ToArray()));
        }
        foreach (UFilter filter in filters) request.Filters!.Add(MapFilter(filter));
        return request;
    }

    private ContentSearchRequest CreateRequest(string alias, string? query, int skip, int take)
    {
        EnsureIndex(alias);
        var request = new ContentSearchRequest(new Language(_options.Language), Currency.Undefined, User.Anonymous(),
            "Umbraco core search", query, skip, take)
        {
            Filters = new FilterCollection(),
            Facets = new Relewise.Client.DataTypes.Search.Facets.Queries.ContentFacetQuery(),
            Sorting = new ContentSortBySpecification(),
            Settings = new Relewise.Client.Requests.Search.Settings.ContentSearchSettings
            {
                SelectedContentProperties = new Relewise.Client.Requests.Shared.SelectedContentPropertiesSettings
                {
                    DataKeys = [IdKey, TypeKey]
                }
            }
        };
        request.Filters!.Add(Equal(OwnerKey, _options.Scope));
        request.Filters!.Add(Equal(IndexKey, alias));
        return request;
    }

    private Task<ContentSearchResponse> Execute(ContentSearchRequest request) =>
        _clients.GetClient<global::Relewise.Client.Search.ISearcher>(_options.ClientName).SearchAsync(request);

    private ContentAdministrativeAction DeleteAction(string alias, params RFilter[] filters) => new(Language.Undefined,
        Currency.Undefined, new FilterCollection(new[] { Equal(OwnerKey, _options.Scope), Equal(IndexKey, alias) }.Concat(filters).ToArray()),
        ContentAdministrativeAction.UpdateKind.Delete);

    private void EnsureIndex(string alias)
    {
        if (!_options.IndexAliases.Contains(alias)) throw new ArgumentException($"Index '{alias}' is not registered with Relewise.", nameof(alias));
    }

    private SemaphoreSlim WriteLock(string alias) => WriteLocks[(uint)HashCode.Combine(_options.ClientName, _options.Scope, alias) % (uint)WriteLocks.Length];

    internal static RFilter MapFilter(UFilter filter)
    {
        RFilter result = filter switch
        {
            KeywordFilter f => Any(f.Values.Select(v => Contains(Field(f.FieldName, "keyword"), v))),
            TextFilter f => Any(f.Values.Select(v => Contains(Field(f.FieldName, "text"), v))),
            IntegerExactFilter f => NumericExact(f.FieldName, "integer", f.Values.Select(v => (double)v)),
            DecimalExactFilter f => NumericExact(f.FieldName, "decimal", f.Values.Select(v => (double)v)),
            DateTimeOffsetExactFilter f => NumericExact(f.FieldName, "date", f.Values.Select(DateNumber)),
            IntegerRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "integer", r.MinValue, r.MaxValue))),
            DecimalRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "decimal", (double?)r.MinValue, (double?)r.MaxValue))),
            DateTimeOffsetRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "date", r.MinValue.HasValue ? DateNumber(r.MinValue.Value) : null, r.MaxValue.HasValue ? DateNumber(r.MaxValue.Value) : null))),
            _ => throw new NotSupportedException($"The Relewise provider does not support filter '{filter.GetType().Name}'.")
        };
        return filter.Negate ? Negate(result) : result;
    }

    private static RFilter NumericExact(string field, string kind, IEnumerable<double> values) =>
        Any(values.Select(v => Contains(Field(field, kind + "Exact"), NumberString(v))));

    private static RFilter NumberRange(string field, string kind, double? min, double? max)
    {
        if (!min.HasValue && !max.HasValue) return new ContentDataFilter(Field(field, kind + "Values"), new HasValueCondition());
        // Relewise ranges are inclusive. The previous representable double makes the upper
        // endpoint exclusive without arbitrary epsilons, for integers, decimals and dates alike.
        double lower = min ?? double.MinValue;
        double upper = max.HasValue ? Math.BitDecrement(max.Value) : double.MaxValue;
        if (lower > upper) return Any([]);
        return new ContentDataFilter(Field(field, kind + "Values"), new ContainsCondition(new DataObjectFilter(new ObjectValueCondition[] {
            new ObjectValueInRangeCondition("value", new Relewise.Client.DataTypes.Range<double>(lower, upper))
        })));
    }

    private static RFilter Any(IEnumerable<RFilter> filters)
    {
        RFilter[] items = filters.ToArray();
        // Explicit false predicate for empty ORs; do not rely on backend empty-group behavior.
        return items.Length == 0 ? new AndFilter([Equal(IdKey, ""), Negate(Equal(IdKey, ""))]) : new OrFilter(items);
    }

    private static void ApplySorting(ContentSearchRequest request, IEnumerable<Sorter> sorters)
    {
        ContentSorting? previous = null;
        foreach (Sorter sorter in sorters)
        {
            SortOrder order = sorter.Direction == Direction.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            ContentSorting current = sorter switch
            {
                ScoreSorter => new ContentRelevanceSorting(order),
                KeywordSorter => new ContentDataSorting(Field(sorter.FieldName, "keywordSort"), order),
                TextSorter => new ContentDataSorting(Field(sorter.FieldName, "textSort"), order),
                IntegerSorter => NumericSort("integer"), DecimalSorter => NumericSort("decimal"), DateTimeOffsetSorter => NumericSort("date"),
                _ => throw new NotSupportedException($"The Relewise provider does not support sorter '{sorter.GetType().Name}'.")
            };
            if (previous is null) request.Sorting!.SortBy(current); else previous.ThenBy = current;
            previous = current;
            ContentSorting NumericSort(string kind) => new ContentDataSorting(Field(sorter.FieldName,
                kind + (sorter.Direction == Direction.Ascending ? "Min" : "Max")), order);
        }
    }

    private async Task<UFacetResult> ComputeFacet(ContentSearchRequest request, UFacet facet)
    {
        var values = new List<FacetValue>();
        switch (facet)
        {
            case KeywordFacet:
            case IntegerExactFacet:
            case DecimalExactFacet:
            case DateTimeOffsetExactFacet:
                string kind = facet switch { KeywordFacet => "keyword", IntegerExactFacet => "integerExact", DecimalExactFacet => "decimalExact", _ => "dateExact" };
                request.Facets!.AddDataString(Field(facet.FieldName, kind));
                var response = await Execute(request);
                foreach (var bucket in response.Facets?.DataString(Field(facet.FieldName, kind))?.Available ?? [])
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
                foreach (var range in f.Ranges) values.Add(new IntegerRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, NumberRange(f.FieldName, "integer", range.MinValue, range.MaxValue))));
                break;
            case DecimalRangeFacet f:
                foreach (var range in f.Ranges) values.Add(new DecimalRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, NumberRange(f.FieldName, "decimal", (double?)range.MinValue, (double?)range.MaxValue))));
                break;
            case DateTimeOffsetRangeFacet f:
                foreach (var range in f.Ranges) values.Add(new DateTimeOffsetRangeFacetValue(range.Key, range.MinValue, range.MaxValue,
                    await CountRange(request, NumberRange(f.FieldName, "date", range.MinValue.HasValue ? DateNumber(range.MinValue.Value) : null,
                        range.MaxValue.HasValue ? DateNumber(range.MaxValue.Value) : null))));
                break;
            default: throw new NotSupportedException($"The Relewise provider does not support facet '{facet.GetType().Name}'.");
        }
        return new UFacetResult(facet.FieldName, values);
    }

    private async Task<long> CountRange(ContentSearchRequest request, RFilter filter)
    {
        request.Filters!.Items!.Add(filter);
        try { return (await Execute(request)).Hits; }
        finally { request.Filters!.Items!.Remove(filter); }
    }

    private static IEnumerable<string> Texts(IndexValue value) =>
        (value.TextsR1 ?? []).Concat(value.TextsR2 ?? []).Concat(value.TextsR3 ?? []).Concat(value.Texts ?? []);

    private static void StoreStrings(Dictionary<string, DataValue?> data, string field, string kind, IEnumerable<string> source)
    {
        string[] values = source.Distinct().ToArray();
        if (values.Length == 0) return;
        data[Field(field, kind)] = kind == "text" ? new DataValue(string.Join(" ", values)) : new DataValue(values);
        data[Field(field, kind + "Sort")] = values.Order(StringComparer.Ordinal).First();
    }

    private static void StoreNumbers(Dictionary<string, DataValue?> data, string field, string kind, IEnumerable<double> source)
    {
        double[] values = source.Distinct().ToArray();
        if (values.Length == 0) return;
        data[Field(field, kind + "Exact")] = new DataValue(values.Select(NumberString));
        data[Field(field, kind + "Values")] = new DataValue(values.Select(v => new DataObject(new Dictionary<string, DataValue?> { ["value"] = v })));
        data[Field(field, kind + "Min")] = values.Min();
        data[Field(field, kind + "Max")] = values.Max();
    }

    internal static string Field(string name, string kind) => $"rwUmbracoField_{Encode(name)}_{kind}";
    private static string Encode(string value) => Convert.ToHexString(Encoding.UTF8.GetBytes(value));
    private static string NumberString(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static double ParseNumber(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    private static double DateNumber(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
    internal static ContentDataFilter Equal(string key, DataValue value) => new(key, new EqualsCondition(value));
    private static ContentDataFilter Contains(string key, DataValue value) => new(key, new ContainsCondition(value));
    // ContentDataFilter's inherited Negated flag is not applied by the backend.
    // Boolean group negation is supported and also preserves missing-field semantics.
    private static RFilter Negate(RFilter filter) => new AndFilter([filter], negated: true);
}
