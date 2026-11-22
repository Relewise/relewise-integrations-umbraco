using System.Globalization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using Umbraco.Extensions;

internal sealed class StorefrontSearch(
    HttpRequest request,
    bool editorial,
    bool offers,
    IPublishedContent? collection = null,
    string? listingPath = null)
{
    private string ListingPath => request.PathBase + (listingPath ?? request.Path.Value);
    private bool FixedCategory => collection?.ContentType.Alias is "productCategory" or "folder";
    private bool FixedBrand => collection?.ContentType.Alias == "brand";
    private string PriceField => "shopPrice" + DemoMarket.Current(request.HttpContext).Currency;
    private string SaleField => "onSale_" + DemoMarket.Current(request.HttpContext).Currency;
    private DecimalRangeFacetRange[] PriceRanges => DemoMarket.Current(request.HttpContext).Currency == "DKK"
        ? [new("low", null, 400m), new("mid", 400m, 1200m), new("high", 1200m, null)]
        : [new("low", null, 50m), new("mid", 50m, 150m), new("high", 150m, null)];

    internal Filter[] Filters()
    {
        var filters = new List<Filter>();
        if (editorial)
        {
            return [];
        }

        if (collection != null)
        {
            if (collection.ContentType.Alias == "brand")
            {
                filters.Add(new KeywordFilter("shopBrand", [collection.Key.ToString("D")], false));
            }
            else
            {
                string[] scopeCategories = collection.DescendantsOrSelf()
                    .Where(node => node.ContentType.Alias == "productCategory")
                    .Select(node => node.Key.ToString("D"))
                    .DefaultIfEmpty(collection.Key.ToString("D"))
                    .ToArray();
                filters.Add(new KeywordFilter("shopCategory", scopeCategories, false));
            }
        }

        // An offer is an actual reduction in the active market, not a curated list of product IDs.
        if (offers || Selected("onSale", "true"))
        {
            filters.Add(new KeywordFilter(SaleField, ["true"], false));
        }

        string[] categories = request.Query["category"].Where(value => Guid.TryParse(value, out _)).OfType<string>().Select(value => Guid.Parse(value).ToString("D")).Distinct().ToArray();
        if (categories.Length > 0)
        {
            filters.Add(new KeywordFilter("shopCategory", categories, false));
        }

        string[] brands = request.Query["brand"].Where(value => Guid.TryParse(value, out _)).OfType<string>()
            .Select(value => Guid.Parse(value).ToString("D")).Distinct().ToArray();
        if (brands.Length > 0)
        {
            filters.Add(new KeywordFilter("shopBrand", brands, false));
        }

        if (request.Query["available"] == "true")
        {
            filters.Add(new KeywordFilter("inStock", ["true"], false));
        }

        bool validMinimum = TryBudget("minPrice", out decimal? minimum);
        bool validMaximum = TryBudget("maxPrice", out decimal? maximum);
        bool custom = validMinimum && validMaximum && (minimum.HasValue || maximum.HasValue);
        var ranges = PriceRanges.Where(range => !custom && request.Query["price"].Contains(range.Key))
            .Select(range => new DecimalRangeFilterRange(range.MinValue, range.MaxValue)).ToArray();
        if (ranges.Length > 0)
        {
            filters.Add(new DecimalRangeFilter(PriceField, ranges, false));
        }

        if (custom)
        {
            // Demo prices have cent precision; Umbraco's native upper bound is exclusive.
            filters.Add(new DecimalRangeFilter(PriceField, [new DecimalRangeFilterRange(minimum, maximum + 0.01m)], false));
        }
        return filters.ToArray();
    }

    internal Facet[] Facets()
    {
        if (editorial)
        {
            return [];
        }
        var facets = new List<Facet>();
        if (!FixedCategory)
        {
            facets.Add(new KeywordFacet("shopCategory"));
        }
        if (!FixedBrand)
        {
            facets.Add(new KeywordFacet("shopBrand"));
        }
        facets.Add(new KeywordFacet("inStock"));
        facets.Add(new DecimalRangeFacet(PriceField, PriceRanges));
        if (!offers)
        {
            facets.Add(new KeywordFacet(SaleField));
        }
        return facets.ToArray();
    }

    internal Sorter[] Sorters() => request.Query["sort"].ToString() switch
    {
        "price-asc" when !editorial => [new DecimalSorter(PriceField, Direction.Ascending), new ScoreSorter(Direction.Descending)],
        "price-desc" when !editorial => [new DecimalSorter(PriceField, Direction.Descending), new ScoreSorter(Direction.Descending)],
        "name-desc" => [new KeywordSorter(global::Umbraco.Cms.Core.Constants.Search.FieldNames.Name, Direction.Descending)],
        "name" => [NameSorter()],
        _ => []
    };

    private static Sorter NameSorter() => new KeywordSorter(global::Umbraco.Cms.Core.Constants.Search.FieldNames.Name, Direction.Ascending);

    internal ListingFacet[] Groups(SearchResult result, IPublishedContent home)
    {
        if (editorial)
        {
            return [];
        }
        var groups = new List<ListingFacet>();
        if (!FixedCategory)
        {
            var counts = (result.Facets.FirstOrDefault(facet => facet.FieldName == "shopCategory")?.Values.OfType<KeywordFacetValue>() ?? [])
                .ToDictionary(value => value.Key, value => value.Count);
            groups.Add(new ListingFacet("category", Storefront.Text(request.HttpContext, "Categories"),
                StorefrontPages.ShoppingCategories(home).OrderBy(node => node.Name)
                    .Select(node => new ListingChoice(node.Key.ToString("D"), node.Name,
                        counts.GetValueOrDefault(node.Key.ToString("D")), Selected("category", node.Key.ToString("D")))).ToArray()));
        }
        if (!FixedBrand)
        {
            var counts = (result.Facets.FirstOrDefault(facet => facet.FieldName == "shopBrand")?.Values.OfType<KeywordFacetValue>() ?? [])
                .ToDictionary(value => value.Key, value => value.Count);
            groups.Add(new ListingFacet("brand", Storefront.Text(request.HttpContext, "Brands"),
                StorefrontPages.ShoppingBrands(home).Select(node => new ListingChoice(node.Key.ToString("D"), StorefrontPages.Title(node),
                    counts.GetValueOrDefault(node.Key.ToString("D")), Selected("brand", node.Key.ToString("D")))).ToArray()));
        }
        groups.Add(new ListingFacet("price", Storefront.Text(request.HttpContext, "Price"),
            (result.Facets.FirstOrDefault(facet => facet.FieldName == PriceField)?.Values.OfType<DecimalRangeFacetValue>() ?? [])
                .Select(range => new ListingChoice(range.Key, PriceLabel(range.Min, range.Max), range.Count, Selected("price", range.Key))).ToArray()));
        long available = result.Facets.FirstOrDefault(facet => facet.FieldName == "inStock")?.Values
            .OfType<KeywordFacetValue>().FirstOrDefault(value => value.Key == "true")?.Count ?? 0;
        var showOnly = new List<ListingChoice>
        {
            new("true", Storefront.Text(request.HttpContext, "InStock"), available, Selected("available", "true"), "available")
        };
        if (!offers)
        {
            long onSale = result.Facets.FirstOrDefault(facet => facet.FieldName == SaleField)?.Values
                .OfType<KeywordFacetValue>().FirstOrDefault(value => value.Key == "true")?.Count ?? 0;
            showOnly.Add(new("true", Storefront.Text(request.HttpContext, "OnSale"), onSale, Selected("onSale", "true"), "onSale"));
        }
        groups.Add(new ListingFacet("showOnly", Storefront.Text(request.HttpContext, "ShowOnly"), showOnly.ToArray()));
        return groups.ToArray();
    }

    internal bool HasFilters => request.Query.Any(pair => pair.Key is "category" or "brand" or "price" or "available" or "onSale" or "minPrice" or "maxPrice" && pair.Value.Any(value => !string.IsNullOrWhiteSpace(value)));

    internal ListingChip[] Chips(IPublishedContent home)
    {
        var chips = new List<ListingChip>();
        void Add(string name, string value, string label) => chips.Add(new ListingChip(label, RemoveValueUrl(name, value)));
        foreach (var category in StorefrontPages.ShoppingCategories(home).OrderBy(node => node.Name))
        {
            string key = category.Key.ToString("D");
            if (Selected("category", key))
            {
                Add("category", key, category.Name);
            }
        }
        foreach (var brand in StorefrontPages.ShoppingBrands(home))
        {
            string key = brand.Key.ToString("D");
            if (Selected("brand", key))
            {
                Add("brand", key, StorefrontPages.Title(brand));
            }
        }
        foreach (var range in PriceRanges.Where(range => Selected("price", range.Key)))
        {
            Add("price", range.Key, PriceLabel(range.MinValue, range.MaxValue));
        }
        if (TryBudget("minPrice", out decimal? minimum) && TryBudget("maxPrice", out decimal? maximum) && (minimum.HasValue || maximum.HasValue))
        {
            var market = DemoMarket.Current(request.HttpContext);
            string Amount(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo(market.Culture));
            string label = market.Currency + " " + (minimum.HasValue && maximum.HasValue ? Amount(minimum.Value) + "–" + Amount(maximum.Value)
                : minimum.HasValue ? Amount(minimum.Value) + "+" : "≤ " + Amount(maximum!.Value));
            var query = request.Query.Where(pair => pair.Key is not ("minPrice" or "maxPrice" or "page" or "collection" or "view"));
            chips.Add(new ListingChip(label, ListingPath + QueryString.Create(query)));
        }
        if (Selected("available", "true"))
        {
            Add("available", "true", Storefront.Text(request.HttpContext, "InStock"));
        }
        if (!offers && Selected("onSale", "true"))
        {
            Add("onSale", "true", Storefront.Text(request.HttpContext, "OnSale"));
        }
        return chips.ToArray();
    }

    private string PriceLabel(decimal? min, decimal? max)
    {
        var market = DemoMarket.Current(request.HttpContext);
        string Amount(decimal? value) => market.Currency + " " + value?.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo(market.Culture));
        return min == null ? Storefront.Text(request.HttpContext, "UnderPrice", Amount(max))
            : max == null ? Amount(min) + "+" : Amount(min) + "–" + max.Value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo(market.Culture));
    }

    private string RemoveValueUrl(string name, string value)
    {
        var query = request.Query.Where(pair => pair.Key is not ("page" or "collection" or "view"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        string[] remaining = request.Query[name].OfType<string>()
            .Where(item => !string.Equals(item, value, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (remaining.Length == 0)
        {
            query.Remove(name);
        }
        else
        {
            query[name] = remaining;
        }
        return ListingPath + QueryString.Create(query);
    }

    internal string ClearQueryUrl() => ListingPath + QueryString.Create(request.Query.Where(pair => pair.Key is not ("page" or "q" or "collection" or "view")));

    private bool Selected(string name, string value) => request.Query[name].Any(selected => string.Equals(selected, value, StringComparison.OrdinalIgnoreCase));

    internal bool IsValid(IPublishedContent home)
    {
        string[] sorts = editorial ? ["", "name", "name-desc"] : ["", "name", "name-desc", "price-asc", "price-desc"];
        if (!TryBudget("minPrice", out decimal? minimum) || !TryBudget("maxPrice", out decimal? maximum) ||
            minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value ||
            (minimum.HasValue || maximum.HasValue) && request.Query["price"].Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            return false;
        }
        if (!sorts.Contains(request.Query["sort"].ToString()) ||
            request.Query["available"].Any(value => value != "true") ||
            request.Query["onSale"].Any(value => value != "true") ||
            request.Query["price"].Any(value => !PriceRanges.Any(range => range.Key == value)))
        {
            return false;
        }
        var categories = StorefrontPages.ShoppingCategories(home).Select(node => node.Key).ToHashSet();
        var brands = StorefrontPages.ShoppingBrands(home).Select(node => node.Key).ToHashSet();
        return (!FixedCategory || !request.Query.ContainsKey("category")) && request.Query["category"].All(value => Guid.TryParse(value, out Guid key) && categories.Contains(key)) &&
            (!FixedBrand || !request.Query.ContainsKey("brand")) && request.Query["brand"].All(value => Guid.TryParse(value, out Guid key) && brands.Contains(key)) &&
            (!editorial || !request.Query.Keys.Any(key => key is "price" or "category" or "brand" or "available" or "onSale"));
    }

    private bool TryBudget(string name, out decimal? amount)
    {
        amount = null;
        var values = request.Query[name];
        if (values.Count == 0 || values.Count == 1 && string.IsNullOrWhiteSpace(values[0]))
        {
            return true;
        }
        if (values.Count != 1 || !decimal.TryParse(values[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed) || parsed < 0 || parsed > 1000000m || decimal.Round(parsed, 2) != parsed)
        {
            return false;
        }
        amount = parsed;
        return true;
    }

    internal string ResetUrl()
    {
        var query = request.Query.Where(value => value.Key is not ("category" or "brand" or "price" or "available" or "onSale" or "minPrice" or "maxPrice" or "page" or "collection" or "view"));
        return ListingPath + QueryString.Create(query);
    }
}
