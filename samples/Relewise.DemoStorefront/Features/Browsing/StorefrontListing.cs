using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.DemoConstants;
using static Relewise.DemoStorefront.Site.Storefront;

namespace Relewise.DemoStorefront.Features.Browsing;

// Shop, category and Brand pages share one query path through Umbraco's search abstraction.
internal static class StorefrontListing
{
    internal static async Task<StorefrontResponse> GetAsync(
        HttpContext http,
        IPublishedContent? collection = null,
        bool suggestionsOnly = false,
        bool dataOnly = false,
        string? view = null)
    {
        HttpRequest request = http.Request;

        view ??= request.Query["view"].FirstOrDefault() ?? "store";

        if (view is not ("store" or "search" or "offers"))
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }

        bool searchPage = view == "search";
        bool offersPage = view == "offers";

        if (!DemoConnection.SearchConfigured(http))
        {
            string[] searchParameters =
                ["q", "category", "brand", "price", "available", "onSale", "minPrice", "maxPrice", "sort", "page"];

            if (DemoConnection.IsConfigured(http) || dataOnly || suggestionsOnly || searchPage || offersPage ||
                searchParameters.Any(request.Query.ContainsKey))
            {
                return DemoConnection.Unavailable(http, dataOnly || suggestionsOnly);
            }

            return new StorefrontResponse(ListingPage.Preview(http, collection));
        }

        if (suggestionsOnly && request.Query["q"].ToString().Trim().Length is < 2 or > 80)
        {
            return new StorefrontResponse(Json: "[]");
        }

        IServiceProvider services = http.RequestServices;
        var searchers = services.GetRequiredService<ISearcherResolver>();
        var contexts = services.GetRequiredService<IUmbracoContextFactory>();

        string requestedType =
            !offersPage && request.Query["type"].FirstOrDefault() == "article" ? "article" : "product";
        string alias = offersPage
            ? SearchProviders.Offers
            : requestedType == "article" ? SearchProviders.Articles : SearchProviders.Catalogue;
        bool offers = offersPage;

        string? query = request.Query["q"];

        DemoMarket market = DemoMarket.Current(request.HttpContext);
        string culture = market.Culture;

        services.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(culture);

        const int pageSize = 24;
        var page = 1;

        if (request.Query.ContainsKey("page") &&
            (!int.TryParse(request.Query["page"], out page) || page < 1 || page > int.MaxValue / pageSize))
        {
            return Redirect(http, PageUrl(request, 1));
        }

        ISearcher? searcher = searchers.GetSearcher(alias);

