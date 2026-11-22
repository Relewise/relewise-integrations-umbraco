using System.Text;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Relewise.DemoStorefront;

internal static class StorefrontPages
{
    internal static IPublishedContent Home(IServiceProvider services)
    {
        HttpContext? http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        if (http?.Items["Fieldwork:Home"] is IPublishedContent cached)
        {
            return cached;
        }
        Guid key = services.GetRequiredService<IContentService>().GetRootContent().Single(node => node.ContentType.Alias == "home").Key;
        IPublishedContent home = services.GetRequiredService<IUmbracoContextAccessor>().GetRequiredUmbracoContext().Content!.GetById(key)!;
        if (http != null)
        {
            http.Items["Fieldwork:Home"] = home;
        }
        return home;
    }

    internal static async Task<string> RenderAsync(IPublishedContent page, HttpContext http)
    {
        if (page.ContentType.Alias is "productCategory" or "folder" or "brand")
        {
            return await StorefrontListing.RenderAsync(http, page);
        }

        return Render(page, http);
    }

    private static string Render(IPublishedContent page, HttpContext http)
    {
        string html = page.ContentType.Alias switch
        {
            "home" => Welcome(page, http),
            "product" or "productVariant" => Product(page, http),
            _ => Article(page, http)
        };
        string section = page.ContentType.Alias is "article" or "localizedArticle" or "guide" ? "journal" :
            page.ContentType.Alias == "home" ? "home" : "shop";
        bool connected = http.RequestServices.GetService<Relewise.Integrations.Umbraco.RelewiseUmbracoConfiguration>() != null;
        return Storefront.Layout(Title(page), html, connected, http, section);
    }

    internal static string CategoryLinks(IPublishedContent home, HttpContext http)
    {
        return Links(ShoppingCategories(home), http);
    }

    internal static IEnumerable<IPublishedContent> ShoppingCategories(IPublishedContent home)
    {
        IPublishedContent? equipment = home.Descendants().FirstOrDefault(node => node.ContentType.Alias == "productCategory" &&
            node.Value<string>("categoryId") == "U19-SMOKE-EQUIPMENT");
        return equipment?.Children().Where(node => node.ContentType.Alias == "productCategory") ?? [];
    }

    internal static IEnumerable<IPublishedContent> ShoppingBrands(IPublishedContent home) =>
        home.Descendants().Where(node => node.ContentType.Alias == "brand").OrderBy(Title, StringComparer.CurrentCultureIgnoreCase);

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

    internal static int? DiscountPercentage(decimal? salesPrice, decimal? listPrice) =>
        salesPrice is >= 0 && listPrice > salesPrice && listPrice > 0
            ? (int)decimal.Floor((listPrice.Value - salesPrice.Value) / listPrice.Value * 100)
            : null;

