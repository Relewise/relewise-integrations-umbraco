using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.DataTypes.Search.Sorting.Enums;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Filters.DataObjects;
using Relewise.Client.Requests.Filters.DataObjects.Conditions;
using Relewise.Client.Requests.Search;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

internal static class CoreSearchTranslation
{
    internal const string IdKey = "rwUmbracoNodeKey";
    internal const string CultureKey = "rwUmbracoSearchCulture";
    internal const string SegmentsKey = "rwUmbracoSearchSegments";
    internal const string AccessKey = "rwUmbracoSearchAccess";
    internal const string ProtectedKey = "rwUmbracoSearchProtected";
    internal static RFilter MapFilter(UFilter filter, string? culture = null)
    {
        RFilter result = filter switch
        {
            KeywordFilter f => Any(f.Values.Select(v => Contains(Field(f.FieldName, "keyword"), v))),
            TextFilter f => Any(f.Values.Select(v => Contains(Field(f.FieldName, "text"), v))),
            IntegerExactFilter f => NumericExact(f.FieldName, "integer", f.Values.Select(v => (double)v), culture),
            DecimalExactFilter f => NumericExact(f.FieldName, "decimal", f.Values.Select(v => (double)v), culture),
            DateTimeOffsetExactFilter f => NumericExact(f.FieldName, "date", f.Values.Select(DateNumber), culture),
            IntegerRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "integer", r.MinValue, r.MaxValue, culture))),
            DecimalRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "decimal", (double?)r.MinValue, (double?)r.MaxValue, culture))),
            DateTimeOffsetRangeFilter f => Any(f.Ranges.Select(r => NumberRange(f.FieldName, "date", r.MinValue.HasValue ? DateNumber(r.MinValue.Value) : null, r.MaxValue.HasValue ? DateNumber(r.MaxValue.Value) : null, culture))),
            _ => throw new NotSupportedException($"The Relewise provider does not support filter '{filter.GetType().Name}'.")
        };
        return filter.Negate ? Negate(result) : result;
    }

    private static RFilter NumericExact(string field, string kind, IEnumerable<double> values, string? culture = null) =>
        Any(values.Select(v => Contains(Field(field, kind + "Exact", culture), NumberString(v))));

    internal static RFilter NumberRange(string field, string kind, double? min, double? max, string? culture = null)
    {
        if (!min.HasValue && !max.HasValue) return new ContentDataFilter(Field(field, kind + "Values", culture), new HasValueCondition());
        // Relewise ranges are inclusive. The previous representable double makes the upper
        // endpoint exclusive without arbitrary epsilons, for integers, decimals and dates alike.
        double lower = min ?? double.MinValue;
        double upper = max.HasValue ? Math.BitDecrement(max.Value) : double.MaxValue;
        if (lower > upper) return Any([]);
        return new ContentDataFilter(Field(field, kind + "Values", culture), new ContainsCondition(new DataObjectFilter(new ObjectValueCondition[] {
            new ObjectValueInRangeCondition("value", new Relewise.Client.DataTypes.Range<double>(lower, upper))
        })));
    }

    private static RFilter Any(IEnumerable<RFilter> filters)
    {
        RFilter[] items = filters.ToArray();
        // Explicit false predicate for empty ORs; do not rely on backend empty-group behavior.
        return items.Length == 0 ? new AndFilter([Equal(IdKey, ""), Negate(Equal(IdKey, ""))]) : new OrFilter(items);
    }

    internal static void ApplySorting(ContentSearchRequest request, IEnumerable<Sorter> sorters, string? culture = null)
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
                kind + (sorter.Direction == Direction.Ascending ? "Min" : "Max"), culture), order);
        }
    }

    internal static IEnumerable<string> Texts(IndexValue value) =>
        (value.TextsR1 ?? []).Concat(value.TextsR2 ?? []).Concat(value.TextsR3 ?? []).Concat(value.Texts ?? []);

    internal static void StoreStrings(Dictionary<string, DataValue?> data, string field, string kind, IEnumerable<string> source)
    {
        string[] values = source.Distinct().ToArray();
        if (values.Length == 0) return;
        data[Field(field, kind)] = kind == "text" ? new DataValue(string.Join(" ", values)) : new DataValue(values);
        data[Field(field, kind + "Sort")] = values.Order(StringComparer.Ordinal).First();
    }

    internal static void StoreNumbers(Dictionary<string, DataValue?> data, string field, string kind, IEnumerable<double> source)
    {
        double[] values = source.Distinct().ToArray();
        if (values.Length == 0) return;
        data[Field(field, kind + "Exact")] = new DataValue(values.Select(NumberString));
        data[Field(field, kind + "Values")] = new DataValue(values.Select(v => new DataObject(new Dictionary<string, DataValue?> { ["value"] = v })));
        data[Field(field, kind + "Min")] = values.Min();
        data[Field(field, kind + "Max")] = values.Max();
    }

    internal static string Field(string name, string kind, string? culture = null)
        => $"rwUmbracoField_{Uri.EscapeDataString(name)}_{kind}" + (culture != null && (kind.StartsWith("integer", StringComparison.Ordinal) || kind.StartsWith("decimal", StringComparison.Ordinal) || kind.StartsWith("date", StringComparison.Ordinal)) ? "_" + Uri.EscapeDataString(culture.ToLowerInvariant()) : string.Empty);
    private static string NumberString(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    internal static double ParseNumber(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    internal static double DateNumber(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
    internal static ContentDataFilter Equal(string key, DataValue value) => new(key, new EqualsCondition(value));
    private static ContentDataFilter Contains(string key, DataValue value) => new(key, new ContainsCondition(value));
    // ContentDataFilter's inherited Negated flag is not applied by the backend.
    // Boolean group negation is supported and also preserves missing-field semantics.
    private static RFilter Negate(RFilter filter) => new AndFilter([filter], negated: true);
}
