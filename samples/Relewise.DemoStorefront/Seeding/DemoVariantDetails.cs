using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Seeding;

internal static class DemoVariantDetails
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        var types = services.GetRequiredService<IContentTypeService>();
        IDataType text =
            (await services.GetRequiredService<IDataTypeService>().GetAllAsync()).First(type =>
                type.Name == "Textstring");
        var strings = services.GetRequiredService<IShortStringHelper>();
        IContentType productType = types.Get(DocumentTypeAliases.Product)!;
        var productSchemaChanged = false;

        foreach (string alias in new[] { "optionType", PropertyAliases.SizeGuide })
        {
            if (!productType.PropertyTypes.Any(property => property.Alias == alias))
            {
                productType.AddPropertyType(
                    new PropertyType(strings, text, alias)
                    {
                        Name = alias == "optionType" ? "Variant option type" : "Size guide",
                        Variations = alias == PropertyAliases.SizeGuide
                            ? ContentVariation.Culture
                            : ContentVariation.Nothing
                    });
                productSchemaChanged = true;
            }
        }

        if (productSchemaChanged)
        {
            await types.UpdateAsync(productType, Constants.Security.SuperUserKey);
        }

        IContentType variantType = types.Get(DocumentTypeAliases.ProductVariant)!;
        var schemaChanged = false;

        foreach (string alias in new[] { PropertyAliases.OptionLabel, PropertyAliases.Capacity })
        {
            if (!variantType.PropertyTypes.Any(property => property.Alias == alias))
            {
                variantType.AddPropertyType(
                    new PropertyType(strings, text, alias)
                    {
                        Name = alias == PropertyAliases.OptionLabel ? "Option label" : "Capacity",
                        Variations = alias == PropertyAliases.OptionLabel
                            ? ContentVariation.Culture
                            : ContentVariation.Nothing
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
                     node.ContentType.Alias == DocumentTypeAliases.Product && node.Published && !node.Edited))
        {
            if (!Specifications.TryGetValue(
                product.GetValue<string>(PropertyAliases.Sku) ?? "",
                out Specification? spec))
            {
                continue;
            }

            var productChanged = false;

            if (string.IsNullOrWhiteSpace(product.GetValue<string>("optionType")))
            {
                product.SetValue("optionType", spec.Kind);
                productChanged = true;
            }

            if (spec.Kind == PropertyAliases.Capacity && (string.IsNullOrWhiteSpace(
                product.GetValue<string>(PropertyAliases.Capacity)) ||
                                            (product.GetValue<string>(PropertyAliases.Sku) == "FIELDWORK-COOKING-POT" &&
                                             product.GetValue<string>(PropertyAliases.Capacity) == "1.5 L")))
            {
                product.SetValue(PropertyAliases.Capacity, string.Join(" / ", spec.English));
                productChanged = true;
            }

            if (product.GetValue<string>(PropertyAliases.Sku) == "FIELDWORK-HIKING-GLOVES")
            {
                foreach (string culture in product.PublishedCultures)
                {
                    if (culture is "en-US" or "da-DK" &&
                        string.IsNullOrWhiteSpace(product.GetValue<string>(PropertyAliases.SizeGuide, culture)))
                    {
                        product.SetValue(
                            PropertyAliases.SizeGuide,
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
                node.ParentId == product.Id && node.ContentType.Alias == DocumentTypeAliases.ProductVariant);
            string[] suffixes = spec.English.Length == 3
                ? ["SMALL", "MEDIUM", "LARGE"]
                : product.GetValue<string>(PropertyAliases.Sku) is "FIELDWORK-REPAIR-KIT" or "FIELDWORK-PACK-COVER"
                    ? ["SMALL", "LARGE"]
                    : ["SOLO", "DUO"];

            foreach (IContent variant in children.Where(node => node.Published && !node.Edited))
            {
                string sku = variant.GetValue<string>(PropertyAliases.Sku) ?? "";
                int index = Array.FindIndex(
                    suffixes,
                    suffix => sku == product.GetValue<string>(PropertyAliases.Sku) + "-" + suffix);

                if (index < 0)
                {
                    continue;
                }

                var changed = false;

                foreach (string culture in variant.PublishedCultures.ToArray())
                {
                    if (culture is not ("en-US" or "da-DK"))
                    {
                        continue;
                    }

                    var label = variant.GetValue<string>(PropertyAliases.OptionLabel, culture);

                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = culture == "en-US" ? spec.English[index] : spec.Danish[index];
                        variant.SetValue(PropertyAliases.OptionLabel, label, culture);
                        changed = true;
                    }

                    string parentName = product.GetCultureName(culture) ?? product.Name ?? string.Empty;
                    string previousHeading = parentName + " " + variant.GetValue<string>(PropertyAliases.Size, culture);
                    string heading = (product.GetValue<string>(
                        PropertyAliases.Heading,
                        culture) ?? parentName) + " · " + label;

                    if (variant.GetValue<string>(PropertyAliases.Heading, culture) == previousHeading
                        && heading != previousHeading)
                    {
                        variant.SetValue(PropertyAliases.Heading, heading, culture);
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                if (spec.Kind == PropertyAliases.Capacity
                    && string.IsNullOrWhiteSpace(variant.GetValue<string>(PropertyAliases.Capacity)))
                {
                    variant.SetValue(PropertyAliases.Capacity, spec.English[index]);
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
            new Specification(PropertyAliases.Capacity, ["1.5 L", "2 L", "3 L"], ["1.5 L", "2 L", "3 L"]),
        ["FIELDWORK-PACKING-CUBES"] = new Specification(
            PropertyAliases.Capacity,
            ["3 L", "6 L", "10 L"],
            ["3 L", "6 L", "10 L"]),
        ["FIELDWORK-COMPRESSION-SACK"] =
            new Specification(PropertyAliases.Capacity, ["5 L", "10 L", "15 L"], ["5 L", "10 L", "15 L"]),
        ["FIELDWORK-FRAME-BAG"] = new Specification(
            PropertyAliases.Capacity,
            ["2 L", "3 L", "4 L"],
            ["2 L", "3 L", "4 L"]),
        ["FIELDWORK-WATER-CARRIER"] = new Specification(
            PropertyAliases.Capacity,
            ["5 L", "10 L", "15 L"],
            ["5 L", "10 L", "15 L"]),
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
            PropertyAliases.Size,
            ["EU 36–39", "EU 40–43", "EU 44–47"],
            ["EU 36–39", "EU 40–43", "EU 44–47"]),
        ["FIELDWORK-RAIN-GAITERS"] = new Specification(
            PropertyAliases.Size,
            ["EU 36–39", "EU 40–43", "EU 44–47"],
            ["EU 36–39", "EU 40–43", "EU 44–47"]
        ),
        ["FIELDWORK-HIKING-GLOVES"] = new Specification(
            PropertyAliases.Size,
            ["S · 18–19 cm", "M · 20–21 cm", "L · 22–23 cm"],
            ["S · 18–19 cm", "M · 20–21 cm", "L · 22–23 cm"]
        ),
        ["FIELDWORK-PHONE-POUCH"] = new Specification(
            PropertyAliases.Size,
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
        ["FIELDWORK-PACK-COVER"] = new Specification(
            PropertyAliases.Capacity,
            ["20–35 L", "45–65 L"],
            ["20–35 L", "45–65 L"]),
        ["FIELDWORK-TREKKING-TENT"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-CAMP-HAMMOCK"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-TENT-FOOTPRINT"] =
            new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"]),
        ["FIELDWORK-CAMP-STOVE"] = new Specification("pack", ["1 burner", "2 burners"], ["1 blus", "2 blus"]),
        ["FIELDWORK-COOKING-POT"] = new Specification(PropertyAliases.Capacity, ["1.2 L", "2 L"], ["1.2 L", "2 L"]),
        ["FIELDWORK-COOKWARE"] = new Specification("occupancy", ["1 person", "2 people"], ["1 person", "2 personer"])
    };

    private sealed record Specification(string Kind, string[] English, string[] Danish);
}
