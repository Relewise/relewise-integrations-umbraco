using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.DemoStorefront.Seeding;

internal static class DemoVariantDetails
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        var types = services.GetRequiredService<IContentTypeService>();
        IDataType text =
            (await services.GetRequiredService<IDataTypeService>().GetAllAsync()).First(type =>
                type.Name == "Textstring");
        var strings = services.GetRequiredService<IShortStringHelper>();
        IContentType productType = types.Get("product")!;
        var productSchemaChanged = false;

        foreach (var alias in new[] { "optionType", "sizeGuide" })
        {
            if (!productType.PropertyTypes.Any(property => property.Alias == alias))
            {
                productType.AddPropertyType(
                    new PropertyType(strings, text, alias)
                    {
                        Name = alias == "optionType" ? "Variant option type" : "Size guide",
                        Variations = alias == "sizeGuide" ? ContentVariation.Culture : ContentVariation.Nothing
                    });
                productSchemaChanged = true;
            }
        }

        if (productSchemaChanged)
        {
            await types.UpdateAsync(productType, Constants.Security.SuperUserKey);
        }

        IContentType variantType = types.Get("productVariant")!;
        var schemaChanged = false;

        foreach (var alias in new[] { "optionLabel", "capacity" })
        {
            if (!variantType.PropertyTypes.Any(property => property.Alias == alias))
            {
                variantType.AddPropertyType(
                    new PropertyType(strings, text, alias)
                    {
                        Name = alias == "optionLabel" ? "Option label" : "Capacity",
                        Variations = alias == "optionLabel" ? ContentVariation.Culture : ContentVariation.Nothing
                    });
                schemaChanged = true;
            }
        }

        if (schemaChanged)
        {
            await types.UpdateAsync(variantType, Constants.Security.SuperUserKey);
        }

        using IScope scope = services.GetRequiredService<IScopeProvider>().CreateScope();

        scope.EagerWriteLock(Constants.Locks.ContentTree);

        var content = services.GetRequiredService<IContentService>();
        IContent[] nodes =
        [
            .. content.GetRootContent()
                .SelectMany(root => content.GetPagedDescendants(
                    root.Id,
                    0,
                    1000,
                    out _
                ))
        ];

        foreach (IContent product in nodes.Where(node =>
                     node.ContentType.Alias == "product" && node.Published && !node.Edited))
        {
            if (!Specifications.TryGetValue(product.GetValue<string>("sku") ?? "", out Specification? spec))
            {
                continue;
            }

            var productChanged = false;

            if (string.IsNullOrWhiteSpace(product.GetValue<string>("optionType")))
            {
                product.SetValue("optionType", spec.Kind);
                productChanged = true;
            }

            if (spec.Kind == "capacity" && (string.IsNullOrWhiteSpace(product.GetValue<string>("capacity")) ||
                                            (product.GetValue<string>("sku") == "FIELDWORK-COOKING-POT" &&
                                             product.GetValue<string>("capacity") == "1.5 L")))
            {
                product.SetValue("capacity", string.Join(" / ", spec.English));
                productChanged = true;
            }

            if (product.GetValue<string>("sku") == "FIELDWORK-HIKING-GLOVES")
            {
                foreach (var culture in product.PublishedCultures)
                {
                    if (culture is "en-US" or "da-DK" &&
                        string.IsNullOrWhiteSpace(product.GetValue<string>("sizeGuide", culture)))
                    {
                        product.SetValue(
                            "sizeGuide",
                            culture == "en-US"
                                ? "Measure around your palm at its widest point, excluding your thumb. " +
                                        "The options show " +
                                  "hand circumference in centimetres. Choose the larger size if between sizes."
                                : "Mål rundt om håndfladen på det bredeste sted uden tommelfingeren. " +
                                        "Størrelserne viser " +
                                  "håndens omkreds i centimeter. Vælg den største størrelse, " +
                                        "hvis du ligger mellem to " +
                                  "størrelser.",
                            culture
                        );
                        productChanged = true;
                    }
                }
            }

            if (productChanged)
            {
                Publish(product);
            }

            IEnumerable<IContent> children = nodes.Where(node =>
                node.ParentId == product.Id && node.ContentType.Alias == "productVariant");
            string[] suffixes = spec.English.Length == 3
                ? ["SMALL", "MEDIUM", "LARGE"]
                : product.GetValue<string>("sku") is "FIELDWORK-REPAIR-KIT" or "FIELDWORK-PACK-COVER"
                    ? ["SMALL", "LARGE"]
                    : ["SOLO", "DUO"];

            foreach (IContent variant in children.Where(node => node.Published && !node.Edited))
            {
                var sku = variant.GetValue<string>("sku") ?? "";
                var index = Array.FindIndex(suffixes, suffix => sku == product.GetValue<string>("sku") + "-" + suffix);

                if (index < 0)
                {
                    continue;
                }

                var changed = false;

                foreach (var culture in variant.PublishedCultures.ToArray())
                {
                    if (culture is not ("en-US" or "da-DK"))
                    {
                        continue;
                    }

                    var label = variant.GetValue<string>("optionLabel", culture);

                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = culture == "en-US" ? spec.English[index] : spec.Danish[index];
                        variant.SetValue("optionLabel", label, culture);
                        changed = true;
                    }

                    var parentName = product.GetCultureName(culture) ?? product.Name ?? string.Empty;
                    var previousHeading = parentName + " " + variant.GetValue<string>("size", culture);
                    var heading = (product.GetValue<string>("heading", culture) ?? parentName) + " · " + label;

                    if (variant.GetValue<string>("heading", culture) == previousHeading && heading != previousHeading)
                    {
                        variant.SetValue("heading", heading, culture);
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                if (spec.Kind == "capacity" && string.IsNullOrWhiteSpace(variant.GetValue<string>("capacity")))
                {
                    variant.SetValue("capacity", spec.English[index]);
                }

                Publish(variant);
            }
        }

        scope.Complete();

        void Publish(IContent node)
        {
            content.Save(node);

            if (!content.Publish(node, [.. node.PublishedCultures]).Success)
            {
                throw new InvalidOperationException($"Failed publishing Variant details for '{node.Name}'.");
            }
        }
    }

    private static readonly Dictionary<string, Specification> Specifications = new(StringComparer.Ordinal)
    {
        ["FIELDWORK-HYDRATION-RESERVOIR"] =
            new Specification("capacity", ["1.5 L", "2 L", "3 L"], ["1.5 L", "2 L", "3 L"]),
        ["FIELDWORK-PACKING-CUBES"] = new Specification("capacity", ["3 L", "6 L", "10 L"], ["3 L", "6 L", "10 L"]),
        ["FIELDWORK-COMPRESSION-SACK"] =
            new Specification("capacity", ["5 L", "10 L", "15 L"], ["5 L", "10 L", "15 L"]),
        ["FIELDWORK-FRAME-BAG"] = new Specification("capacity", ["2 L", "3 L", "4 L"], ["2 L", "3 L", "4 L"]),
        ["FIELDWORK-WATER-CARRIER"] = new Specification("capacity", ["5 L", "10 L", "15 L"], ["5 L", "10 L", "15 L"]),
        ["FIELDWORK-MAP-POUCH"] = new Specification(
            "dimensions",
            ["A6 · 10.5 × 14.8 cm", "A5 · 14.8 × 21 cm", "A4 · 21 × 29.7 cm"],
            ["A6 · 10.5 × 14.8 cm", "A5 · 14.8 × 21 cm", "A4 · 21 × 29.7 cm"]
        ),
        ["FIELDWORK-TOWEL"] = new Specification(
            "dimensions",
            ["40 × 80 cm", "60 × 120 cm", "80 × 160 cm"],
            ["40 × 80 cm", "60 × 120 cm", "80 × 160 cm"]
        ),
        ["FIELDWORK-CAMP-TABLE"] = new Specification(
            "dimensions",
            ["60 × 40 cm", "80 × 60 cm", "100 × 70 cm"],
            ["60 × 40 cm", "80 × 60 cm", "100 × 70 cm"]
        ),
        ["FIELDWORK-FOAM-MAT"] =
            new Specification("length", ["120 cm", "180 cm", "200 cm"], ["120 cm", "180 cm", "200 cm"]),
        ["FIELDWORK-WINTER-SLEEPING-BAG"] = new Specification(
            "length",
            ["160 cm", "180 cm", "200 cm"],
            ["160 cm", "180 cm", "200 cm"]
        ),
        ["FIELDWORK-GEAR-STRAPS"] =
            new Specification("length", ["30 cm", "45 cm", "60 cm"], ["30 cm", "45 cm", "60 cm"]),
        ["FIELDWORK-SOCKS"] = new Specification(
            "size",
            ["EU 36–39", "EU 40–43", "EU 44–47"],
            ["EU 36–39", "EU 40–43", "EU 44–47"]),
        ["FIELDWORK-RAIN-GAITERS"] = new Specification(
            "size",
            ["EU 36–39", "EU 40–43", "EU 44–47"],
            ["EU 36–39", "EU 40–43", "EU 44–47"]
        ),
        ["FIELDWORK-HIKING-GLOVES"] = new Specification(
            "size",
            ["S · 18–19 cm", "M · 20–21 cm", "L · 22–23 cm"],
            ["S · 18–19 cm", "M · 20–21 cm", "L · 22–23 cm"]
        ),
        ["FIELDWORK-PHONE-POUCH"] = new Specification(
            "size",
            ["≤ 6.1 in", "≤ 6.7 in", "≤ 7.0 in"],
            ["≤ 6.1 in", "≤ 6.7 in", "≤ 7.0 in"]
        ),
        ["FIELDWORK-FIRST-AID-KIT"] = new Specification(
            "pack",
            ["12 pieces", "24 pieces", "36 pieces"],
            ["12 dele", "24 dele", "36 dele"]
        ),
        ["FIELDWORK-CUP-SET"] =
            new Specification("pack", ["2 cups", "4 cups", "6 cups"], ["2 kopper", "4 kopper", "6 kopper"]),
        ["FIELDWORK-TENT-PEG-SET"] = new Specification(
            "pack",
            ["6 pegs", "10 pegs", "16 pegs"],
            ["6 pløkker", "10 pløkker", "16 pløkker"]
        ),
        ["FIELDWORK-REPAIR-KIT"] = new Specification("pack", ["4 pieces", "8 pieces"], ["4 dele", "8 dele"]),
        ["FIELDWORK-PACK-COVER"] = new Specification("capacity", ["20–35 L", "45–65 L"], ["20–35 L", "45–65 L"]),
        ["FIELDWORK-TREKKING-TENT"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-CAMP-HAMMOCK"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-TENT-FOOTPRINT"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-CAMP-STOVE"] = new Specification("pack", ["1 burner", "2 burners"], ["1 blus", "2 blus"]),
        ["FIELDWORK-COOKING-POT"] = new Specification("capacity", ["1.2 L", "2 L"], ["1.2 L", "2 L"]),
        ["FIELDWORK-COOKWARE"] = new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"])
    };

    private sealed record Specification(string Kind, string[] English, string[] Danish);
}
