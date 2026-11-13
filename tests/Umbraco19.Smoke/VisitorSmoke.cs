using System.Net;
using System.Text;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

internal static class VisitorSmoke
{
    public static void MapVisitorSmoke(this WebApplication app)
    {
        app.MapGet("/smoke/search", async (HttpRequest request, ISearcherResolver searchers, IUmbracoContextFactory contexts) =>
        {
            string? query = request.Query["q"];
            string alias = Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
            var searcher = searchers.GetSearcher(alias) ?? throw new InvalidOperationException("No published searcher");
            var results = await searcher.SearchAsync(alias, query);
            using var context = contexts.EnsureUmbracoContext();
            var html = new StringBuilder($"<h1>Visitor search</h1><form><label>Search <input name=\"q\" value=\"{Encode(query)}\"></label><button>Search</button></form><p>Remote matches: {results.Total}</p><ul>");
            foreach (var result in results.Documents)
            {
                var page = context.UmbracoContext.Content?.GetById(result.Id);
                string url = page is null ? $"/smoke/page/{result.Id}" : page.Url();
                html.Append($"<li data-key=\"{result.Id}\"><a href=\"{Encode(url)}\">{Encode(page?.Name ?? "[not published]")}</a> ({result.Id})</li>");
            }
            html.Append("</ul>");
            return Results.Content(Layout(html.ToString()), "text/html");
        });
        app.MapGet("/smoke/page/{key:guid}", (Guid key, IUmbracoContextFactory contexts) =>
        {
            using var context = contexts.EnsureUmbracoContext();
            var page = context.UmbracoContext.Content?.GetById(key);
            return page is null ? Results.NotFound() : Results.Content(Layout($"<h1>{Encode(page.Name)}</h1><p>{Encode(page.GetProperty("bodyText")?.GetValue()?.ToString())}</p><a href=\"/smoke/search\">Search</a>"), "text/html");
        });
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Layout(string body) => $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Umbraco 19 visitor smoke</title></head><body>{body}</body></html>";
}
