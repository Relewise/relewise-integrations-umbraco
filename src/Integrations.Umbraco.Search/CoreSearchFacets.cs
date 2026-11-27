using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using RFacet = Relewise.Client.DataTypes.Search.Facets.Queries.Facet;
using RFacetResult = Relewise.Client.DataTypes.Search.Facets.Result.FacetResult;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

// Native facet selections return results and disjunctive counts in the same search request.
internal static class CoreSearchFacets
{
    public static UFilter[] Configure(
        ContentFacetQuery query,
        UFacet[] facets,
        UFilter[] filters,
        Func<string, SearchFieldDefinition> resolve)
    {
        var consumed = new HashSet<UFilter>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (UFacet facet in facets)
        {
            SearchFieldDefinition field = resolve(facet.FieldName);
            string identity = field.DataKey == null ? "native:" + field.Kind : "data:" + field.DataKey;

            if (!keys.Add(identity))
            {
                throw new NotSupportedException($"Multiple facets target the same stored field '{identity}'.");
            }

            UFilter[] matching = [.. filters.Where(filter => filter.FieldName == facet.FieldName)];
            UFilter? selection = matching is [{ Negate: false } single] ? single : null;
            RFacet native = Create(facet, field, selection, out bool selected);
            query.Items.Add(native);

            if (selected)
            {
                consumed.Add(selection!);
            }
        }

        return [.. filters.Where(filter => !consumed.Contains(filter))];
    }

    private static RFacet Create(UFacet facet, SearchFieldDefinition field, UFilter? selection, out bool selected)
    {
        List<string>? words = selection is KeywordFilter { Values.Length: > 0 } keyword
            ? [.. keyword.Values]
            : null;
        selected = false;

        if (facet is KeywordFacet)
        {
            if (field.Kind == SearchFieldKind.Brand)
            {
                selected = words != null;
                return new BrandFacet(words);
            }

            if (field.Kind == SearchFieldKind.Categories)
            {
                selected = words != null;
                return new CategoryFacet(CategorySelectionStrategy.Ancestors, words);
            }

            if (field.Kind == SearchFieldKind.Data && field.ValueType == typeof(string))
            {
                selected = words != null;
                return new ContentDataStringValueFacet(field.DataKey!, words, CollectionFilterType.Or);
            }

            if (field.Kind == SearchFieldKind.Data && field.ValueType == typeof(bool))
            {
                List<bool>? booleans = words?.Select(value => bool.TryParse(value, out bool parsed)
                        ? parsed
                        : throw new ArgumentException("Boolean facets require true or false selections."))
                    .ToList();
                selected = booleans != null;
                return new ContentDataBooleanValueFacet(field.DataKey!, booleans, CollectionFilterType.Or);
            }
        }
        else if (field.Kind == SearchFieldKind.Data && field.Numeric)
        {
            if (facet is DecimalExactFacet || facet is IntegerExactFacet && field.ValueType == typeof(int))
            {
                List<double>? values = selection switch
                {
                    IntegerExactFilter { Values.Length: > 0 } integers =>
                        integers.Values.Select(v => (double)v).ToList(),
                    DecimalExactFilter { Values.Length: > 0 } numbers =>
                        numbers.Values.Select(v => (double)v).ToList(),
                    _ => null
                };
                selected = values != null;
                return new ContentDataDoubleValueFacet(field.DataKey!, values, CollectionFilterType.Or);
            }

            if (!field.Collection && facet is IntegerRangeFacet or DecimalRangeFacet)
            {
                List<ChainableRange<double?>> ranges = Ranges(facet)
                    .Select(value =>
                        new ChainableRange<double?>(value.Min, value.Max))
                    .ToList();
                ValidateRanges(ranges);
                List<ChainableRange<double?>>? values = selection switch
                {
                    IntegerRangeFilter f when f.Ranges.Any() => f.Ranges
                        .Select(r => new ChainableRange<double?>(r.MinValue, r.MaxValue))
                        .ToList(),
                    DecimalRangeFilter f when f.Ranges.Any() => f.Ranges
                        .Select(r => new ChainableRange<double?>((double?)r.MinValue, (double?)r.MaxValue))
                        .ToList(),
                    _ => null
                };

                if (values != null)
                {
                    ValidateRanges(values);
                }

                selected = values != null;
                return new ContentDataDoubleRangesFacet(
                    field.DataKey!,
                    predefinedRanges: ranges,
                    expandedRangeSize: null,
                    selected: values);
            }
        }

        throw CoreSearchTranslation.Unsupported(facet.FieldName, facet.GetType().Name);
    }

