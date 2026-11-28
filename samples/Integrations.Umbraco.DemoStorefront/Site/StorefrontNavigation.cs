using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;
using static Relewise.Integrations.Umbraco.DemoStorefront.Site.SiteContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

// Navigation and short-lived feedback for the example; basket contents stay in CartStateStore.
internal static class StorefrontNavigation
{
    private const string NoticeKey = "Fieldwork.Notice";
    private const string ListingViewKey = "fieldwork-listing-view";
    private const string ListingCollectionKey = "fieldwork-listing-collection";
    private const string ListingQueryKey = "fieldwork-listing-query";

    public static void Notify(
        HttpContext http,
        string key,
        bool error = false,
        params string[] values)
    {
        ITempDataDictionary data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(http);

        data[NoticeKey] = JsonSerializer.Serialize(new Notice(key, error, values));
        // Minimal API results do not execute MVC's TempData save filter.
        data.Save();
    }

    public static StorefrontNotice? Feedback(HttpContext http)
    {
        ITempDataDictionary data = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(http);

        if (data[NoticeKey] is not string json)
        {
            return null;
        }

        data.Remove(NoticeKey);
        data.Save();

        var notice = JsonSerializer.Deserialize<Notice>(json)!;
        return new StorefrontNotice(
            Storefront.Text(http, notice.Key, [.. notice.Values]),
            notice.Error,
            notice.Key == "CartAdded");
    }

    public static async Task RememberListingAsync(
        HttpContext http,
        Guid? collection,
        string view = "store",
        QueryString? query = null)
    {
        await http.Session.LoadAsync(http.RequestAborted);
        http.Session.SetString(ListingCollectionKey, collection?.ToString("D") ?? "");
        http.Session.SetString(ListingViewKey, view);
        http.Session.SetString(
            ListingQueryKey,
            (query ?? QueryString.Create(http.Request.Query.Where(pair => pair.Key is not ("collection" or "view"))))
            .Value ?? "");
    }

    public static string ShoppingUrl(HttpContext http)
    {
        DemoMarket market = DemoMarket.Current(http);
        string view = http.Session.GetString(ListingViewKey) ?? "store";
        string path = market.LanguagePath + (view is "search" or "offers" ? "/" + view : "/store");

        if (Guid.TryParse(http.Session.GetString(ListingCollectionKey), out Guid key))
        {
            IPublishedContentCache cache = http.RequestServices.GetRequiredService<IUmbracoContextAccessor>()
                .GetRequiredUmbracoContext()
                .Content;
            string? translated = cache.GetById(key)?.Url(market.Culture);

            if (!string.IsNullOrWhiteSpace(translated) && translated.StartsWith('/') && !translated.StartsWith("//"))
            {
                path = translated;
            }
        }

        return path + http.Session.GetString(ListingQueryKey);
    }

    private sealed record Notice(string Key, bool Error, string[] Values);

    public static StorefrontLink[] Links(IEnumerable<IPublishedContent> pages)
    {
        return
        [
            .. pages.OrderBy(Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(page => new StorefrontLink(Title(page), page.Url(), false))
        ];
    }

    public static BreadcrumbItem[] Breadcrumb(
        IPublishedContent? page,
        HttpContext http,
        string section,
        string? current = null)
    {
        DemoMarket market = DemoMarket.Current(http);
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
            crumbs.AddRange(
                page.Ancestors()
                    .Reverse()
                    .Where(node => node.ContentType.Alias == DocumentTypeAliases.ProductCategory &&
                                   node.Value<string>(PropertyAliases.CategoryId) != "U19-SMOKE-EQUIPMENT")
                    .Select(node => (Title(node), (string?)node.Url())));

            if (page.ContentType.Alias == DocumentTypeAliases.ProductVariant && page.Parent() is { } parent)
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

        return [.. crumbs.Select(crumb => new BreadcrumbItem(crumb.Name, crumb.Url))];
    }
}
