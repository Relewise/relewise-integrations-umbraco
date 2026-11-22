using System.Text;
using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.Site.SiteContent;
using static Relewise.DemoStorefront.Site.StorefrontNavigation;

namespace Relewise.DemoStorefront.Features.Products;

internal static class ProductPage
{
    internal static string Render(
        IPublishedContent page,
        HttpContext http)
    {
        IPublishedContent product = page.ContentType.Alias == "productVariant" ? page.Parent()! : page;
        var brand = product.Value<IPublishedContent>("brand");

        (string Sale, string? List, string? Savings) prices = ProductPrices.Display(product, http);
        IPublishedContent[] variants =
            [.. product.Children().Where(node => node.ContentType.Alias == "productVariant")];

        string lead = page.Value<string>("bodyText") ?? "";
        string parentLead = product.Value<string>("bodyText") ?? "";
        var rawOption = page.Value<string>("size");

        if (page != product && !string.IsNullOrWhiteSpace(rawOption) &&
            (lead == parentLead + " Available in " + rawOption + "." ||
             lead == parentLead + " Fås i " + rawOption.ToLowerInvariant() + "."))
        {
            lead = parentLead;
        }

        var html = new StringBuilder(Breadcrumb(page, http, "shop"));

        html.Append(
            ($"<section class='detail'><div class='illustration'>{Storefront.Image(page)}" +
             $"</div><div class='details'><p class='eyebrow'>{Storefront.E(brand?.Name ?? "Fieldwork")}" +
             $"</p><h1>{Storefront.E(Title(page))}</h1><p class='lead'>{Storefront.E(lead)}" +
             $"</p><div class='price'><strong>{Storefront.E(prices.Sale)}</strong>") +
            (prices.List == null
                ? ""
                : $"<del>{Storefront.E(prices.List)}</del>" +
                  $"<span class='offer-label'>{Storefront.E(prices.Savings)}</span>") +
            "</div>");
        html.Append(PurchaseControls.Availability(product, http));

        if (variants.Length > 0)
        {
            var option = product.Value<string>("optionType");

            if (string.IsNullOrWhiteSpace(option))
            {
                option = variants.Any(variant => !string.IsNullOrWhiteSpace(variant.Value<string>("color")))
                    ? "colour"
                    : "size";
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
                string label = ProductOptions.Label(variant);

                html.Append(
                    $"<a href='{Storefront.E(variant.Url())}' " +
                    $"{(page.Key == variant.Key ? "class='selected' aria-current='page'" : "")}>" +
                    $"{Storefront.E(label)}</a>");
            }

            html.Append("</div>");
        }

        var sizeGuide = product.Value<string>("sizeGuide");

        if (!string.IsNullOrWhiteSpace(sizeGuide))
        {
            html.Append(
                $"<details class='size-guide'><summary>{T(http, "SizeGuide")}</summary><p>" +
                $"{Storefront.E(sizeGuide)}</p></details>");
        }

        html.Append(PurchaseControls.AddForm(page, http));

        var weight = product.Value<int>("weightGrams");
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
            html.Append(
                $"<p class='muted'>{T(http, "Brand")}: <a class='text-link' href='" +
                $"{Storefront.E(brand.Url())}'>{Storefront.E(Title(brand))}</a></p>");
        }

        html.Append("</div></section>");
        html.Append(RecommendationPlacement.Placeholder(page, http));
        html.Append(IntegrationDetails.Render(page, http));

        return html.ToString();
    }

    private static string T(HttpContext http, string key)
    {
        return Storefront.E(Storefront.Text(http, key));
    }
}
