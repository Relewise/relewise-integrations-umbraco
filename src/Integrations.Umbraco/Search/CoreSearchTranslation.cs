using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Sorting.Content;
using Relewise.Client.DataTypes.Search.Sorting.Enums;
using Relewise.Client.DataTypes.Search.Sorting.Product;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

internal static class CoreSearchTranslation
{
    public static RFilter MapFilter(UFilter filter, SearchFieldDefinition field, bool product, string? culture)
    {
        RFilter result;

        if (filter is KeywordFilter keyword)
        {
            result = Keyword(field, keyword.Values, product, culture);
        }
        else if (filter is IntegerExactFilter integer && field.Numeric && field.Kind == SearchFieldKind.Data)
        {
            result = Any(
                integer.Values.Select(value => Data(
                    field,
                    field.Collection ? new ContainsCondition((double)value) : new EqualsCondition((double)value),
                    culture)));
        }
        else if (filter is DecimalExactFilter number && field.Numeric && field.Kind == SearchFieldKind.Data)
        {
            result = Any(
                number.Values.Select(value => Data(
                    field,
                    field.Collection ? new ContainsCondition((double)value) : new EqualsCondition((double)value),
                    culture)));
        }
        else if (filter is IntegerRangeFilter integers && field.Numeric && !field.Collection &&
                 field.Kind == SearchFieldKind.Data)
        {
            result = Any(integers.Ranges.Select(range => NumberRange(field, range.MinValue, range.MaxValue, culture)));
        }
        else if (filter is DecimalRangeFilter numbers && field.Numeric && !field.Collection &&
                 field.Kind == SearchFieldKind.Data)
        {
            result = Any(
                numbers.Ranges.Select(range =>
                    NumberRange(field, (double?)range.MinValue, (double?)range.MaxValue, culture)));
        }
        else
        {
            throw Unsupported(filter.FieldName, filter.GetType().Name);
        }

        // Group negation preserves the distinction between a missing value and an explicit false value.
        return filter.Negate ? new AndFilter([result], negated: true) : result;
    }

    private static RFilter Keyword(SearchFieldDefinition field, string[] values, bool product, string? culture)
    {
        if (values.Length == 0)
        {
            return Any([]);
        }

        return field.Kind switch
        {
            SearchFieldKind.Id => product ? new ProductIdFilter(values) : new ContentIdFilter(values),
            SearchFieldKind.Brand when product => new BrandIdFilter(values),
            SearchFieldKind.Categories => product
                ? new ProductCategoryIdFilter(values, CategoryScope.Ancestor)
                : new ContentCategoryIdFilter(values, CategoryScope.Ancestor),
            SearchFieldKind.Data when field.ValueType == typeof(bool) => Any(
                values.Select(value =>
                    bool.TryParse(value, out bool parsed)
                        ? Data(field, new EqualsCondition(parsed), culture)
                        : throw new ArgumentException("Boolean query fields require true or false values."))),
            SearchFieldKind.Data when field.ValueType == typeof(string) => Any(
                values.Select(value =>
                    Data(
                        field,
                        field.Collection ? new ContainsCondition(value) : new EqualsCondition(value),
                        culture))),
            _ => throw Unsupported(field.DataKey ?? field.Kind.ToString(), nameof(KeywordFilter))
        };
    }

    private static RFilter NumberRange(SearchFieldDefinition field, double? min, double? max, string? culture)
    {
        if (min.HasValue && !double.IsFinite(min.Value) || max.HasValue && !double.IsFinite(max.Value))
        {
            throw new ArgumentException("Numeric ranges require finite bounds.");
        }

        if (min.HasValue && max.HasValue && min >= max)
        {
            return Any([]);
        }

        var bounds = new List<RFilter>();

        if (min.HasValue)
        {
            bounds.Add(Data(field, new LessThanCondition(min.Value, negated: true), culture));
        }

        if (max.HasValue)
        {
            bounds.Add(Data(field, new LessThanCondition(max.Value), culture));
        }

        // An unbounded range still requires a stored value; missing Data must not match.
        return bounds.Count == 0 ? Data(field, new HasValueCondition(), culture) : new AndFilter([.. bounds]);
    }

    private static ContentDataFilter Data(SearchFieldDefinition field, ValueCondition condition, string? culture)
    {
        return new ContentDataFilter(
            field.DataKey!,
            condition,
            language: culture == null ? null : new Language(culture));
    }

