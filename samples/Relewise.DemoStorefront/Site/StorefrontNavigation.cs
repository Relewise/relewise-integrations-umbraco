using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.Site.SiteContent;

namespace Relewise.DemoStorefront.Site;

// Navigation and short-lived feedback for the example; basket contents stay in CartStateStore.
internal static class StorefrontNavigation
{
    private const string NoticeKey = "Fieldwork.Notice";

    internal static void Notify(HttpContext http, string key, bool error = false, params string[] values)
    {
        ITempDataDictionary data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        data[NoticeKey] = JsonSerializer.Serialize(new Notice(key, error, values));
        // Minimal API results do not execute MVC's TempData save filter.
        data.Save();
    }

    internal static string Feedback(HttpContext http)
    {
        ITempDataDictionary data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
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
            IPublishedContentCache cache = http.RequestServices.GetRequiredService<IUmbracoContextAccessor>().GetRequiredUmbracoContext().Content;
            string? translated = cache?.GetById(key)?.Url(market.Culture);
            if (!string.IsNullOrWhiteSpace(translated) && translated.StartsWith('/') && !translated.StartsWith("//"))
            {
                path = translated;
            }
        }
        return path + http.Session.GetString("fieldwork-listing-query");
    }

    private sealed record Notice(string Key, bool Error, string[] Values);

    internal static string CategoryLinks(IPublishedContent home, HttpContext http)
    {
        return Links(ShoppingCategories(home), http);
    }

    internal static string Links(IEnumerable<IPublishedContent> pages, HttpContext http)
    {
        IPublishedContent[] links = pages.OrderBy(Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return links.Length == 0 ? "" : $"<nav class='categories' aria-label='{T(http, "Categories")}'>" +
            string.Concat(links.Select(page => $"<a href='{Storefront.E(page.Url())}'>{Storefront.E(Title(page))}</a>")) + "</nav>";
    }

    internal static string Breadcrumb(IPublishedContent? page, HttpContext http, string section, string? current = null)
    {
        var market = DemoMarket.Current(http);
        var crumbs = new List<(string Name, string? Url)> { (Storefront.Text(http, "Home"), market.HomeUrl) };
        string key = section switch
        {
            "journal" => "Journal",
            "offers" => "OffersNavigation",
            "search" => "SearchResults",
            _ => "Shop"
        };
        string sectionUrl = section switch
        {
            "journal" => market.StoreUrl + "?type=article",
            "offers" => market.LanguagePath + "/offers",
            "search" => market.LanguagePath + "/search",
            _ => market.StoreUrl
        };
        crumbs.Add((Storefront.Text(http, key), sectionUrl));
        if (page != null && section == "shop")
        {
            crumbs.AddRange(page.Ancestors().Reverse().Where(node => node.ContentType.Alias == "productCategory" &&
                node.Value<string>("categoryId") != "U19-SMOKE-EQUIPMENT").Select(node => (Title(node), (string?)node.Url())));
            if (page.ContentType.Alias == "productVariant" && page.Parent() is { } parent)
            {
                crumbs.Add((Title(parent), parent.Url()));
            }
        }

        if (page == null && current == crumbs[^1].Name)
        {
            crumbs[^1] = (current, null);
        }
        else if (page != null || current != null)
        {
            crumbs.Add((current ?? Title(page!), null));
        }
        else
        {
            crumbs[^1] = (crumbs[^1].Name, null);
        }

        return $"<nav class='breadcrumbs' aria-label='{T(http, "Breadcrumb")}'><ol>" +
            string.Concat(crumbs.Select(crumb => crumb.Url == null
                ? $"<li aria-current='page'>{Storefront.E(crumb.Name)}</li>"
                : $"<li><a href='{Storefront.E(crumb.Url)}'>{Storefront.E(crumb.Name)}</a></li>")) + "</ol></nav>";
    }

    private static string T(HttpContext http, string key) => Storefront.E(Storefront.Text(http, key));
}