    private static (string Sale, string? List, string? Savings) SalePrices(IPublishedContent product, HttpContext http)
    {
        var market = DemoMarket.Current(http);
        decimal? Read(string alias) => PriceConverter.Read(product.GetProperty(alias)?.GetValue())?.Values?
            .SingleOrDefault(value => value.Currency.Value == market.Currency)?.Amount;
        string Amount(decimal value) => market.Currency + " " + value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo(market.Culture));
        decimal? sale = Read("salesPrice");
        decimal? list = Read("listPrice");
        int? percent = DiscountPercentage(sale, list);
        string? savings = percent.HasValue
            ? percent > 0 ? Storefront.Text(http, "SavePercent", percent.Value) : Storefront.Text(
                http,
                "SaveAmount",
                Amount(list!.Value - sale!.Value)
            )
            : null;
        return (sale.HasValue ? Amount(sale.Value) : Storefront.Text(http, "PriceUnavailable", market.Currency),
            percent.HasValue ? Amount(list!.Value) : null, savings);
    }

    internal static ListingCard CardData(IPublishedContent page, HttpContext http)
    {
        bool product = page.ContentType.Alias == "product";
        string image = page.ContentType.Alias is "product" or "article" && !string.IsNullOrWhiteSpace(page.Value<string>("heroImage"))
            ? Storefront.Image(page) : Storefront.Icon();
        (string Sale, string? List, string? Savings) prices = product ? SalePrices(page, http) : (Sale: "", List: (string?)null, Savings: (string?)null);
        int stock = page.Value<int>("stock");
        string[] options = product ? page.Children().Where(node => node.ContentType.Alias == "productVariant").Select(OptionLabel).Distinct().ToArray() : [];
        return new ListingCard(page.Key, DemoConfiguration.EntityId(page, http.RequestServices), page.Url(), Title(page),
            page.Value<string>("bodyText") ?? "", image, product, prices.Sale, prices.List,
            product ? Storefront.Text(http, stock <= 0 ? "OutOfStock" : stock <= 3 ? "LowStock" : "InStock", stock) : "",
            options.Length > 0 ? Storefront.Text(http, "AvailableOptions", string.Join(", ", options)) : null, prices.Savings,
            product && page.Value<IPublishedContent>("brand") is { } brand ? Title(brand) : null
        );
    }

    internal static string Card(IPublishedContent page, HttpContext http)
    {
        ListingCard card = CardData(page, http);
        string details = card.Product
            ? $"<span class='card-price'><strong>{Storefront.E(card.Price)}</strong>" +
                (card.ListPrice == null ? "" : $"<del>{Storefront.E(card.ListPrice)}</del><span class='offer-label'>{Storefront.E(card.Savings)}</span>") +
                $"</span><span class='card-availability'>{Storefront.E(card.Availability)}</span>" +
                (card.Options == null ? "" : $"<span class='card-options'>{Storefront.E(card.Options)}</span>")
            : $"<strong>{T(http, "ReadStory")}</strong>";
        return $"<article class='card' data-node='{card.Key}' data-entity='{Storefront.E(card.EntityId)}'><a href='{Storefront.E(card.Url)}'><div class='illustration'>{card.ImageHtml}</div><div class='card-body'><small>{(card.Brand == null ? T(http, card.Product ? "Equipment" : "FieldNotes") : Storefront.E(card.Brand))}</small><h3>{Storefront.E(card.Title)}</h3>{details}<p>{Storefront.E(card.Summary)}</p></div></a></article>";
    }

    private static string Welcome(IPublishedContent home, HttpContext http)
    {
        var market = DemoMarket.Current(http);
        IPublicAccessService access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        IPublishedContent[] nodes = home.Descendants().Where(node => !access.IsProtected(node.Path).Success).ToArray();
        return $"<section class='welcome'><div><p class='eyebrow'>Fieldwork {T(http, "OutdoorGoods")}</p><h1>{Storefront.E(Title(home))}</h1><p class='lead'>{Storefront.E(home.Value<string>("bodyText"))}</p><div class='actions'><a class='button' href='{market.StoreUrl}'>{T(http, "ExploreCollection")}</a><a class='button secondary' href='{market.StoreUrl}?type=article'>{T(http, "ExploreJournal")}</a></div></div><div class='illustration'>{Storefront.Icon()}</div></section>" +
            (DemoConnection.IsConfigured(http) ? StorefrontRecommendations.Placeholder(home, http) :
            $"<section class='page-section'><h2>{T(http, "ShopByCategory")}</h2>{CategoryLinks(home, http)}</section>" +
            $"<section class='page-section'><div class='section-heading'><h2>{T(http, "FromCollection")}</h2><a href='{market.StoreUrl}'>{T(http, "ViewAllProducts")}</a></div><div class='grid'>" +
            string.Concat(nodes.Where(node => node.ContentType.Alias == "product").OrderBy(Title).Take(3).Select(node => Card(node, http))) + "</div></section>") +
            $"<section class='page-section'><div class='section-heading'><h2>{T(http, "Journal")}</h2><a href='{market.StoreUrl}?type=article'>{T(http, "ViewAllStories")}</a></div><div class='grid'>" +
            string.Concat(nodes.Where(node => node.ContentType.Alias is "article" or "localizedArticle").OrderByDescending(node => node.CreateDate).ThenBy(Title).Take(3).Select(node => Card(node, http))) + "</div></section>";
    }

    private static string Product(IPublishedContent page, HttpContext http)
    {
        IPublishedContent product = page.ContentType.Alias == "productVariant" ? page.Parent()! : page;
        IPublishedContent? brand = product.Value<IPublishedContent>("brand");
        (string Sale, string? List, string? Savings) prices = SalePrices(product, http);
        IPublishedContent[] variants = product.Children().Where(node => node.ContentType.Alias == "productVariant").ToArray();
        string lead = page.Value<string>("bodyText") ?? "";
        string parentLead = product.Value<string>("bodyText") ?? "";
        string? rawOption = page.Value<string>("size");
        if (page != product && !string.IsNullOrWhiteSpace(rawOption) &&
            (lead == parentLead + " Available in " + rawOption + "." || lead == parentLead + " Fås i " + rawOption.ToLowerInvariant() + "."))
        {
            lead = parentLead;
        }
        var html = new StringBuilder(Breadcrumb(page, http, "shop"));
        html.Append($"<section class='detail'><div class='illustration'>{Storefront.Image(page)}</div><div class='details'><p class='eyebrow'>{Storefront.E(brand?.Name ?? "Fieldwork")}</p><h1>{Storefront.E(Title(page))}</h1><p class='lead'>{Storefront.E(lead)}</p><div class='price'><strong>{Storefront.E(prices.Sale)}</strong>" +
            (prices.List == null ? "" : $"<del>{Storefront.E(prices.List)}</del><span class='offer-label'>{Storefront.E(prices.Savings)}</span>") + "</div>");
        html.Append(StorefrontCart.Availability(product, http));
        if (variants.Length > 0)
        {
            string? option = product.Value<string>("optionType");
            if (string.IsNullOrWhiteSpace(option))
            {
                option = variants.Any(variant => !string.IsNullOrWhiteSpace(variant.Value<string>("color"))) ? "colour" : "size";
            }
            string key = option switch
            {
                "colour" => "ChooseColour",
                "capacity" => "ChooseCapacity",
                "dimensions" => "ChooseDimensions",
                "occupancy" => "ChooseOccupancy",
                "pack" => "ChoosePackSize",
                "length" => "ChooseLength",
                _ => "ChooseSize"
            };
            html.Append($"<p class='option-label'>{T(http, key)}</p><div class='variants'>");
            foreach (IPublishedContent variant in variants)
            {
                string label = OptionLabel(variant);
                html.Append($"<a href='{Storefront.E(variant.Url())}' {(page.Key == variant.Key ? "class='selected' aria-current='page'" : "")}>{Storefront.E(label)}</a>");
            }

            html.Append("</div>");
        }

        string? sizeGuide = product.Value<string>("sizeGuide");
        if (!string.IsNullOrWhiteSpace(sizeGuide))
        {
            html.Append($"<details class='size-guide'><summary>{T(http, "SizeGuide")}</summary><p>{Storefront.E(sizeGuide)}</p></details>");
        }
        html.Append(StorefrontCart.AddForm(page, http));
        int weight = product.Value<int>("weightGrams");
        string? capacity = page.Value<string>("capacity") ?? product.Value<string>("capacity");
        if (weight > 0 || !string.IsNullOrWhiteSpace(capacity))
        {
            html.Append($"<section class='product-specifications'><h2>{T(http, "Specifications")}</h2><dl>");
            if (weight > 0)
            {
                html.Append($"<dt>{T(http, "Weight")}</dt><dd>{weight} g</dd>");
            }
            if (!string.IsNullOrWhiteSpace(capacity))
            {
                html.Append($"<dt>{T(http, "Capacity")}</dt><dd>{Storefront.E(capacity)}</dd>");
            }
            html.Append("</dl></section>");
        }
        if (brand != null)
        {
            html.Append($"<p class='muted'>{T(http, "Brand")}: <a class='text-link' href='{Storefront.E(brand.Url())}'>{Storefront.E(Title(brand))}</a></p>");
        }

        html.Append("</div></section>");
        html.Append(StorefrontRecommendations.Placeholder(page, http));
        html.Append(DeveloperDetails(page, http));
        return html.ToString();
    }

    private static string Article(IPublishedContent page, HttpContext http)
    {
        string culture = DemoMarket.Current(http).Culture;
        var html = new StringBuilder(Breadcrumb(page, http, "journal"));
        html.Append($"<article class='reading'><header class='article-header'><p class='eyebrow'>{T(http, "Journal")}</p><h1>{Storefront.E(Title(page))}</h1><p class='lead'>{Storefront.E(page.Value<string>("bodyText"))}</p></header>");
        foreach (string alias in new[] { "contentRows", "pageGrid" })
        {
            html.Append(StorefrontEditorial.Render(page.GetProperty(alias)?.GetValue(), culture));
        }

        html.Append($"<p class='reading-back'><a class='text-link' href='{DemoMarket.Current(http).StoreUrl}?type=article'>{T(http, "ReturnJournal")}</a></p></article>");
        html.Append(DeveloperDetails(page, http));
        return html.ToString();
    }

    private static string DeveloperDetails(IPublishedContent page, HttpContext http)
    {
        var fields = new List<(string Label, string Value)>
        {
            ("Umbraco node ID", page.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Umbraco key", page.Key.ToString("D")),
            ("Document type", page.ContentType.Alias),
            ("Culture", DemoMarket.Current(http).Culture)
        };
        if (page.ContentType.Alias == "productVariant" && page.Parent() is { } product)
        {
            fields.Add(("Relewise Product ID", DemoConfiguration.EntityId(product, http.RequestServices)));
            fields.Add(("Relewise Variant ID", DemoConfiguration.EntityId(page, http.RequestServices)));
            fields.Add(("Parent Product key", product.Key.ToString("D")));
        }
        else
        {
            fields.Add((page.ContentType.Alias == "product" ? "Relewise Product ID" : "Relewise Content ID",
                DemoConfiguration.EntityId(page, http.RequestServices)));
        }

        string rows = string.Concat(fields.Select(field =>
            $"<dt>{Storefront.E(field.Label)}</dt><dd><code>{Storefront.E(field.Value)}</code></dd>"));
        return $"<details class='developer-details' lang='en'><summary>Integration details</summary><dl>{rows}</dl></details>";
    }

    internal static string OptionLabel(IPublishedContent page)
    {
        foreach (string alias in new[] { "optionLabel", "color", "size" })
        {
            string? value = page.Value<string>(alias);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return Title(page);
    }

    internal static string Title(IPublishedContent page) => page.Value<string>("heading") ?? page.Name;
    private static string T(HttpContext http, string key) => Storefront.E(Storefront.Text(http, key));
}