    private static RFilter Any(IEnumerable<RFilter> filters)
    {
        RFilter[] items = [.. filters];
        ContentDataFilter empty = Equal(RelewiseDataKeys.NodeKey, "");

        return items.Length == 0 ? new AndFilter([empty, new AndFilter([empty], negated: true)]) : new OrFilter(items);
    }

    public static void ApplySorting(
        ContentSearchRequest request,
        IEnumerable<Sorter> sorters,
        Func<string, SearchFieldDefinition> resolve)
    {
        ContentSorting? previous = null;

        foreach (Sorter sorter in sorters)
        {
            SortOrder order = sorter.Direction == Direction.Descending ? SortOrder.Descending : SortOrder.Ascending;
            ContentSorting current;

            if (sorter is ScoreSorter)
            {
                current = new ContentRelevanceSorting(order);
            }
            else
            {
                SearchFieldDefinition field = SortField(sorter, resolve);
                current = field.Kind switch
                {
                    SearchFieldKind.Id =>
                        new ContentAttributeSorting(ContentAttributeSorting.SortableAttribute.Id, order),
                    SearchFieldKind.DisplayName =>
                        new ContentAttributeSorting(ContentAttributeSorting.SortableAttribute.DisplayName, order),
                    SearchFieldKind.Data => new ContentDataSorting(
                        field.DataKey!,
                        order,
                        field.Numeric ? SortMode.Numerical : SortMode.Alphabetical),
                    _ => throw Unsupported(sorter.FieldName, sorter.GetType().Name)
                };
            }

            if (previous == null)
            {
                request.Sorting!.SortBy(current);
            }
            else
            {
                previous.ThenBy = current;
            }

            previous = current;
        }
    }

    public static void ApplySorting(
        ProductSearchRequest request,
        IEnumerable<Sorter> sorters,
        Func<string, SearchFieldDefinition> resolve)
    {
        ProductSorting? previous = null;

        foreach (Sorter sorter in sorters)
        {
            SortOrder order = sorter.Direction == Direction.Descending ? SortOrder.Descending : SortOrder.Ascending;
            ProductSorting current;

            if (sorter is ScoreSorter)
            {
                current = new ProductRelevanceSorting(order);
            }
            else
            {
                SearchFieldDefinition field = SortField(sorter, resolve);
                current = field.Kind switch
                {
                    SearchFieldKind.Id =>
                        new ProductAttributeSorting(ProductAttributeSorting.SortableAttribute.Id, order),
                    SearchFieldKind.DisplayName =>
                        new ProductAttributeSorting(ProductAttributeSorting.SortableAttribute.DisplayName, order),
                    SearchFieldKind.Brand =>
                        new ProductAttributeSorting(ProductAttributeSorting.SortableAttribute.BrandId, order),
                    SearchFieldKind.SalesPrice =>
                        new ProductAttributeSorting(ProductAttributeSorting.SortableAttribute.SalesPrice, order),
                    SearchFieldKind.Data => new ProductDataSorting(
                        field.DataKey!,
                        DataSelectionStrategy.Product,
                        order,
                        field.Numeric ? SortMode.Numerical : SortMode.Alphabetical),
                    _ => throw Unsupported(sorter.FieldName, sorter.GetType().Name)
                };
            }

            if (previous == null)
            {
                request.Sorting!.SortBy(current);
            }
            else
            {
                previous.ThenBy = current;
            }

            previous = current;
        }
    }

    private static SearchFieldDefinition SortField(Sorter sorter, Func<string, SearchFieldDefinition> resolve)
    {
        SearchFieldDefinition field = resolve(sorter.FieldName);
        bool numeric = sorter is IntegerSorter or DecimalSorter;
        bool text = sorter is KeywordSorter or TextSorter;

        if (field.Collection || field.ValueType == typeof(bool) ||
            !(numeric && field.Numeric || text && field.ValueType == typeof(string)))
        {
            throw Unsupported(sorter.FieldName, sorter.GetType().Name);
        }

        return field;
    }

    public static ContentDataFilter Equal(string key, DataValue value)
    {
        return new ContentDataFilter(key, new EqualsCondition(value));
    }

    public static NotSupportedException Unsupported(string field, string operation)
    {
        return new NotSupportedException(
            $"Query operation '{operation}' is not supported for mapped field '{field}'. " +
            "Use scalar stored values and a compatible provider binding.");
    }
}
