using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class BrowsingEndpoints
{
    internal static void MapBrowsing(this WebApplication app)
    {
        app.MapGet(
            "/store",
            (HttpContext http) => Results.LocalRedirect(DemoMarket.Current(http).StoreUrl + http.Request.QueryString));

        foreach (string operation in new[] { "suggestions", "results" })
        {
            app.MapGet(
                "/{language:regex(^(en|da)$)}/store/" + operation,
                async Task<IResult> (HttpContext http) =>
                {
                    http.Response.Headers.CacheControl = "no-store";

                    IPublishedContent? collection = null;

                    if (http.Request.Query.TryGetValue(
                            "collection",
                            out Microsoft.Extensions.Primitives.StringValues selected))
                    {
                        if (!Guid.TryParse(selected, out Guid key))
                        {
                            return Results.BadRequest(
                                new
                                {
                                    error = "Invalid collection."
                                });
                        }

                        collection = SiteContent.Home(http.RequestServices)
                            .DescendantsOrSelf()
                            .FirstOrDefault(node =>
                                node.Key == key && node.ContentType.Alias is "brand" or "productCategory" or "folder");

                        if (collection == null)
                        {
                            return Results.NotFound(
                                new
                                {
                                    error = "Unknown collection."
                                });
                        }
                    }

                    bool suggestions = operation == "suggestions";

                    return await StorefrontListing.GetAsync(http, collection, suggestions, !suggestions);
                });
        }

        foreach (string view in new[] { "store", "search", "offers" })
        {
            app.MapGet(
                "/{language:regex(^(en|da)$)}/" + view,
                async Task<IResult> (HttpContext http) =>
                {
                    if (view == "store" && http.Request.Query["provider"] == "offers")
                    {
                        return Results.LocalRedirect(
                            DemoMarket.Current(http).LanguagePath + "/offers" +
                            QueryString.Create(
                                http.Request.Query.Where(pair => pair.Key != "provider")));
                    }

                    return await StorefrontListing.GetAsync(http, view: view);
                });
        }
    }
}
