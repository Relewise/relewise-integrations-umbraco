using System.Globalization;
using System.Net;
using Umbraco.Cms.Core.Dictionary;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

public static class Storefront
{
    internal static void MapStorefront(this WebApplication app)
    {
        app.MapGet("/store/market", async Task<IResult> (HttpContext http, string? market, string? returnUrl) =>
        {
            var selected = DemoMarket.All.FirstOrDefault(value => value.Id == market);
            if (selected == null)
            {
                return Results.BadRequest("Unknown market.");
            }

            http.Items[typeof(DemoMarket)] = selected;
            try
            {
                await StorefrontCart.ChangeMarket(http);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("StorefrontCart")
                    .LogError(error, "Unable to change the cart currency.");
                StorefrontNavigation.Notify(http, DemoConnection.IsConnectionFailure(error, http.RequestAborted) ? "RelewiseActionFailed" : "CartFailed", true);
                return Results.LocalRedirect(selected.ReturnUrl(returnUrl));
            }

            http.Response.Cookies.Append(DemoMarket.CookieName, selected.Id, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30)
            });
            string target = selected.ReturnUrl(returnUrl);
            // Resolve translated detail-page slugs through Umbraco, keeping the same content item.
            if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate("http://localhost" + returnUrl, UriKind.Absolute, out var uri))
            {
                using var context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
                var site = http.RequestServices.GetRequiredService<IContentService>().GetRootContent().Single(node => node.ContentType.Alias == "home");
                var home = context.UmbracoContext.Content?.GetById(site.Key);
                var page = home?.DescendantsOrSelf().FirstOrDefault(node =>
                    node.Url("en-US") == uri.AbsolutePath || node.Url("da-DK") == uri.AbsolutePath);
                if (page != null)
                {
                    target = page.Url(selected.Culture) + uri.Query;
                }
            }

            StorefrontNavigation.Notify(http, "MarketChanged", false, Text(http, selected.LabelKey), selected.Currency);
            return Results.LocalRedirect(target);
        });
        app.MapGet("/store", (HttpContext http) => Results.LocalRedirect(DemoMarket.Current(http).StoreUrl + http.Request.QueryString));
        foreach (string operation in new[] { "suggestions", "results" })
        {
            app.MapGet("/{language:regex(^(en|da)$)}/store/" + operation, async Task<IResult> (HttpContext http) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                IPublishedContent? collection = null;
                if (http.Request.Query.TryGetValue("collection", out var selected))
                {
                    if (!Guid.TryParse(selected, out Guid key))
                    {
                        return Results.BadRequest(new { error = "Invalid collection." });
                    }
                    collection = StorefrontPages.Home(http.RequestServices).DescendantsOrSelf().FirstOrDefault(node =>
                        node.Key == key && node.ContentType.Alias is "brand" or "productCategory" or "folder");
                    if (collection == null)
                    {
                        return Results.NotFound(new { error = "Unknown collection." });
                    }
                }
                bool suggestions = operation == "suggestions";
                return Results.Content(await StorefrontListing.RenderAsync(http, collection, suggestionsOnly: suggestions, dataOnly: !suggestions), "application/json");
            });
        }
        foreach (string view in new[] { "store", "search", "offers" })
        {
            app.MapGet("/{language:regex(^(en|da)$)}/" + view, async Task<IResult> (HttpContext http) =>
            {
                if (view == "store" && http.Request.Query["provider"] == "offers")
                {
                    return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/offers" +
                        QueryString.Create(http.Request.Query.Where(pair => pair.Key != "provider")));
                }
                return Results.Content(await StorefrontListing.RenderAsync(http, view: view), "text/html");
            });
        }
    }

    internal static string PageUrl(HttpRequest request, long page)
    {
        var query = request.Query.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
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

    internal static string Text(HttpContext http, string key, params object[] values)
    {
        var culture = CultureInfo.GetCultureInfo(DemoMarket.Current(http).Culture);
        string text = http.RequestServices.GetRequiredService<ICultureDictionaryFactory>().CreateDictionary(culture)["Fieldwork." + key];
        return values.Length == 0 ? text : string.Format(culture, text, values);
    }

    public static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Prices(object? value, HttpContext http)
    {
        var market = DemoMarket.Current(http);
        var price = PriceConverter.Read(value)?.Values?.SingleOrDefault(price => price.Currency.Value == market.Currency);
        return price == null
            ? E(Text(http, "PriceUnavailable", market.Currency))
            : E(market.Currency + " " + price.Amount.ToString("N2", CultureInfo.GetCultureInfo(market.Culture)));
    }

    public static string Image(Umbraco.Cms.Core.Models.PublishedContent.IPublishedContent content)
    {
        string? image = content.Value<string>("heroImage");
        if (string.IsNullOrWhiteSpace(image) && content.ContentType.Alias == "productVariant")
        {
            image = content.Parent()?.Value<string>("heroImage");
        }
        image = string.IsNullOrWhiteSpace(image) ? "/images/daypack.svg" : image;
        return DemoIllustrations.Render(content, image) ?? $"<img alt='' width='190' height='190' src='{E(image)}'>";
    }
    public static string Icon() => "<svg viewBox='0 0 200 180' aria-hidden='true'><path d='M20 150 100 30 180 150z'/><path d='M100 30v120h60z' fill='white' opacity='.2'/><path d='m70 150 30-60 30 60' fill='#edf1e9'/></svg>";
    public static string Layout(string title, string body, bool connected, HttpContext http, string section) =>
        StorefrontChrome.Render(title, body, http, section);
}
