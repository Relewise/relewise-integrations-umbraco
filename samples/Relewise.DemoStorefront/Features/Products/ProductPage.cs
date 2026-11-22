using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Products;

internal static class ProductPage
{
    internal static ProductPageModel Create(
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

        var option = product.Value<string>("optionType");

        if (string.IsNullOrWhiteSpace(option))
        {
            option = variants.Any(variant => !string.IsNullOrWhiteSpace(variant.Value<string>("color")))
                ? "colour"
                : "size";
        }

        string optionLabel = option switch
        {
            "colour" => "ChooseColour",
            "capacity" => "ChooseCapacity",
            "dimensions" => "ChooseDimensions",
            "occupancy" => "ChooseOccupancy",
            "pack" => "ChoosePackSize",
            "length" => "ChooseLength",
            _ => "ChooseSize"
        };

        return new ProductPageModel
        {
            Page = page,
            Brand = brand,
            Lead = lead,
            Prices = prices,
            Variants =
            [
                .. variants.Select(variant =>
                    new StorefrontLink(ProductOptions.Label(variant), variant.Url(), page.Key == variant.Key))
            ],
            OptionLabel = optionLabel,
            SizeGuide = product.Value<string>("sizeGuide"),
            Weight = product.Value<int>("weightGrams"),
            Capacity = page.Value<string>("capacity") ?? product.Value<string>("capacity"),
            Purchase = PurchaseControls.Create(page, product, http),
            Recommendations = RecommendationPlacement.Configuration(page, http)
        };
    }
}