    public static UFacetResult[] Read(
        ContentFacetResult? response,
        UFacet[] facets,
        Func<string, SearchFieldDefinition> resolve)
    {
        return [.. facets.Select(facet => ReadFacet(response, facet, resolve(facet.FieldName)))];
    }

    private static UFacetResult ReadFacet(ContentFacetResult? response, UFacet facet, SearchFieldDefinition field)
    {
        string key = field.DataKey == null
            ? "native:" + (field.Kind == SearchFieldKind.Brand ? FacetingField.Brand : FacetingField.Category)
            : "data:" + field.DataKey;
        RFacetResult result = response?.Items.SingleOrDefault(item => Key(item) == key) ??
                              throw new InvalidOperationException(
                                  $"Relewise did not return the requested facet for '{facet.FieldName}'.");
        IEnumerable<FacetValue> values = result switch
        {
            BrandFacetResult brand when field.Kind == SearchFieldKind.Brand => brand.Available.Select(value =>
                new KeywordFacetValue(value.Value.Id, value.Hits)),
            CategoryFacetResult category when field.Kind == SearchFieldKind.Categories =>
                category.Available.Select(value => new KeywordFacetValue(value.Value.Id, value.Hits)),
            ContentDataStringValueFacetResult text when facet is KeywordFacet && field.ValueType == typeof(string) =>
                text.Available.Select(value =>
                    new KeywordFacetValue(value.Value, value.Hits)),
            ContentDataBooleanValueFacetResult boolean when facet is KeywordFacet && field.ValueType == typeof(bool) =>
                boolean.Available.Select(value =>
                    new KeywordFacetValue(value.Value ? "true" : "false", value.Hits)),
            ContentDataDoubleValueFacetResult number when facet is IntegerExactFacet or DecimalExactFacet =>
                number.Available.Select(value =>
                    facet is IntegerExactFacet
                        ? (FacetValue)new IntegerExactFacetValue(IntegerValue(value.Value), value.Hits)
                        : new DecimalExactFacetValue((decimal)value.Value, value.Hits)),
            ContentDataDoubleRangesFacetResult ranges when facet is IntegerRangeFacet or DecimalRangeFacet =>
                RangeValues(facet, ranges),
            _ => throw new InvalidOperationException($"Unexpected response type for facet '{facet.FieldName}'.")
        };

        return new UFacetResult(facet.FieldName, values.ToArray());
    }

    private static int IntegerValue(double value)
    {
        if (!double.IsFinite(value) || !value.Equals(Math.Truncate(value)) ||
            value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidOperationException(
                "An integer facet returned a value outside the declared integer contract.");
        }

        return (int)value;
    }

    private static IEnumerable<FacetValue> RangeValues(UFacet facet, ContentDataDoubleRangesFacetResult result)
    {
        foreach ((string key, double? min, double? max) in Ranges(facet))
        {
            // Match the exact bucket bounds sent to the SDK, rather than rounding values into another bucket.
            long count = result.Available.FirstOrDefault(value =>
                    Nullable.Equals(value.Value.LowerBoundInclusive, min) &&
                    Nullable.Equals(value.Value.UpperBoundExclusive, max))
                ?.Hits ?? 0;
            yield return facet is IntegerRangeFacet
                ? new IntegerRangeFacetValue(key, (int?)min, (int?)max, count)
                : new DecimalRangeFacetValue(key, (decimal?)min, (decimal?)max, count);
        }
    }

    private static IEnumerable<(string Key, double? Min, double? Max)> Ranges(UFacet facet)
    {
        return facet switch
        {
            IntegerRangeFacet integers =>
                integers.Ranges.Select(r => (r.Key, (double?)r.MinValue, (double?)r.MaxValue)),
            DecimalRangeFacet numbers => numbers.Ranges.Select(r => (r.Key, (double?)r.MinValue, (double?)r.MaxValue)),
            _ => throw CoreSearchTranslation.Unsupported(facet.FieldName, facet.GetType().Name)
        };
    }

