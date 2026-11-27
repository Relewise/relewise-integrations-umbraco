using Relewise.Integrations.Umbraco.DemoStorefront.Features.Recommendations;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;

internal static class ProductPage
{
    public static ProductPageModel Create(
        IPublishedContent page,
        HttpContext http)
    {
        IPublishedContent product = page.ContentType.Alias == DocumentTypeAliases.ProductVariant
            ? page.Parent()!
            : page;
        var brand = product.Value<IPublishedContent>(PropertyAliases.Brand);

        (string Sale, string? List, string? Savings) prices = ProductPrices.Display(product, http);
        IPublishedContent[] variants =
            [.. product.Children().Where(node => node.ContentType.Alias == DocumentTypeAliases.ProductVariant)];

        string lead = page.Value<string>(PropertyAliases.BodyText) ?? "";
        string parentLead = product.Value<string>(PropertyAliases.BodyText) ?? "";
        var rawOption = page.Value<string>(PropertyAliases.Size);

        if (page != product && !string.IsNullOrWhiteSpace(rawOption) &&
            (lead == parentLead + " Available in " + rawOption + "." ||
             lead == parentLead + " Fås i " + rawOption.ToLowerInvariant() + "."))
        {
            lead = parentLead;
        }

        var option = product.Value<string>("optionType");

        if (string.IsNullOrWhiteSpace(option))
        {
            option = variants.Any(variant => !string.IsNullOrWhiteSpace(variant.Value<string>(PropertyAliases.Color)))
                ? "colour"
                : PropertyAliases.Size;
        }

        string optionLabel = option switch
        {
            "colour" => "ChooseColour",
            PropertyAliases.Capacity => "ChooseCapacity",
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
            SizeGuide = product.Value<string>(PropertyAliases.SizeGuide),
            Weight = product.Value<int>(PropertyAliases.WeightGrams),
            Capacity = page.Value<string>(PropertyAliases.Capacity) ?? product.Value<string>(PropertyAliases.Capacity),
            Purchase = PurchaseControls.Create(page, product, http),
            Recommendations = RecommendationPlacement.Configuration(page, http)
        };
    }
}
