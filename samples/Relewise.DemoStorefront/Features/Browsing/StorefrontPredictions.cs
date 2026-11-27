using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Responses.Search;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using static Relewise.DemoStorefront.DemoConstants;
using ISearcher = Relewise.Client.Search.ISearcher;

namespace Relewise.DemoStorefront.Features.Browsing;

// The site-wide search box needs predictions, not a results/count search first.
internal static class StorefrontPredictions
{
    public static async Task<IResult> GetAsync(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (DemoConnection.RequireConnection(http, true) is { } unavailable)
        {
            return unavailable;
        }

        string term = http.Request.Query["q"].ToString().Trim();

        if (term.Length is < 2 or > 80)
        {
            return Results.Json(Array.Empty<string>());
        }

        DemoMarket market = DemoMarket.Current(http);
        bool editorial = http.Request.Query["type"] == "article";
        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();
        var request = new SearchTermPredictionRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            user,
            "Storefront search suggestions",
            term,
            5)
        {
            Settings = new SearchTermPredictionSettings
            {
                TargetEntityTypes = [editorial ? EntityType.Content : EntityType.Product]
            }
        };

        Filter Data(string key, ValueCondition condition)
        {
            return editorial ? new ContentDataFilter(key, condition) : new ProductDataFilter(key, condition);
        }

        string[] aliases = editorial
            ? [DocumentTypeAliases.Article, DocumentTypeAliases.LocalizedArticle, DocumentTypeAliases.Guide]
            : [DocumentTypeAliases.Product];
        request.Filters = new FilterCollection(
            new OrFilter([.. aliases.Select(alias => Data(RelewiseDataKeys.DocumentTypeAlias, new EqualsCondition(alias)))]),
            Data(RelewiseDataKeys.Published, new EqualsCondition(true)),
            Data(RelewiseDataKeys.Search.Protected, new EqualsCondition(false)),
            new OrFilter(
            [
                Data(RelewiseDataKeys.Search.Culture, new ContainsCondition(market.Culture.ToLowerInvariant())),
                Data(RelewiseDataKeys.Search.Culture, new ContainsCondition(string.Empty))
            ]));

        var client = http.RequestServices.GetRequiredService<IRelewiseClientFactory>().GetClient<ISearcher>();
        SearchTermPredictionResponse response = await client.PredictAsync(request, http.RequestAborted);
        string[] terms = [.. (response.Predictions ?? [])
            .Select(prediction => prediction.Term)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)];

        return Results.Json(terms);
    }
}