    private static void ValidateRanges(List<ChainableRange<double?>> ranges)
    {
        ChainableRange<double?>[] sorted = [.. ranges.OrderBy(r => r.LowerBoundInclusive ?? double.NegativeInfinity)];

        if (sorted.Length == 0)
        {
            throw new NotSupportedException("Range facets require explicit nonempty buckets.");
        }

        for (var i = 0; i < sorted.Length; i++)
        {
            double? lower = sorted[i].LowerBoundInclusive, upper = sorted[i].UpperBoundExclusive;

            if (lower.HasValue && !double.IsFinite(lower.Value) || upper.HasValue && !double.IsFinite(upper.Value) ||
                lower >= upper || i > 0 &&
                (sorted[i - 1].UpperBoundExclusive ?? double.PositiveInfinity) > (lower ?? double.NegativeInfinity))
            {
                throw new NotSupportedException("Numeric facet ranges must be finite, nonempty and nonoverlapping.");
            }
        }
    }

    public static List<RFacet> Merge(IEnumerable<RFacet> required, IEnumerable<RFacet> configured)
    {
        RFacet[] core = [.. required];
        HashSet<string> keys = [.. core.Select(Key)];

        return [.. configured.Where(facet => !keys.Contains(Key(facet))), .. core];
    }

    private static string Key(RFacet facet)
    {
        return facet switch
        {
            DataValueFacet<string> value => "data:" + value.Key,
            DataValueFacet<bool> value => "data:" + value.Key,
            DataValueFacet<double> value => "data:" + value.Key,
            ContentDataDoubleRangesFacet value => "data:" + value.Key,
            ProductDataDoubleRangesFacet value => "data:" + value.Key,
            _ => "native:" + facet.Field
        };
    }

    private static string Key(RFacetResult facet)
    {
        return facet switch
        {
            ContentDataStringValueFacetResult value => "data:" + value.Key,
            ContentDataBooleanValueFacetResult value => "data:" + value.Key,
            ContentDataDoubleValueFacetResult value => "data:" + value.Key,
            ContentDataDoubleRangesFacetResult value => "data:" + value.Key,
            _ => "native:" + facet.Field
        };
    }

    public static RFacet ForProduct(RFacet facet)
    {
        return facet switch
        {
            ContentDataStringValueFacet f => new ProductDataStringValueFacet(
                    DataSelectionStrategy.Product,
                    f.Key,
                    f.Selected,
                    f.CollectionFilterType)
            { Settings = f.Settings },
            ContentDataBooleanValueFacet f => new ProductDataBooleanValueFacet(
                    DataSelectionStrategy.Product,
                    f.Key,
                    f.Selected,
                    f.CollectionFilterType)
            { Settings = f.Settings },
            ContentDataDoubleValueFacet f => new ProductDataDoubleValueFacet(
                    DataSelectionStrategy.Product,
                    f.Key,
                    f.Selected,
                    f.CollectionFilterType)
            { Settings = f.Settings },
            ContentDataDoubleRangesFacet f => new ProductDataDoubleRangesFacet(
                    f.Key,
                    DataSelectionStrategy.Product,
                    f.PredefinedRanges,
                    f.ExpandedRangeSize,
                    f.Selected)
            { Settings = f.Settings },
            BrandFacet or CategoryFacet => facet,
            _ => throw new NotSupportedException($"Cannot translate facet '{facet.GetType().Name}' to Product search.")
        };
    }

    public static RFacetResult ForContent(RFacetResult facet)
    {
        return facet switch
        {
            ProductDataStringValueFacetResult f => new ContentDataStringValueFacetResult(
                f.Key,
                f.Selected,
                f.CollectionFilterType,
                f.Available),
            ProductDataBooleanValueFacetResult f => new ContentDataBooleanValueFacetResult(
                f.Key,
                f.Selected,
                f.CollectionFilterType,
                f.Available),
            ProductDataDoubleValueFacetResult f => new ContentDataDoubleValueFacetResult(
                f.Key,
                f.Selected,
                f.CollectionFilterType,
                f.Available),
            ProductDataDoubleRangesFacetResult f => new ContentDataDoubleRangesFacetResult(
                f.Key,
                f.ExpandedRangeSize,
                f.Selected,
                f.Available),
            _ => facet
        };
    }
}
