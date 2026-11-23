using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using RFilter = Relewise.Client.Requests.Filters.Filter;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;
using UFacetResult = Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult;
using UFilter = Umbraco.Cms.Core.Search.Querying.Filtering.Filter;

namespace Relewise.Integrations.Umbraco.Search;

// Prices stay in the SDK's MultiCurrency model; the core field is only a query binding.
internal sealed class ProductPriceQuery
{
    private readonly string _fieldName;
    private readonly DecimalRangeFilter[] _filters;
    private readonly DecimalRangeFacet? _facet;
    private readonly bool _hasSorting;

    internal ProductPriceQuery(string fieldName, UFilter[] filters, UFacet[] facets, Sorter[] sorters)
    {
        _fieldName = fieldName;
        UFilter[] selectedFilters = [.. filters.Where(filter => Matches(filter.FieldName))];
        UFacet[] selectedFacets = [.. facets.Where(facet => Matches(facet.FieldName))];
        Sorter[] selectedSorters = [.. sorters.Where(sorter => Matches(sorter.FieldName))];

        if (selectedFilters.Any(filter => filter is not DecimalRangeFilter) ||
            selectedFacets.Length > 1 || selectedFacets.Any(facet => facet is not DecimalRangeFacet) ||
            selectedSorters.Any(sorter => sorter is not DecimalSorter))
        {
            throw new NotSupportedException(
                "SalesPrice bindings support DecimalRangeFilter, one DecimalRangeFacet and DecimalSorter only.");
        }

        _filters = [.. selectedFilters.Cast<DecimalRangeFilter>()];
        _facet = selectedFacets.Cast<DecimalRangeFacet>().SingleOrDefault();
        _hasSorting = selectedSorters.Length > 0;

        if (_facet != null)
        {
            ValidateRanges(_facet);
        }
    }

    internal bool Matches(string? fieldName)
    {
        return fieldName == _fieldName;
    }

    internal void Apply(ProductSearchRequest request)
    {
        if (_filters.Length == 0 && _facet == null && !_hasSorting)
        {
            return;
        }

        Currency currency = RequireCurrency(request.Currency);
        bool useSelection = _facet?.Ranges.Any() == true && _filters is [{ Negate: false } selection] &&
                            selection.Ranges.Any() && selection.Ranges.All(range =>
                                !range.MinValue.HasValue || !range.MaxValue.HasValue ||
                                range.MinValue < range.MaxValue);

        request.Filters ??= new FilterCollection();

        if (!useSelection)
        {
            foreach (DecimalRangeFilter filter in _filters)
            {
                request.Filters.Add(MapFilter(filter, currency));
            }
        }

        if (_facet?.Ranges.Any() == true)
        {
            List<ChainableRange<decimal?>> ranges =
                [.. _facet.Ranges.Select(range => new ChainableRange<decimal?>(range.MinValue, range.MaxValue))];
            List<ChainableRange<decimal?>>? selected = useSelection
                ? [.. _filters[0].Ranges.Select(range => new ChainableRange<decimal?>(range.MinValue, range.MaxValue))]
                : null;

            request.Facets ??= new ProductFacetQuery();
            request.Facets.Items.RemoveAll(facet => facet.Field == FacetingField.SalesPrice);
            request.Facets.Items.Add(new PriceRangesFacet(
                FacetingField.SalesPrice, PriceSelectionStrategy.Product, ranges, null, selected));
        }
    }

    internal RFilter MapFilter(UFilter filter, Currency? currency)
    {
        var price = (DecimalRangeFilter)filter;
        RFilter[] ranges = [.. price.Ranges.Select(range => MapRange(range, RequireCurrency(currency)))];

        if (ranges.Length == 0)
        {
            var exists = new ProductSalesPriceFilter(new Range<decimal?>(null, null), RequireCurrency(currency));

            return new AndFilter([exists, new AndFilter([exists], true)], price.Negate);
        }

        return new OrFilter(ranges, price.Negate);
    }

    private static RFilter MapRange(DecimalRangeFilterRange range, Currency currency)
    {
        var lower = new ProductSalesPriceFilter(new Range<decimal?>(range.MinValue, null), currency);

        if (!range.MaxValue.HasValue)
        {
            return lower;
        }

        // SDK price filters have inclusive endpoints. Negating [max, infinity) preserves
        // Umbraco's exclusive upper bound without assuming cent precision or rounding.
        var upper = new ProductSalesPriceFilter(new Range<decimal?>(range.MaxValue, null), currency, true);

        return new AndFilter([lower, upper]);
    }

    internal UFacetResult Read(ContentFacetResult? response)
    {
        PriceRangesFacetResult? result = response?.Items.OfType<PriceRangesFacetResult>()
            .FirstOrDefault(facet => facet.Field == FacetingField.SalesPrice &&
                                     facet.PriceSelectionStrategy == PriceSelectionStrategy.Product);
        if (_facet?.Ranges.Any() == true && result == null)
        {
            throw new InvalidOperationException("Relewise did not return the requested SalesPrice facet.");
        }

        var values = new List<FacetValue>();

        foreach (DecimalRangeFacetRange range in _facet?.Ranges ?? [])
        {
            long count = result?.Available.FirstOrDefault(value =>
                value.Value.LowerBoundInclusive == range.MinValue &&
                value.Value.UpperBoundExclusive == range.MaxValue)?.Hits ?? 0;
            values.Add(new DecimalRangeFacetValue(range.Key, range.MinValue, range.MaxValue, count));
        }

        return new UFacetResult(_fieldName, values);
    }

    private static Currency RequireCurrency(Currency? currency)
    {
        return currency ?? throw new InvalidOperationException(
            "SalesPrice queries require a currency. Set request.Currency in ConfigureRequest().");
    }

    private static void ValidateRanges(DecimalRangeFacet facet)
    {
        DecimalRangeFacetRange[] ranges = [.. facet.Ranges.OrderBy(range => range.MinValue ?? decimal.MinValue)];

        for (var i = 0; i < ranges.Length; i++)
        {
            DecimalRangeFacetRange range = ranges[i];

            if (range.MinValue >= range.MaxValue ||
                (i > 0 && (ranges[i - 1].MaxValue ?? decimal.MaxValue) > (range.MinValue ?? decimal.MinValue)))
            {
                throw new NotSupportedException("Price facet ranges must be nonempty and nonoverlapping.");
            }
        }
    }
}
