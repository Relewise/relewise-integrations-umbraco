using System.Net;
using System.Text;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

public static class Storefront
{
    internal static void MapStorefront(this WebApplication app)
    {
        app.MapGet("/store", async (HttpRequest request, ISearcherResolver searchers, IUmbracoContextFactory contexts, IServiceProvider services, IContentTypeService types) =>
        {
            bool connected = searchers.GetSearcher("catalogue") != null;
            string requestedType = request.Query["type"].FirstOrDefault() == "article" ? "article" : "product";
            string alias = request.Query["provider"].FirstOrDefault() ?? (connected ? (requestedType == "article" ? "articles" : "catalogue") : Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent);
            if (!new[] { "catalogue", "articles", "guides", "allEditorial", "localized", "offers", "commerceCatalogue", Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent }.Contains(alias))
            {
                return Results.BadRequest();
            }

            string? query = request.Query["q"];
            if (searchers.GetSearcher("catalogue") == null && !request.Query.ContainsKey("provider"))
            {
                alias = Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
            }

            string? culture = request.Query["culture"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(culture))
            {
                culture = null;
            }

            int page = int.TryParse(request.Query["page"], out var number) ? Math.Max(1, number) : 1;
            if (page > int.MaxValue / 6)
            {
                return Results.BadRequest("Page is outside the supported range.");
            }
            if (!connected)
            {
                alias = Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
            }

            var searcher = searchers.GetSearcher(alias);
            if (searcher == null)
            {
                return Results.BadRequest("Search provider is not registered in this demo configuration.");
            }

            var localFilters = !connected ? new[] { new KeywordFilter(Umbraco.Cms.Core.Constants.Search.FieldNames.ContentTypeId, [types.Get(requestedType)!.Key.AsKeyword()], false) } : null;
            var result = await searcher.SearchAsync(alias, query ?? (!connected ? "*" : null), filters: localFilters, culture: culture, skip: (page - 1) * 6, take: 6);
            using var context = contexts.EnsureUmbracoContext();
            var body = new StringBuilder($"<section class='hero'><p class='eyebrow'>UMBRACO 19 · LOCAL STOREFRONT</p><h1>Made for the everyday outdoors.</h1><p>Explore the collection, or find your next adventure in our journal.</p></section><form class='search'><label for='q'>Search the site</label><div><input id='q' name='q' value='{E(query)}' placeholder='Try Trail, flask or camp'><select aria-label='Search provider' name='provider'>");
            foreach (var choice in new[] { ("catalogue", "Products"), ("articles", "Journal"), ("guides", "Guides"), ("allEditorial", "All editorial"), ("localized", "Multilingual journal"), ("offers", "Offers"), ("commerceCatalogue", "Commerce catalogue"), (Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent, "Ordinary Umbraco search") })
            {
                if (searchers.GetSearcher(choice.Item1) != null)
                {
                    body.Append($"<option value='{choice.Item1}' {(alias == choice.Item1 ? "selected" : "")}>{choice.Item2}</option>");
                }
            }

            body.Append("</select><select aria-label='Language' name='culture'>");
            foreach (var language in new[] { ("", "Invariant"), ("en-US", "English"), ("da-DK", "Dansk") })
            {
                body.Append($"<option value='{language.Item1}' {(culture == language.Item1 || culture == null && language.Item1 == "" ? "selected" : "")}>{language.Item2}</option>");
            }

            body.Append($"</select><button>Search</button></div></form><section class='result-heading'><h2>{(alias == "articles" ? "Journal" : alias == "offers" ? "Offers" : alias == "catalogue" ? "The collection" : "Published content")}</h2><span>{result.Total} results · provider: {E(alias)}</span></section><section class='grid'>");
            int rendered = 0;
            foreach (var document in result.Documents)
            {
                var content = context.UmbracoContext.Content?.GetById(document.Id);
                if (content == null)
                {
                    continue; // A publication change can precede remote search-index visibility.
                }

                rendered++;
                bool product = content.ContentType.Alias == "product";
                body.Append($"<article class='card' data-node='{content.Key}' data-entity='{E(DemoConfiguration.EntityId(content, services))}'><a href='{E(content.Url(culture))}'><div class='illustration {E(content.Value<string>("color"))}'>{(product ? Image(content.Name) : Icon("Journal"))}</div><div class='card-body'><small>{(product ? "EQUIPMENT" : "FIELD NOTES")}</small><h3>{E(content.Value<string>("heading", culture: culture) ?? content.Name)}</h3><p>{E(content.Value<string>("bodyText", culture: culture))}</p>{(product ? $"<strong>DKK {E(content.Value<string>("salesPrice"))}</strong>" : "<strong>Read the story →</strong>")}</div></a><footer>Relewise ID: <code>{E(DemoConfiguration.EntityId(content, services))}</code></footer></article>");
            }
            body.Append("</section>");
            if (rendered < result.Documents.Count())
            {
                body.Append("<p class='empty'>Some pages are being updated. Please try again shortly.</p>");
            }

            if (result.Total == 0)
            {
                body.Append("<p class='empty'>No matches. Try another search.</p>");
            }

            body.Append($"<nav class='pagination'><a href='?provider={E(alias)}&type={requestedType}&q={Uri.EscapeDataString(query ?? "")}&culture={E(culture)}&page={Math.Max(1, page - 1)}'>← Previous</a><span>Page {page}</span><a href='?provider={E(alias)}&type={requestedType}&q={Uri.EscapeDataString(query ?? "")}&culture={E(culture)}&page={page + 1}'>Next →</a></nav><aside class='evidence'>One indexing pipeline · named Content & Product searches · canonical IDs shared with behavioral tracking</aside>");
            return Results.Content(Layout(body.ToString(), connected), "text/html");
        });
    }
    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Image(string name) => "<img alt='' width='190' height='190' src='/images/" + (name.Contains("flask", StringComparison.OrdinalIgnoreCase) ? "flask" : name.Contains("shelter", StringComparison.OrdinalIgnoreCase) ? "shelter" : "daypack") + ".svg'>";
    public static string Icon(string name) => name.Contains("flask", StringComparison.OrdinalIgnoreCase)
        ? "<svg viewBox='0 0 200 180' aria-hidden='true'><rect x='77' y='25' width='46' height='18' rx='5'/><rect x='66' y='40' width='68' height='116' rx='20'/><path d='M82 60v70' stroke='white' opacity='.25' stroke-width='6'/></svg>"
        : name.Contains("pack", StringComparison.OrdinalIgnoreCase)
        ? "<svg viewBox='0 0 200 180' aria-hidden='true'><path d='M78 30q22-26 44 0' fill='none' stroke='currentColor' stroke-width='9'/><rect x='50' y='28' width='100' height='130' rx='27'/><rect x='66' y='90' width='68' height='50' rx='12' fill='white' opacity='.2'/><path d='M64 62h72' stroke='white' opacity='.4' stroke-width='4'/></svg>"
        : "<svg viewBox='0 0 200 180' aria-hidden='true'><path d='M20 150 100 30 180 150z'/><path d='M100 30v120h60z' fill='white' opacity='.2'/><path d='m70 150 30-60 30 60' fill='#edf1e9'/></svg>";
    public static string Layout(string body, bool connected = true) => "<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Fieldwork — Umbraco 19 storefront</title><style>" + Css + "</style></head><body><header><a href='/store' class='logo'>FIELDWORK<span>OUTDOOR GOODS</span></a><nav><a href='/store'>Shop</a><a href='/store?type=article'>Journal</a>" + (connected ? "<a href='/store?provider=offers'>Offers</a>" : string.Empty) + "</nav><span class='local'>DEMO STOREFRONT</span></header><main>" + (connected ? string.Empty : "<p class='evidence'>Examine is serving search. Relewise query profiles can be enabled through the optional local.settings.json configuration.</p>") + body + "</main><footer class='site-footer'>Umbraco 19 + Relewise · Local development dataset</footer></body></html>";
    private const string Css = "*{box-sizing:border-box}body{margin:0;background:#fafaf6;color:#20352b;font:16px system-ui,sans-serif}a{color:inherit;text-decoration:none}header{padding:24px 5%;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #dde1d8}.logo{font-weight:800;font-size:24px;letter-spacing:3px}.logo span{display:block;font-size:9px;letter-spacing:2px}nav{display:flex;gap:28px}.local,.eyebrow{font-size:11px;letter-spacing:2px}.local{border:1px solid #bdcab6;border-radius:30px;padding:8px 12px}main{max-width:1200px;margin:auto;padding:0 32px}.hero{padding:54px 0 32px}.hero h1{font-size:46px;max-width:680px;line-height:1.1;margin:16px 0}.hero p{color:#657264}.search{background:#eaf0e4;border-radius:10px;padding:20px}.search label{display:block;font-size:13px;font-weight:600;margin-bottom:8px}.search div{display:flex;gap:10px}input,select,button{font:inherit;border:1px solid #c3ceba;border-radius:5px;padding:12px;background:#fff}input{flex:1;min-width:100px}button{background:#254c37;color:#fff;border-color:#254c37;cursor:pointer;padding:12px 28px}.result-heading{display:flex;align-items:center;justify-content:space-between;margin:32px 0 16px}.result-heading h2{margin:0;font-size:25px}.result-heading span{font-size:12px;color:#657264}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:22px}.card{border:1px solid #dfe4da;border-radius:8px;overflow:hidden;background:white}.illustration{background:#e9dfcc;height:190px;display:grid;place-items:center;color:#556746}.illustration svg{height:170px;fill:currentColor}.card:nth-child(2n) .illustration{background:#dfe7e8;color:#456573}.card:nth-child(3n) .illustration{background:#e6e7d7;color:#8a704b}.card-body{padding:22px}.card small{font-size:10px;letter-spacing:2px}.card h3{font-size:22px;margin:12px 0}.card p{color:#657264;font-size:14px;min-height:40px}.card footer{padding:10px 20px;background:#f3f5ef;font-size:10px;overflow-wrap:anywhere}.pagination{justify-content:center;margin:28px}.evidence{border-left:3px solid #809675;background:#f0f3eb;padding:16px;font-size:12px;margin:32px 0}.site-footer{padding:25px;text-align:center;color:#72806e;font-size:12px;border-top:1px solid #dde1d8}.detail{display:grid;grid-template-columns:1fr 1fr;gap:40px;margin:50px 0}.detail .illustration{height:380px;border-radius:10px}.detail .illustration svg{height:300px}.detail h1{font-size:40px}.details{padding:20px}.ids{font:12px monospace;background:#edf2e7;padding:18px;overflow-wrap:anywhere}.variants{display:flex;gap:14px;margin:20px 0}.variants a{border:1px solid #cad6c3;padding:8px 12px;border-radius:5px}@media(max-width:700px){.grid{grid-template-columns:1fr}.hero h1{font-size:32px}.detail{grid-template-columns:1fr}.search div{flex-wrap:wrap}.local{display:none}}";
}
