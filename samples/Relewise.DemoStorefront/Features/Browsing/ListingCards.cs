using Relewise.DemoStorefront.Features.Products;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.Site.SiteContent;

namespace Relewise.DemoStorefront.Features.Browsing;

internal static class ListingCards
{
    internal static ListingCard CardData(
        IPublishedContent page,
        HttpContext http)
    {
        var product = page.ContentType.Alias == "product";
        var image = page.ContentType.Alias is "product" or "article" &&
                    !string.IsNullOrWhiteSpace(page.Value<string>("heroImage"))
            ? Storefront.Image(page)
            : Storefront.Icon();
        (string Sale, string? List, string? Savings) prices =
            product ? ProductPrices.Display(page, http) : (Sale: "", List: null, Savings: null);
        var stock = page.Value<int>("stock");
        var options = product
            ? page.Children()
                .Where(node => node.ContentType.Alias == "productVariant")
                .Select(ProductOptions.Label)
                .Distinct()
                .ToArray()
            : [];

        return new ListingCard(
            page.Key,
            DemoConfiguration.EntityId(page, http.RequestServices),
            page.Url(),
            Title(page),
            page.Value<string>("bodyText") ?? "",
            image,
            product,
            prices.Sale,
            prices.List,
            product
                ? Storefront.Text(http, stock <= 0 ? "OutOfStock" : stock <= 3 ? "LowStock" : "InStock", stock)
                : "",
            options.Length > 0 ? Storefront.Text(http, "AvailableOptions", string.Join(", ", options)) : null,
            prices.Savings,
            product && page.Value<IPublishedContent>("brand") is { } brand ? Title(brand) : null
        );
    }

    internal static string Card(
        IPublishedContent page,
        HttpContext http)
    {
        ListingCard card = CardData(page, http);
        var details = card.Product
            ? $"<span class='card-price'><strong>{Storefront.E(card.Price)}</strong>" +
              (card.ListPrice == null
                  ? ""
                  : $"<del>{Storefront.E(card.ListPrice)}</del><span class='offer-label'>" +
                    $"{Storefront.E(card.Savings)}</span>") +
              $"</span><span class='card-availability'>{Storefront.E(card.Availability)}</span>" +
              (card.Options == null ? "" : $"<span class='card-options'>{Storefront.E(card.Options)}</span>")
            : $"<strong>{T(http, "ReadStory")}</strong>";

        var label = card.Brand == null
            ? T(http, card.Product ? "Equipment" : "FieldNotes")
            : Storefront.E(card.Brand);

        return $"<article class='card' data-node='{card.Key}' data-entity='{Storefront.E(card.EntityId)}'>" +
               $"<a href='{Storefront.E(card.Url)}'><div class='illustration'>{card.ImageHtml}</div>" +
               $"<div class='card-body'><small>{label}</small><h3>{Storefront.E(card.Title)}</h3>" +
               $"{details}<p>{Storefront.E(card.Summary)}</p></div></a></article>";
    }

    private static string T(HttpContext http, string key)
    {
        return Storefront.E(Storefront.Text(http, key));
    }
}
