using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using static Relewise.Integrations.Umbraco.Search.CoreSearchTranslation;
using RFacet = Relewise.Client.DataTypes.Search.Facets.Queries.Facet;
using RFacetResult = Relewise.Client.DataTypes.Search.Facets.Result.FacetResult;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

// Native selections let Relewise calculate each facet without its own selection in the same search.
internal static class CoreSearchFacets
{
    internal static UFilter[] Configure(
        ContentFacetQuery query,
        UFacet[] facets,
        UFilter[] filters,
        string? culture,
        IReadOnlyDictionary<string, string> booleanDataFields)
    {
        var consumed = new HashSet<UFilter>();

        foreach (UFacet facet in facets)
        {
            if (!IsExact(facet))
            {
                throw new NotSupportedException(
                    $"Range facet '{facet.GetType().Name}' is not supported for indexed Data fields. " +
                    "Product providers can bind a DecimalRangeFacet to SalesPrice with MapSalesPrice().");
            }

            UFilter[] matching = [.. filters.Where(filter => filter.FieldName == facet.FieldName)];
            // Multiple predicates are ANDed by Umbraco. Do not merge them into an OR selection.
            UFilter? selection = matching.Length == 1 && !matching[0].Negate &&
                                 facets.Count(value => value.FieldName == facet.FieldName) == 1
                ? matching[0]
                : null;
            string key = Key(facet, culture, booleanDataFields);
            bool selected;

            if (facet is KeywordFacet && booleanDataFields.ContainsKey(facet.FieldName))
            {
                List<bool>? values = selection is KeywordFilter { Values.Length: > 0 } boolean
                    ? boolean.Values.Select(value => bool.TryParse(value, out bool parsed)
                        ? parsed
                        : throw new ArgumentException("Boolean facet selections require true or false values."))
                        .ToList()
                    : null;
                query.AddDataBoolean(key, values);
                selected = values != null;
            }
            else if (facet is KeywordFacet)
            {
                List<string>? values = selection is KeywordFilter { Values.Length: > 0 } keyword
                    ? keyword.Values.ToList()
                    : null;
                query.AddDataString(key, CollectionFilterType.Or, values);
                selected = values != null;
            }
            else
            {
                List<string>? values = ExactSelection(facet, selection);
                query.AddDataString(key, CollectionFilterType.Or, values);
                selected = values != null;
            }
            if (selected)
            {
                consumed.Add(selection!);
            }
        }

        return [.. filters.Where(filter => !consumed.Contains(filter))];
    }

    private static List<string>? ExactSelection(UFacet facet, UFilter? filter)
    {
        IEnumerable<double>? values = (facet, filter) switch
        {
            (IntegerExactFacet, IntegerExactFilter f) => f.Values.Select(value => (double)value),
            (DecimalExactFacet, DecimalExactFilter f) => f.Values.Select(value => (double)value),
            (DateTimeOffsetExactFacet, DateTimeOffsetExactFilter f) => f.Values.Select(DateNumber),
            _ => null
        };
        List<string>? selected = values?.Select(value =>
            value.ToString("R", CultureInfo.InvariantCulture)).ToList();

        return selected is { Count: > 0 } ? selected : null;
    }

    internal static UFacetResult[] Read(
        ContentFacetResult? response,
        UFacet[] facets,
        string? culture,
        IReadOnlyDictionary<string, string> booleanDataFields)
    {
        return [.. facets.Select(facet => ReadFacet(response, facet, culture, booleanDataFields))];
    }

    private static UFacetResult ReadFacet(
        ContentFacetResult? response,
        UFacet facet,
        string? culture,
        IReadOnlyDictionary<string, string> booleanDataFields)
    {
        string key = Key(facet, culture, booleanDataFields);
        var values = new List<FacetValue>();

        if (facet is KeywordFacet && booleanDataFields.ContainsKey(facet.FieldName))
        {
            values.AddRange((response?.DataBoolean(key)?.Available ?? [])
                .Select(value => new KeywordFacetValue(value.Value ? "true" : "false", value.Hits)));
        }
        else if (facet is KeywordFacet)
        {
            values.AddRange((response?.DataString(key)?.Available ?? [])
                .Select(value => new KeywordFacetValue(value.Value, value.Hits)));
        }
        else if (IsExact(facet))
        {
            foreach (AvailableFacetValue<string> value in response?.DataString(key)?.Available ?? [])
            {
                values.Add(facet switch
                {
                    IntegerExactFacet => new IntegerExactFacetValue(
                        checked((int)ParseNumber(value.Value)), value.Hits),
                    DecimalExactFacet => new DecimalExactFacetValue((decimal)ParseNumber(value.Value), value.Hits),
                    _ => new DateTimeOffsetExactFacetValue(
                        DateTimeOffset.FromUnixTimeMilliseconds(checked((long)ParseNumber(value.Value))), value.Hits)
                });
            }
        }

        return new UFacetResult(facet.FieldName, values);
    }

    private static bool IsExact(UFacet facet)
    {
        return facet is KeywordFacet or IntegerExactFacet or DecimalExactFacet or DateTimeOffsetExactFacet;
    }

    private static string Key(UFacet facet, string? culture, IReadOnlyDictionary<string, string> booleanDataFields)
    {
        if (booleanDataFields.TryGetValue(facet.FieldName, out string? key))
        {
            return key;
        }

        string kind = facet switch
        {
            KeywordFacet => "keyword",
            IntegerExactFacet => "integerExact",
            DecimalExactFacet => "decimalExact",
            DateTimeOffsetExactFacet => "dateExact",
            _ => throw new NotSupportedException($"Unsupported facet '{facet.GetType().Name}'.")
        };

        return Field(facet.FieldName, kind, culture);
    }

    internal static List<RFacet> Merge(IEnumerable<RFacet> required, IEnumerable<RFacet> configured)
    {
        RFacet[] core = [.. required];
        HashSet<string?> keys = [.. core.Select(DataKey)];

        return [.. configured.Where(facet => !keys.Contains(DataKey(facet))), .. core];
    }

    private static string? DataKey(RFacet facet)
    {
        return facet switch
        {
            DataValueFacet<string> f => f.Key,
            DataValueFacet<bool> f => f.Key,
            DataValueFacet<double> f => f.Key,
            _ => null
        };
    }

    internal static RFacet ForProduct(RFacet facet)
    {
        return facet switch
        {
            ContentDataStringValueFacet f => new ProductDataStringValueFacet(
                DataSelectionStrategy.Product, f.Key, f.Selected, f.CollectionFilterType)
            { Settings = f.Settings },
            ContentDataBooleanValueFacet f => new ProductDataBooleanValueFacet(
                DataSelectionStrategy.Product, f.Key, f.Selected, f.CollectionFilterType)
            { Settings = f.Settings },
            _ => throw new NotSupportedException($"Cannot translate facet '{facet.GetType().Name}' to Product search.")
        };
    }

    internal static RFacetResult ForContent(RFacetResult facet)
    {
        return facet switch
        {
            ProductDataStringValueFacetResult f => new ContentDataStringValueFacetResult(
                f.Key, f.Selected, f.CollectionFilterType, f.Available),
            ProductDataBooleanValueFacetResult f => new ContentDataBooleanValueFacetResult(
                f.Key, f.Selected, f.CollectionFilterType, f.Available),
            _ => facet
        };
    }
}