        if (searcher == null)
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }

        bool editorial = requestedType == "article";
        string listingPath = collection?.Url(culture) ??
                             (view == "store" ? market.StoreUrl : market.LanguagePath + "/" + view);
        var browsing = new StorefrontSearch(request, editorial, offers, collection, listingPath);
        IPublishedContent home = SiteContent.Home(services);

        if ((collection != null && editorial) || !browsing.IsValid(home))
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }

        SearchResult result = await searcher.SearchAsync(
            alias,
            query,
            browsing.Filters(),
            suggestionsOnly ? [] : browsing.Facets(),
            suggestionsOnly ? [] : browsing.Sorters(),
            culture,
            skip: suggestionsOnly ? 0 : (page - 1) * pageSize,
            take: suggestionsOnly ? 0 : pageSize,
            maxSuggestions: suggestionsOnly ? 5 : 0
        );

        if (suggestionsOnly)
        {
            return new StorefrontResponse(Json: JsonSerializer.Serialize(result.Suggestions ?? []));
        }

        long lastPage = Math.Max(1, (result.Total - 1) / pageSize + 1);

        if (page > lastPage)
        {
            return Redirect(http, PageUrl(request, lastPage));
        }

        string heading = Text(
            http,
            searchPage ? "SearchResults" : offers ? "OffersNavigation" : editorial ? "Journal" : "Collection");

        if (collection != null)
        {
            heading = collection.Value<string>(PropertyAliases.Heading) ?? collection.Name;
        }

        using UmbracoContextReference context = contexts.EnsureUmbracoContext();
        IPublishedContentCache cache = context.UmbracoContext.Content;

        ListingCard[] cards =
        [
            .. result.Documents.Select(document => cache.GetById(document.Id))
                .Where(content => content != null)
                .Select(content => ListingCards.CardData(content!, http))
        ];

        string[] labelKeys =
        [
            "Filters", "FiltersShort", "FilterAndSort", "SortBy", "ClearFilters", "SearchProvider", "Search",
            "SearchProducts", "SearchJournal",
            "SearchPlaceholder", "EditorialSearchPlaceholder", "UpdatingResults", "ResultsUpdated",
            "ListingUpdateFailed", "TryAgain",
            "CustomPriceRange", "MinimumPrice", "MaximumPrice", "PriceRangeInvalid", "AppliedFilters", "RemoveFilter",
            "Browse", "NoMatchesHeading", "NoMatchesHelp", "BrowseWithoutQuery", "Previous", "Next",
            "Pagination", "Equipment", "FieldNotes", "ReadStory", "OnOffer", "Updating", "Recommended",
            "RecommendedRelewise", "NameAscending", "NameDescending", "PriceAscending", "PriceDescending", "Products",
            "Journal", "SearchResults"
        ];

        string[] queryKeys =
            ["type", "q", "sort", "page", "category", "brand", "price", "available", "onSale", "minPrice", "maxPrice"];
        Dictionary<string, string[]> parameters = request.Query.Where(pair =>
                queryKeys.Contains(pair.Key) && (!offers || pair.Key != "onSale") &&
                (!offersPage || pair.Key != "type") && pair.Value.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.OfType<string>()
                    .ToArray());

        string PageLink(int target)
        {
            Dictionary<string, StringValues> values = parameters.ToDictionary(
                pair => pair.Key,
                pair => new StringValues(pair.Value));

            values.Remove("page");

            if (target > 1)
            {
                values["page"] = target.ToString(CultureInfo.InvariantCulture);
            }

            return listingPath + QueryString.Create(values);
        }

        var model = new ListingModel
        {
            Path = listingPath,
            View = view,
            SearchPage = searchPage,
            Endpoint = market.LanguagePath + "/store/results",
            SuggestionsEndpoint = market.LanguagePath + "/store/suggestions",
            Collection = collection?.Key,
            Editorial = editorial,
            Currency = market.Currency,
            Provider = "Relewise",
            Parameters = parameters,
            Labels = labelKeys.ToDictionary(key => key, key => Text(http, key)),
            Facets = browsing.Groups(result, home),
            Chips = browsing.Chips(home),
            Cards = cards,
            Heading = string.IsNullOrWhiteSpace(query) ? Text(http, "Browse") : Text(http, "ResultsFor", query),
            ResultLabel = Text(http, result.Total == 1 ? "Result" : "Results", result.Total),
            RangeLabel = cards.Length == 0
                ? Text(http, "Results", 0)
                : Text(
                    http,
                    "ShowingRange",
                    (page - 1L) * pageSize + 1,
                    (page - 1L) * pageSize + cards.Length,
                    result.Total
                ),
            ShowResultsLabel = Text(http, result.Total == 1 ? "ShowProduct" : "ShowProducts", result.Total),
            Total = result.Total,
            Page = page,
            LastPage = lastPage,
            PageLabel = Text(http, "Page", page, lastPage),
            Updating = cards.Length < result.Documents.Count(),
            ResetUrl = browsing.ResetUrl(),
            ClearQueryUrl = browsing.ClearQueryUrl(),
            PreviousUrl = page > 1 ? PageLink(page - 1) : null,
            NextUrl = page < lastPage ? PageLink(page + 1) : null
        };

        await StorefrontNavigation.RememberListingAsync(
            http,
            collection?.Key,
            view,
            QueryString.Create(
                parameters.Select(pair =>
                    new KeyValuePair<string, StringValues>(pair.Key, pair.Value))));

        if (dataOnly)
        {
            return new StorefrontResponse(Json: JsonSerializer.Serialize(model, StorefrontChrome.Json));
        }

        return new StorefrontResponse(ListingPage.Create(http, collection, model, heading));
    }

    private static StorefrontResponse Invalid(HttpContext http, string message, bool json)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;

        if (json)
        {
            return new StorefrontResponse(Json: JsonSerializer.Serialize(new { error = message }));
        }

        return new StorefrontResponse(
            new StorefrontPage(
                Text(http, "Search"),
                "shop",
                "/Views/Shared/Message.cshtml",
                new StorefrontMessage(message, Empty: true)));
    }

    private static StorefrontResponse Redirect(HttpContext http, string url)
    {
        http.Response.Redirect(url);

        return new StorefrontResponse();
    }

    private static string PageUrl(
        HttpRequest request,
        long page)
    {
        Dictionary<string, StringValues> query = request.Query.ToDictionary(
            value => value.Key,
            value => value.Value,
            StringComparer.OrdinalIgnoreCase);

        if (page == 1)
        {
            query.Remove("page");
        }
        else
        {
            query["page"] = page.ToString(CultureInfo.InvariantCulture);
        }

        return request.PathBase + request.Path + QueryString.Create(query);
    }
}
