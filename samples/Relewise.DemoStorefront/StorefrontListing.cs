using System.Text;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using static Storefront;

// Shop, category and Brand pages share one query path through Umbraco's search abstraction.
internal static class StorefrontListing
{
    internal static async Task<string> RenderAsync(HttpContext http, IPublishedContent? collection = null, bool suggestionsOnly = false, bool dataOnly = false, string? view = null)
    {
        var request = http.Request;
        view ??= request.Query["view"].FirstOrDefault() ?? "store";
        if (view is not ("store" or "search" or "offers"))
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }
        bool searchPage = view == "search";
        bool offersPage = view == "offers";
        if (!DemoConnection.SearchConfigured(http))
        {
            string[] searchParameters = ["q", "provider", "category", "brand", "price", "available", "onSale", "minPrice", "maxPrice", "sort", "page"];
            if (DemoConnection.IsConfigured(http) || dataOnly || suggestionsOnly || searchPage || offersPage || searchParameters.Any(request.Query.ContainsKey))
            {
                return DemoConnection.Unavailable(http, dataOnly || suggestionsOnly);
            }
            return Preview(http, collection);
        }
        if (suggestionsOnly && (request.Query["q"].ToString().Trim().Length is < 2 or > 80))
        {
            return "[]";
        }
        var services = http.RequestServices;
        var searchers = services.GetRequiredService<ISearcherResolver>();
        var contexts = services.GetRequiredService<IUmbracoContextFactory>();
        string requestedType = request.Query["type"].FirstOrDefault() == "article" ? "article" : "product";
        string alias = offersPage ? "offers" : request.Query["provider"].FirstOrDefault() ?? (requestedType == "article" ? "articles" : "catalogue");
        if (!new[] { "catalogue", "articles", "guides", "allEditorial", "localized", "offers", "commerceCatalogue", Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent }.Contains(alias))
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }

        bool offers = offersPage || alias == "offers";
        string requestedProvider = alias;
        string? query = request.Query["q"];

        var market = DemoMarket.Current(request.HttpContext);
        string culture = market.Culture;
        services.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(culture);
        string T(string key, params object[] values) => E(Text(request.HttpContext, key, values));

        const int pageSize = 24;
        int page = 1;
        if (request.Query.ContainsKey("page") &&
            (!int.TryParse(request.Query["page"], out page) || page < 1 || page > int.MaxValue / pageSize))
        {
            return Redirect(http, PageUrl(request, 1));
        }
        var settings = services.GetRequiredService<IConfiguration>();
        if (alias == Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent && settings.GetValue<bool?>("Demo:DefaultSearch") == false)
        {
            return DemoConnection.Unavailable(http, dataOnly || suggestionsOnly);
        }
        var searcher = searchers.GetSearcher(alias);
        if (searcher == null)
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }

        string effectiveProvider = alias == Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent
            ? settings["Demo:DefaultProvider"] ?? "articles" : requestedProvider;
        bool editorial = effectiveProvider is "articles" or "guides" or "allEditorial" or "localized" ||
            (effectiveProvider == Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent && requestedType == "article");
        string listingPath = collection?.Url(culture) ?? (view == "store" ? market.StoreUrl : market.LanguagePath + "/" + view);
        var browsing = new StorefrontSearch(request, editorial, offers, collection, listingPath);
        var home = StorefrontPages.Home(services);
        if ((collection != null && editorial) || !browsing.IsValid(home))
        {
            return Invalid(http, Text(http, "InvalidSearch"), dataOnly || suggestionsOnly);
        }
        var result = await searcher.SearchAsync(
            alias,
            query,
            filters: browsing.Filters(),
            facets: suggestionsOnly ? [] : browsing.Facets(),
            sorters: suggestionsOnly ? [] : browsing.Sorters(),
            culture: culture,
            skip: suggestionsOnly ? 0 : (page - 1) * pageSize,
            take: suggestionsOnly ? 0 : pageSize,
            maxSuggestions: suggestionsOnly ? 5 : 0);
        if (suggestionsOnly)
        {
            return System.Text.Json.JsonSerializer.Serialize(result.Suggestions ?? []);
        }
        long lastPage = Math.Max(1, (result.Total - 1) / pageSize + 1);
        if (page > lastPage)
        {
            return Redirect(http, PageUrl(request, lastPage));
        }

        string heading = searchPage ? Text(http, "SearchResults") : offers ? Text(request.HttpContext, "OffersNavigation") : alias switch
        {
            "catalogue" or "commerceCatalogue" => Text(request.HttpContext, "Collection"),
            "articles" => Text(request.HttpContext, "Journal"),
            "guides" => Text(request.HttpContext, "Guides"),
            "allEditorial" => Text(request.HttpContext, "Editorial"),
            "localized" => Text(request.HttpContext, "Localized"),
            "offers" => Text(request.HttpContext, "Offers"),
            _ => Text(request.HttpContext, editorial ? "Journal" : "Collection")
        };
        if (collection != null)
        {
            heading = collection.Value<string>("heading") ?? collection.Name;
        }
        using var context = contexts.EnsureUmbracoContext();
        var cards = result.Documents.Select(document => context.UmbracoContext.Content?.GetById(document.Id))
            .Where(content => content != null).Select(content => StorefrontPages.CardData(content!, http)).ToArray();
        string[] labelKeys = ["Filters", "FiltersShort", "FilterAndSort", "SortBy", "ClearFilters", "SearchProvider", "Search", "SearchProducts", "SearchJournal",
            "SearchPlaceholder", "EditorialSearchPlaceholder", "UpdatingResults", "ResultsUpdated", "ListingUpdateFailed", "TryAgain",
            "CustomPriceRange", "MinimumPrice", "MaximumPrice", "PriceRangeInvalid", "AppliedFilters", "RemoveFilter", "Browse", "NoMatchesHeading", "NoMatchesHelp", "BrowseWithoutQuery", "Previous", "Next",
            "Pagination", "Equipment", "FieldNotes", "ReadStory", "OnOffer", "Updating", "Recommended", "RecommendedRelewise", "NameAscending", "NameDescending", "PriceAscending", "PriceDescending", "Products", "Journal", "SearchResults"];
        string[] queryKeys = ["type", "provider", "q", "sort", "page", "category", "brand", "price", "available", "onSale", "minPrice", "maxPrice"];
        var parameters = request.Query.Where(pair => queryKeys.Contains(pair.Key) && (!offers || pair.Key != "onSale") && (!offersPage || pair.Key is not ("provider" or "type")) && pair.Value.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToDictionary(pair => pair.Key, pair => pair.Value.OfType<string>().ToArray());
        string PageLink(int target)
        {
            var values = parameters.ToDictionary(pair => pair.Key, pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value));
            values.Remove("page");
            if (target > 1)
            {
                values["page"] = target.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
            RangeLabel = cards.Length == 0 ? Text(http, "Results", 0) : Text(http, "ShowingRange", (page - 1L) * pageSize + 1, (page - 1L) * pageSize + cards.Length, result.Total),
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
        await StorefrontNavigation.RememberListingAsync(http, collection?.Key, view,
            QueryString.Create(parameters.Select(pair => new KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>(pair.Key, pair.Value))));
        string json = System.Text.Json.JsonSerializer.Serialize(model, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (dataOnly)
        {
            return json;
        }
        var body = new StringBuilder(StorefrontPages.Breadcrumb(collection, http, searchPage ? "search" : offers ? "offers" : editorial ? "journal" : "shop", searchPage || offers ? heading : null));
        body.Append($"<section class='listing-heading'><h1>{E(heading)}</h1></section>");
        if (collection != null)
        {
            body.Append(StorefrontPages.Links(collection.Children().Where(node => node.ContentType.Alias is "productCategory" or "folder"), http));
        }
        // Default JSON escaping protects the script boundary even when CMS values contain markup.
        body.Append($"<div id='storefront-listing'><p role='status'>{T("UpdatingResults")}</p></div><script id='listing-data' type='application/json'>{json}</script>");
        http.Items["Fieldwork:HasSearch"] = true;
        http.Items["Fieldwork:SearchProvider"] = "Relewise";
        return Layout(heading, body.ToString(), true, http, searchPage ? "search" : offers ? "offers" : editorial ? "journal" : "shop");
    }

    private static string Preview(HttpContext http, IPublishedContent? collection)
    {
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(DemoMarket.Current(http).Culture);
        using var context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        var home = StorefrontPages.Home(http.RequestServices);
        bool editorial = http.Request.Query["type"] == "article";
        string heading = collection == null ? Text(http, editorial ? "Journal" : "Collection") : StorefrontPages.Title(collection);
        IEnumerable<IPublishedContent> pages = (collection?.ContentType.Alias == "brand" ? home : collection ?? home).Descendants()
            .Where(node => editorial ? node.ContentType.Alias is "article" or "guide" or "localizedArticle" : node.ContentType.Alias == "product");
        if (collection?.ContentType.Alias == "brand")
        {
            pages = pages.Where(node => node.Value<IPublishedContent>("brand")?.Key == collection.Key);
        }
        var access = http.RequestServices.GetRequiredService<Umbraco.Cms.Core.Services.IPublicAccessService>();
        pages = pages.Where(node => !access.IsProtected(node.Path).Success);
        string body = StorefrontPages.Breadcrumb(collection, http, editorial ? "journal" : "shop") +
            $"<h1>{E(heading)}</h1><p>{E(Text(http, "ContentPreview"))}</p><div class='grid'>" +
            string.Concat(pages.OrderBy(StorefrontPages.Title).Take(6).Select(page => StorefrontPages.Card(page, http))) + "</div>";
        return Layout(heading, body, false, http, editorial ? "journal" : "shop");
    }

    private static string Invalid(HttpContext http, string message, bool json)
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        if (json)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { error = message });
        }
        return Layout(Text(http, "Search"), "<p class='empty'>" + E(message) + "</p>", false, http, "shop");
    }

    private static string Redirect(HttpContext http, string url)
    {
        http.Response.Redirect(url);
        return string.Empty;
    }
}
