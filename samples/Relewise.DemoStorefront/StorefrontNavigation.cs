using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

// Navigation and short-lived feedback for the example; basket contents stay in StorefrontCart.
internal static class StorefrontNavigation
{
    private const string NoticeKey = "Fieldwork.Notice";

    internal static void Notify(HttpContext http, string key, bool error = false, params string[] values)
    {
        var data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        data[NoticeKey] = JsonSerializer.Serialize(new Notice(key, error, values));
        // Minimal API results do not execute MVC's TempData save filter.
        data.Save();
    }

    internal static string Feedback(HttpContext http)
    {
        var data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        if (data[NoticeKey] is not string json)
        {
            return string.Empty;
        }
        data.Remove(NoticeKey);
        data.Save();
        Notice notice = JsonSerializer.Deserialize<Notice>(json)!;
        string text = Storefront.E(Storefront.Text(http, notice.Key, notice.Values.Cast<object>().ToArray()));
        string actions = notice.Key == "CartAdded"
            ? $"<div class='notice-actions'><a class='text-link' href='{DemoMarket.Current(http).LanguagePath}/cart'>{Storefront.E(Storefront.Text(http, "ViewCart"))}</a><a class='text-link' href='{Storefront.E(ShoppingUrl(http))}'>{Storefront.E(Storefront.Text(http, "ContinueShopping"))}</a></div>"
            : string.Empty;
        return $"<section class='notice {(notice.Error ? "notice-error" : "")}' role='{(notice.Error ? "alert" : "status")}'><p>{text}</p>{actions}</section>";
    }

    internal static async Task RememberListingAsync(HttpContext http, Guid? collection, string view = "store", QueryString? query = null)
    {
        await http.Session.LoadAsync(http.RequestAborted);
        http.Session.SetString("fieldwork-listing-collection", collection?.ToString("D") ?? "");
        http.Session.SetString("fieldwork-listing-view", view);
        http.Session.SetString("fieldwork-listing-query", (query ?? QueryString.Create(http.Request.Query.Where(pair => pair.Key is not ("collection" or "view")))).Value ?? "");
    }

    internal static string ShoppingUrl(HttpContext http)
    {
        var market = DemoMarket.Current(http);
        string view = http.Session.GetString("fieldwork-listing-view") ?? "store";
        string path = market.LanguagePath + (view is "search" or "offers" ? "/" + view : "/store");
        if (Guid.TryParse(http.Session.GetString("fieldwork-listing-collection"), out Guid key))
        {
            var cache = http.RequestServices.GetRequiredService<IUmbracoContextAccessor>().GetRequiredUmbracoContext().Content;
            string? translated = cache?.GetById(key)?.Url(market.Culture);
            if (!string.IsNullOrWhiteSpace(translated) && translated.StartsWith('/') && !translated.StartsWith("//"))
            {
                path = translated;
            }
        }
        return path + http.Session.GetString("fieldwork-listing-query");
    }

    private sealed record Notice(string Key, bool Error, string[] Values);
}
