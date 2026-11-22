using Umbraco.Cms.Core;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

internal static class DemoCatalogue
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        var content = services.GetRequiredService<IContentService>();
        var types = services.GetRequiredService<IContentTypeService>();
        var variantType = types.Get("productVariant")!;
        if (!variantType.PropertyTypes.Any(property => property.Alias == "size"))
        {
            var dataTypes = services.GetRequiredService<IDataTypeService>();
            var text = (await dataTypes.GetAllAsync()).First(type => type.Name == "Textstring");
            variantType.AddPropertyType(new PropertyType(services.GetRequiredService<IShortStringHelper>(), text, "size") { Name = "Size" });
            await types.UpdateAsync(variantType, Constants.Security.SuperUserKey);
        }

        using var scope = services.GetRequiredService<IScopeProvider>().CreateScope();
        scope.EagerWriteLock(Constants.Locks.ContentTree);
        var nodes = content.GetRootContent()
            .SelectMany(root => new[] { root }.Concat(content.GetPagedDescendants(root.Id, 0, 1000, out _))).ToList();
        var equipment = nodes.Single(node => node.ContentType.Alias == "productCategory" && node.GetValue<string>("categoryId") == "U19-SMOKE-EQUIPMENT");
        var brand = nodes.Single(node => node.ContentType.Alias == "brand" && node.GetValue<string>("brandId") == "U19-SMOKE-BRAND");
        int template = types.Get("product")!.AllowedTemplates!.Single().Id;
        Guid prices = types.Get("currencyPrice")!.Key;

        void Publish(IContent node)
        {
            if (node.ContentType.VariesByCulture() && string.IsNullOrWhiteSpace(node.GetCultureName("en-US")))
            {
                node.SetCultureName(node.Name, "en-US");
            }

            content.Save(node);
            if (!content.Publish(node, ["*"]).Success)
            {
                throw new InvalidOperationException($"Failed publishing catalogue item '{node.Name}'.");
            }

            if (nodes.All(existing => existing.Key != node.Key))
            {
                nodes.Add(node);
            }
        }

        var brands = new Dictionary<string, IContent> { ["U19-SMOKE-BRAND"] = brand };
        bool distributeExisting = !nodes.Any(node => node.ContentType.Alias == "brand" &&
            node.GetValue<string>("brandId") is "FIELDWORK-RIDGEWAY" or "FIELDWORK-EMBERCAMP");
        foreach (var (id, name, description) in new[]
        {
            ("FIELDWORK-RIDGEWAY", "Ridgeway", "Practical packs and equipment for days on the trail."),
            ("FIELDWORK-EMBERCAMP", "Embercamp", "Thoughtful essentials for relaxed days outdoors.")
        })
        {
            var additionalBrand = nodes.FirstOrDefault(node => node.ContentType.Alias == "brand" && node.GetValue<string>("brandId") == id);
            if (additionalBrand == null)
            {
                additionalBrand = content.Create(name, brand.ParentId, "brand");
                additionalBrand.TemplateId = brand.TemplateId;
                additionalBrand.SetValue("brandId", id);
                SetText(additionalBrand, "heading", name);
                SetText(additionalBrand, "bodyText", description);
                Publish(additionalBrand);
            }
            brands.Add(id, additionalBrand);
        }

        var categories = new Dictionary<string, IContent>();
        foreach (var (code, name) in new[] { ("packs", "Packs and bags"), ("drinkware", "Drinkware"), ("camping", "Camping"), ("accessories", "Accessories") })
        {
            string id = "FIELDWORK-" + code.ToUpperInvariant();
            var category = nodes.FirstOrDefault(node => node.ContentType.Alias == "productCategory" && node.GetValue<string>("categoryId") == id);
            if (category == null)
            {
                category = content.Create(name, equipment.Id, "productCategory");
                category.TemplateId = template;
                SetText(category, "heading", name);
                category.SetValue("categoryId", id);
                Publish(category);
            }

            categories.Add(code, category);
        }

        foreach (ProductSeed item in Products)
        {
            var product = nodes.FirstOrDefault(node => node.ContentType.Alias == "product" && node.GetValue<string>("sku") == item.Sku);
            if (product == null)
            {
                product = content.Create(item.Name, categories[item.Category].Id, "product");
                product.TemplateId = template;
                SetText(product, "heading", item.Name);
                product.SetValue("sku", item.Sku);
                SetText(product, "bodyText", item.Description);
                product.SetValue("heroImage", "/images/" + item.Image + ".svg");
                product.SetValue("brand", Udi.Create(Constants.UdiEntityType.Document, brands[BrandIdForSku(item.Sku)].Key).ToString());
                product.SetValue("stock", item.Stock ?? 10 + categories[item.Category].Level * 5);
                if (item.AdditionalCategory != null)
                {
                    product.SetValue("extraCategories", System.Text.Json.JsonSerializer.Serialize(new[] { categories[item.AdditionalCategory].Key }));
                }
                product.SetValue("listPrice", DemoBlockValues.Prices(prices, ("DKK", item.Dkk), ("EUR", item.Eur), ("USD", item.Usd)));
                product.SetValue("salesPrice", DemoBlockValues.Prices(prices,
                    ("DKK", SalesPrice(item, "DKK", item.Dkk)),
                    ("EUR", SalesPrice(item, "EUR", item.Eur)),
                    ("USD", SalesPrice(item, "USD", item.Usd))));
                Publish(product);
            }

            // Upgrade the original single-Brand seed once. Never publish drafts or replace a custom Brand choice.
            if (distributeExisting && product.Published && !product.Edited &&
                product.AvailableCultures.All(culture => product.PublishedCultures.Contains(culture)) &&
                product.GetValue<string>("brand") == Udi.Create(Constants.UdiEntityType.Document, brand.Key).ToString() &&
                BrandIdForSku(item.Sku) != "U19-SMOKE-BRAND")
            {
                product.SetValue("brand", Udi.Create(Constants.UdiEntityType.Document, brands[BrandIdForSku(item.Sku)].Key).ToString());
                Publish(product);
            }

            if (product.ParentId == equipment.Id)
            {
                var moved = content.Move(product, categories[item.Category].Id);
                if (!moved.Success)
                {
                    throw new InvalidOperationException($"Failed placing '{product.Name}' in its category: {moved.Result}.");
                }
            }

            if (!product.Edited && product.Published &&
                item.Sku is "FIELDWORK-SUMMIT-PACK" or "FIELDWORK-HIP-PACK" or "FIELDWORK-CAMP-CHAIR" or "FIELDWORK-LANTERN")
            {
                var currentPrices = StorefrontProductIndexer.ReadPrices(product.GetValue<string>("salesPrice"));
                if (currentPrices.GetValueOrDefault("DKK") == item.Dkk &&
                    currentPrices.GetValueOrDefault("EUR") == item.Eur &&
                    currentPrices.GetValueOrDefault("USD") == item.Usd)
                {
                    product.SetValue("salesPrice", DemoBlockValues.Prices(prices,
                        ("DKK", decimal.Round(item.Dkk * .8m, 2)),
                        ("EUR", decimal.Round(item.Eur * .8m, 2)),
                        ("USD", decimal.Round(item.Usd * .8m, 2))));
                    Publish(product);
                }
            }

            // Stable SKUs make startup additive: keep edits to existing Products and Variants.
            foreach (VariantSeed option in item.Variants)
            {
                string sku = item.Sku + "-" + option.Label.Replace(' ', '-').ToUpperInvariant();
                if (nodes.Any(node => node.ContentType.Alias == "productVariant" && node.GetValue<string>("sku") == sku))
                {
                    continue;
                }

                var variant = content.Create(item.Name + " " + option.Label, product.Id, "productVariant");
                variant.TemplateId = template;
                SetText(variant, "heading", item.Name + " " + option.Label);
                variant.SetValue("sku", sku);
                SetText(variant, "bodyText", item.Description + " Available in " + option.Label + ".");
                SetText(variant, "color", option.Color);
                SetText(variant, "size", option.Size);
                Publish(variant);
            }
        }

        scope.Complete();
    }

    internal static string BrandIdForSku(string sku) => sku.StartsWith("U19-SMOKE-", StringComparison.Ordinal)
        ? "U19-SMOKE-BRAND"
        : (sku.Sum(character => character) % 3) switch
        {
            0 => "U19-SMOKE-BRAND",
            1 => "FIELDWORK-RIDGEWAY",
            _ => "FIELDWORK-EMBERCAMP"
        };

    private static decimal SalesPrice(ProductSeed item, string currency, decimal amount)
    {
        bool discounted = item.OfferCurrency == currency ||
            item.Sku is "FIELDWORK-SUMMIT-PACK" or "FIELDWORK-HIP-PACK" or "FIELDWORK-CAMP-CHAIR" or "FIELDWORK-LANTERN";
        return discounted ? decimal.Round(amount * .8m, 2) : amount;
    }

    private static void SetText(IContent node, string alias, string? value)
        => node.SetValue(alias, value, node.ContentType.VariesByCulture() ? "en-US" : null);

    private static VariantSeed[] Colors(params string[] colors) => colors.Select(color => new VariantSeed(color, color, null)).ToArray();
    private static VariantSeed[] Sizes(params string[] sizes) => sizes.Select(size => new VariantSeed(size, null, size)).ToArray();

    private static readonly ProductSeed[] Products =
    [
        new(
            "U19-SMOKE-PACK",
            "Trail daypack",
            "packs",
            899m,
            119.95m,
            129.95m,
            "A lightweight daypack for trails, city commutes and everything between.",
            "daypack",
            Colors("Forest", "Sand")),
        new(
            "FIELDWORK-SUMMIT-PACK",
            "Summit hiking pack",
            "packs",
            1299m,
            174.95m,
            189.95m,
            "A roomy hiking backpack with a supportive hip belt for full days on the mountain.",
            "daypack",
            Colors("Forest", "Slate", "Sand")),
        new(
            "FIELDWORK-RIDGE-PACK",
            "Ridge travel pack",
            "packs",
            1099m,
            149.95m,
            159.95m,
            "A versatile travel backpack with organised pockets for weekends away.",
            "daypack",
            Colors("Slate", "Sand", "Rust")),
        new(
            "FIELDWORK-HIP-PACK",
            "Trail hip pack",
            "packs",
            349m,
            46.95m,
            49.95m,
            "Keep your walking essentials close in this compact waist bag.",
            "daypack",
            Colors("Forest", "Slate")),
        new(
            "FIELDWORK-SLING",
            "Compact sling bag",
            "packs",
            449m,
            59.95m,
            64.95m,
            "A small shoulder bag for your camera, notebook and everyday essentials.",
            "daypack",
            Colors("Sand", "Slate")),
        new(
            "FIELDWORK-DRY-BAG",
            "Waterproof dry bag",
            "packs",
            199m,
            26.95m,
            29.95m,
            "A roll-top bag that keeps spare layers dry on wet walks and paddling trips.",
            "daypack",
            Colors("Forest", "Rust")),
        new(
            "U19-SMOKE-FLASK",
            "Trail flask",
            "drinkware",
            299m,
            39.95m,
            44.95m,
            "Keep your coffee warm and your water cool, from first light to late evening.",
            "flask",
            Colors("Forest", "Slate", "Copper")),
        new(
            "FIELDWORK-WATER-BOTTLE",
            "Trail water bottle",
            "drinkware",
            179m,
            24.95m,
            26.95m,
            "A reusable water bottle with an easy-carry loop for walks and workouts.",
            "flask",
            Colors("Forest", "Slate", "Sand")),
        new(
            "FIELDWORK-CAMP-MUG",
            "Insulated camp mug",
            "drinkware",
            229m,
            30.95m,
            34.95m,
            "An insulated mug for coffee by the tent or tea on the trail.",
            "mug",
            Colors("Forest", "Copper", "Sand")),
        new(
            "FIELDWORK-TUMBLER",
            "Coffee travel tumbler",
            "drinkware",
            249m,
            34.95m,
            36.95m,
            "A leak-resistant travel cup that fits your daily commute and weekend plans.",
            "mug",
            Colors("Slate", "Copper")),
        new(
            "FIELDWORK-HYDRATION-BOTTLE",
            "Lightweight hydration bottle",
            "drinkware",
            129m,
            17.95m,
            19.95m,
            "A lightweight drinking bottle for short runs and long summer walks.",
            "flask",
            Colors("Sand", "Forest")),
        new(
            "FIELDWORK-FOOD-FLASK",
            "Wide-mouth food flask",
            "drinkware",
            329m,
            44.95m,
            47.95m,
            "Carry a warm lunch in this insulated food jar with a wide opening.",
            "flask",
            Colors("Copper", "Slate")),
        new(
            "U19-SMOKE-SHELTER",
            "Camp shelter",
            "camping",
            1999m,
            269.95m,
            289.95m,
            "Simple shelter, thoughtful details. A place to pause on your next adventure.",
            "shelter",
            Colors("Moss", "Sand", "Slate")),
        new(
            "FIELDWORK-TARP",
            "Woodland camping tarp",
            "camping",
            699m,
            94.95m,
            99.95m,
            "A versatile tarp for a sheltered lunch stop or an open-air campsite.",
            "shelter",
            Colors("Moss", "Sand", "Slate")),
        new(
            "FIELDWORK-SLEEPING-BAG",
            "Weekend sleeping bag",
            "camping",
            899m,
            119.95m,
            129.95m,
            "A comfortable sleeping bag for mild nights under canvas.",
            "sleeping-bag",
            Colors("Moss", "Rust", "Slate")),
        new(
            "FIELDWORK-CAMP-CHAIR",
            "Folding camp chair",
            "camping",
            499m,
            66.95m,
            74.95m,
            "A folding chair for slow evenings around the campsite.",
            "chair",
            Colors("Forest", "Sand")),
        new(
            "FIELDWORK-SLEEPING-MAT",
            "Lightweight sleeping mat",
            "camping",
            549m,
            74.95m,
            79.95m,
            "An insulated sleeping pad that packs down for overnight adventures.",
            "sleeping-bag",
            Colors("Moss", "Slate")),
        new(
            "FIELDWORK-LANTERN",
            "Compact camp lantern",
            "camping",
            299m,
            39.95m,
            44.95m,
            "A rechargeable lantern with soft light for the tent and picnic table.",
            "lantern",
            Colors("Forest", "Copper")),
        new(
            "FIELDWORK-POLES",
            "Trekking poles",
            "accessories",
            599m,
            79.95m,
            89.95m,
            "Adjustable walking poles for steady steps on uneven ground.",
            "accessories",
            Colors("Slate", "Forest", "Copper")),
        new(
            "FIELDWORK-SOCKS",
            "Merino hiking socks",
            "accessories",
            149m,
            19.95m,
            22.95m,
            "Soft merino wool socks for warm feet and comfortable hiking.",
            "accessories",
            Sizes("Small", "Medium", "Large")),
        new(
            "FIELDWORK-CAP",
            "Trail walking cap",
            "accessories",
            199m,
            26.95m,
            29.95m,
            "A breathable cap for sunny trails and everyday exploring.",
            "accessories",
            Colors("Forest", "Sand", "Slate")),
        new(
            "FIELDWORK-PACK-COVER",
            "Waterproof pack cover",
            "accessories",
            179m,
            24.95m,
            26.95m,
            "A lightweight rain cover that protects your backpack on wet days.",
            "daypack",
            Sizes("Small", "Large")),
        new(
            "FIELDWORK-COOKWARE",
            "Camp cookware set",
            "accessories",
            399m,
            54.95m,
            59.95m,
            "A nesting pot and cup set for simple meals outdoors.",
            "mug",
            Sizes("Solo", "Duo")),
        new(
            "FIELDWORK-TOWEL",
            "Quick-dry travel towel",
            "accessories",
            129m,
            17.95m,
            19.95m,
            "A compact towel for wild swims, camping and travel.",
            "accessories",
            Sizes("Small", "Medium", "Large")),
        new(
            "FIELDWORK-PICNIC-BLANKET",
            "Packable picnic blanket",
            "camping",
            400m,
            50m,
            50m,
            "A packable blanket for campsite breakfasts and picnic stops. Only two left; discounted in Europe only.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 2,
            AdditionalCategory: "accessories",
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-REPAIR-KIT",
            "Trail repair kit",
            "accessories",
            149m,
            19.95m,
            22.95m,
            "A compact repair kit for packs and camping equipment. Currently out of stock; discounted in the US only.",
            "accessories",
            Sizes("Small", "Large"),
            Stock: 0,
            AdditionalCategory: "camping",
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-HYDRATION-PACK",
            "Hydration running pack",
            "packs",
            599m,
            79.95m,
            89.95m,
            "A close-fitting running pack with space for water and a light layer.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 0,
            AdditionalCategory: "accessories",
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-OVERNIGHT-PACK",
            "Overnight trekking pack",
            "packs",
            1499m,
            199.95m,
            219.95m,
            "A supportive trekking pack for overnight walks with tent and sleeping gear.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 13),
        new(
            "FIELDWORK-EXPEDITION-PACK",
            "Expedition trekking pack",
            "packs",
            1899m,
            254.95m,
            279.95m,
            "A high-capacity pack for longer routes and bulky cold-weather equipment.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 14),
        new(
            "FIELDWORK-FOLDABLE-PACK",
            "Foldaway daypack",
            "packs",
            249m,
            34.95m,
            39.95m,
            "A foldaway daypack that fits inside your main travel bag.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 15,
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-WEEKEND-DUFFEL",
            "Weekend duffel bag",
            "packs",
            699m,
            94.95m,
            104.95m,
            "A wide-opening duffel for weekend clothing and outdoor equipment.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5),
        new(
            "FIELDWORK-BASECAMP-DUFFEL",
            "Basecamp equipment duffel",
            "packs",
            999m,
            134.95m,
            149.95m,
            "A tough equipment bag with grab handles for the journey to basecamp.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 17,
            AdditionalCategory: "accessories"),
        new(
            "FIELDWORK-PACKING-CUBES",
            "Travel packing cubes",
            "packs",
            199m,
            26.95m,
            29.95m,
            "Keep spare clothing organised with lightweight zipped packing cubes.",
            "daypack",
            Sizes("Small", "Medium", "Large"),
            Stock: 18,
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-COMPRESSION-SACK",
            "Sleeping bag compression sack",
            "packs",
            149m,
            19.95m,
            22.95m,
            "Compression straps reduce the packed volume of a sleeping bag or spare layers.",
            "daypack",
            Sizes("Small", "Medium", "Large"),
            Stock: 2),
        new(
            "FIELDWORK-ROLLTOP-PACK",
            "Rolltop commuter pack",
            "packs",
            799m,
            109.95m,
            119.95m,
            "A rolltop backpack with a padded laptop sleeve for everyday journeys.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5),
        new(
            "FIELDWORK-CAMERA-PACK",
            "Trail camera pack",
            "packs",
            1199m,
            159.95m,
            174.95m,
            "Padded dividers keep camera equipment organised on trails and city walks.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 0,
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-MAP-POUCH",
            "Weatherproof map pouch",
            "packs",
            129m,
            17.95m,
            19.95m,
            "A clear-front pouch protects route notes and maps from passing showers.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 22,
            AdditionalCategory: "accessories"),
        new(
            "FIELDWORK-TOILETRY-BAG",
            "Hanging wash bag",
            "packs",
            179m,
            24.95m,
            27.95m,
            "A hanging wash bag keeps small essentials together at the campsite.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 23),
        new(
            "FIELDWORK-TRAVEL-WALLET",
            "Trail travel wallet",
            "packs",
            99m,
            13.95m,
            15.95m,
            "A slim zipped wallet for travel cards and campsite essentials.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5,
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-BIKE-PANNIER",
            "Touring bike pannier",
            "packs",
            649m,
            89.95m,
            99.95m,
            "A rolltop pannier carries clothing and lunch on touring bike rides.",
            "daypack",
            Colors("Forest", "Sand", "Slate"),
            Stock: 25),
        new(
            "FIELDWORK-FRAME-BAG",
            "Bike frame bag",
            "packs",
            349m,
            49.95m,
            54.95m,
            "A compact frame bag keeps repair tools and snacks within easy reach.",
            "daypack",
            Sizes("Small", "Medium", "Large"),
            Stock: 2),
        new(
            "FIELDWORK-ESPRESSO-CUP",
            "Camp espresso cup",
            "drinkware",
            89m,
            11.95m,
            13.95m,
            "A small stainless cup for morning espresso beside the tent.",
            "mug",
            Colors("Forest", "Sand", "Slate"),
            Stock: 27,
            AdditionalCategory: "camping",
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-ENAMEL-MUG",
            "Classic enamel mug",
            "drinkware",
            119m,
            15.95m,
            17.95m,
            "A traditional enamel mug for tea breaks and fireside coffee.",
            "mug",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5),
        new(
            "FIELDWORK-FOLDING-CUP",
            "Folding trail cup",
            "drinkware",
            79m,
            10.95m,
            12.95m,
            "A folding cup slips into a jacket pocket for quick refreshment stops.",
            "mug",
            Colors("Forest", "Sand", "Slate"),
            Stock: 12),
        new(
            "FIELDWORK-LARGE-THERMOS",
            "All-day vacuum flask",
            "drinkware",
            449m,
            59.95m,
            64.95m,
            "A large vacuum flask shares warm drinks with friends on long outings.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 0,
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-KIDS-BOTTLE",
            "Junior adventure bottle",
            "drinkware",
            139m,
            18.95m,
            21.95m,
            "A compact drinking bottle with a carry loop for younger explorers.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 14),
        new(
            "FIELDWORK-FILTER-BOTTLE",
            "Trail filter bottle",
            "drinkware",
            499m,
            69.95m,
            74.95m,
            "A drinking bottle with a replaceable filter for outdoor travel.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5,
            AdditionalCategory: "camping"),
        new(
            "FIELDWORK-STEEL-BOTTLE",
            "Single-wall steel bottle",
            "drinkware",
            199m,
            26.95m,
            29.95m,
            "A single-wall steel bottle for cold drinks on everyday walks.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 2,
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-HYDRATION-RESERVOIR",
            "Pack hydration reservoir",
            "drinkware",
            249m,
            34.95m,
            39.95m,
            "A flexible water reservoir fits inside hydration-compatible backpacks.",
            "flask",
            Sizes("Small", "Medium", "Large"),
            Stock: 17),
        new(
            "FIELDWORK-TEA-FLASK",
            "Infuser tea flask",
            "drinkware",
            279m,
            37.95m,
            42.95m,
            "Brew loose-leaf tea directly in an insulated flask with a removable infuser.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 18),
        new(
            "FIELDWORK-CAMP-JUG",
            "Insulated camp jug",
            "drinkware",
            599m,
            79.95m,
            89.95m,
            "An insulated jug keeps drinks ready for the whole group at basecamp.",
            "flask",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5,
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-CUP-SET",
            "Nesting camping cups",
            "drinkware",
            169m,
            22.95m,
            25.95m,
            "Nesting cups save space when packing shared camp kitchen equipment.",
            "mug",
            Sizes("Small", "Medium", "Large"),
            Stock: 20,
            AdditionalCategory: "camping"),
        new(
            "FIELDWORK-WATER-CARRIER",
            "Collapsible water carrier",
            "drinkware",
            229m,
            30.95m,
            34.95m,
            "Carry water back to camp in a container that folds flat when empty.",
            "flask",
            Sizes("Small", "Medium", "Large"),
            Stock: 21),
        new(
            "FIELDWORK-TREKKING-TENT",
            "Lightweight trekking tent",
            "camping",
            2499m,
            334.95m,
            369.95m,
            "A lightweight tent with a sheltered entrance for overnight walking routes.",
            "shelter",
            Sizes("Solo", "Duo"),
            Stock: 0,
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-FAMILY-TENT",
            "Family basecamp tent",
            "camping",
            3999m,
            539.95m,
            589.95m,
            "A spacious family tent with room to gather on rainy camping days.",
            "shelter",
            Colors("Forest", "Sand", "Slate"),
            Stock: 2),
        new(
            "FIELDWORK-BIVVY-BAG",
            "Minimalist bivvy bag",
            "camping",
            799m,
            109.95m,
            119.95m,
            "A compact bivvy shelter for simple nights beneath the stars.",
            "sleeping-bag",
            Colors("Forest", "Sand", "Slate"),
            Stock: 24),
        new(
            "FIELDWORK-CAMP-HAMMOCK",
            "Packable camp hammock",
            "camping",
            549m,
            74.95m,
            84.95m,
            "A packable hammock with tree straps for quiet afternoons outdoors.",
            "shelter",
            Sizes("Solo", "Duo"),
            Stock: 25,
            AdditionalCategory: "accessories",
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-CAMP-PILLOW",
            "Inflatable camp pillow",
            "camping",
            199m,
            26.95m,
            29.95m,
            "An inflatable pillow adds a little comfort without filling your pack.",
            "sleeping-bag",
            Colors("Forest", "Sand", "Slate"),
            Stock: 26),
        new(
            "FIELDWORK-FOAM-MAT",
            "Folding foam sleeping mat",
            "camping",
            299m,
            39.95m,
            44.95m,
            "A folding foam mat provides simple insulation for campsites and lunch stops.",
            "sleeping-bag",
            Sizes("Small", "Medium", "Large"),
            Stock: 5),
        new(
            "FIELDWORK-WINTER-SLEEPING-BAG",
            "Cold-weather sleeping bag",
            "camping",
            1599m,
            214.95m,
            239.95m,
            "A generously insulated sleeping bag for colder nights at camp.",
            "sleeping-bag",
            Sizes("Small", "Medium", "Large"),
            Stock: 28,
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-CAMP-TABLE",
            "Folding camp table",
            "camping",
            699m,
            94.95m,
            104.95m,
            "A folding table creates a steady place for camp meals and maps.",
            "chair",
            Sizes("Small", "Medium", "Large"),
            Stock: 12),
        new(
            "FIELDWORK-CAMP-STOOL",
            "Compact tripod stool",
            "camping",
            179m,
            24.95m,
            27.95m,
            "A compact tripod stool offers a quick seat on walks and fishing trips.",
            "chair",
            Colors("Forest", "Sand", "Slate"),
            Stock: 2,
            AdditionalCategory: "accessories"),
        new(
            "FIELDWORK-CAMP-STOVE",
            "Compact camping stove",
            "camping",
            449m,
            59.95m,
            64.95m,
            "A compact stove for preparing warm meals at the campsite.",
            "mug",
            Sizes("Solo", "Duo"),
            Stock: 0,
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-COOKING-POT",
            "Lightweight camping pot",
            "camping",
            299m,
            39.95m,
            44.95m,
            "A lightweight cooking pot with folding handles for the camp kitchen.",
            "mug",
            Sizes("Solo", "Duo"),
            Stock: 15),
        new(
            "FIELDWORK-CAMP-KETTLE",
            "Trail camping kettle",
            "camping",
            249m,
            34.95m,
            39.95m,
            "A compact kettle makes shared tea breaks easy on outdoor trips.",
            "mug",
            Colors("Forest", "Sand", "Slate"),
            Stock: 16),
        new(
            "FIELDWORK-SOLAR-LIGHT",
            "Solar campsite light",
            "camping",
            349m,
            46.95m,
            49.95m,
            "A rechargeable campsite light with a solar panel for sunny rest days.",
            "lantern",
            Colors("Forest", "Sand", "Slate"),
            Stock: 17,
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-TENT-FOOTPRINT",
            "Protective tent footprint",
            "camping",
            399m,
            54.95m,
            59.95m,
            "A groundsheet protects the tent floor from rough ground and damp grass.",
            "shelter",
            Sizes("Solo", "Duo"),
            Stock: 5,
            AdditionalCategory: "accessories"),
        new(
            "FIELDWORK-TENT-PEG-SET",
            "Lightweight tent peg set",
            "camping",
            99m,
            13.95m,
            15.95m,
            "Spare tent pegs help secure your shelter on the next camping trip.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 19),
        new(
            "FIELDWORK-HIKING-GLOVES",
            "Lightweight hiking gloves",
            "accessories",
            249m,
            34.95m,
            39.95m,
            "Light gloves keep hands comfortable on cool early-morning walks.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 2,
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-NECK-WARMER",
            "Merino neck warmer",
            "accessories",
            179m,
            24.95m,
            27.95m,
            "A soft merino neck warmer adds versatile warmth on windy trails.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 21),
        new(
            "FIELDWORK-TRAIL-BEANIE",
            "Ribbed trail beanie",
            "accessories",
            149m,
            19.95m,
            22.95m,
            "A warm ribbed hat for cold campsites and winter walks.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5),
        new(
            "FIELDWORK-RAIN-GAITERS",
            "Waterproof hiking gaiters",
            "accessories",
            299m,
            39.95m,
            44.95m,
            "Light gaiters help keep wet grass and trail debris out of your boots.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 0,
            AdditionalCategory: "camping",
            OfferCurrency: "DKK"),
        new(
            "FIELDWORK-HEADLAMP",
            "Rechargeable trail headlamp",
            "accessories",
            399m,
            54.95m,
            59.95m,
            "A rechargeable headlamp leaves both hands free on dark trails.",
            "lantern",
            Colors("Forest", "Sand", "Slate"),
            Stock: 24),
        new(
            "FIELDWORK-FIRST-AID-KIT",
            "Outdoor first-aid kit",
            "accessories",
            229m,
            30.95m,
            34.95m,
            "A compact organiser keeps basic first-aid supplies together on trips.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 25),
        new(
            "FIELDWORK-TRAIL-COMPASS",
            "Baseplate trail compass",
            "accessories",
            199m,
            26.95m,
            29.95m,
            "A baseplate compass pairs with your paper map for route planning.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5,
            OfferCurrency: "EUR"),
        new(
            "FIELDWORK-BINOCULARS",
            "Compact trail binoculars",
            "accessories",
            899m,
            119.95m,
            134.95m,
            "Compact binoculars bring distant wildlife and landscape details closer.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 2),
        new(
            "FIELDWORK-PHONE-POUCH",
            "Weatherproof phone pouch",
            "accessories",
            129m,
            17.95m,
            19.95m,
            "A clear phone pouch adds protection during wet walks and paddling trips.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 28,
            AdditionalCategory: "camping"),
        new(
            "FIELDWORK-TRAIL-SUNGLASSES",
            "Wraparound trail sunglasses",
            "accessories",
            349m,
            46.95m,
            49.95m,
            "Light wraparound sunglasses stay comfortable on active outdoor days.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 12,
            OfferCurrency: "USD"),
        new(
            "FIELDWORK-TRAIL-SIT-PAD",
            "Folding trail sit pad",
            "accessories",
            79m,
            10.95m,
            12.95m,
            "A folding foam sit pad gives you a dry seat for short trail breaks.",
            "accessories",
            Colors("Forest", "Sand", "Slate"),
            Stock: 5),
        new(
            "FIELDWORK-GEAR-STRAPS",
            "Reusable equipment straps",
            "accessories",
            89m,
            11.95m,
            13.95m,
            "Reusable straps secure a sleeping mat or spare gear to your backpack.",
            "accessories",
            Sizes("Small", "Medium", "Large"),
            Stock: 14),
        new(
            "FIELDWORK-COMPACT-HEADLAMP",
            "Compact trail headlamp",
            "accessories",
            229m,
            29.95m,
            34.95m,
            "A compact 200-lumen headlamp lights the trail while keeping both hands free. A lightweight option for evening walks.",
            "headlamp",
            Colors("Forest", "Slate", "Copper"),
            Stock: 8),
        new(
            "FIELDWORK-HIGH-OUTPUT-HEADLAMP",
            "High-output trail headlamp",
            "accessories",
            529m,
            69.95m,
            74.95m,
            "A rechargeable 600-lumen headlamp with a long-range beam for dark trails and overnight hikes.",
            "headlamp",
            Colors("Forest", "Slate", "Copper"),
            Stock: 8),
        new(
            "FIELDWORK-INSULATED-GLOVES",
            "Insulated winter hiking gloves",
            "accessories",
            299m,
            39.95m,
            44.95m,
            "Insulated hiking gloves keep your hands warm on cold winter walks. Soft lining and a grippy palm help when using trekking poles.",
            "gloves",
            Sizes("Small", "Medium", "Large"),
            Stock: 8),
        new(
            "FIELDWORK-WATERPROOF-GLOVES",
            "Waterproof hiking gloves",
            "accessories",
            379m,
            49.95m,
            54.95m,
            "Waterproof hiking gloves protect your hands from wind and rain. Flexible fingers and textured palms suit wet-weather hill walking.",
            "gloves",
            Sizes("Small", "Medium", "Large"),
            Stock: 8),
        new(
            "FIELDWORK-COMPACT-FIRST-AID",
            "Compact trail first-aid kit",
            "accessories",
            149m,
            19.95m,
            24.95m,
            "A small first-aid kit for solo day hikes, with clearly labelled compartments for dressings and basic supplies.",
            "first-aid",
            Sizes("Solo", "Duo"),
            Stock: 8),
        new(
            "FIELDWORK-FAMILY-FIRST-AID",
            "Family camping first-aid kit",
            "accessories",
            299m,
            39.95m,
            44.95m,
            "A roomy first-aid kit for family camping trips, with organised compartments and space for extra dressings and personal supplies.",
            "first-aid",
            Sizes("Solo", "Duo"),
            Stock: 8),
        new(
            "FIELDWORK-LIGHT-CAMP-CHAIR",
            "Ultralight trekking chair",
            "camping",
            599m,
            79.95m,
            89.95m,
            "A lightweight folding camping chair with a supportive backrest. Packs into a small carry bag for hiking and campsite evenings.",
            "chair",
            Colors("Forest", "Sand", "Slate"),
            Stock: 8),
        new(
            "FIELDWORK-RECLINING-CHAIR",
            "Reclining camp chair",
            "camping",
            749m,
            99.95m,
            109.95m,
            "A comfortable folding camp chair with an adjustable reclining backrest and padded seat for relaxed evenings at basecamp.",
            "chair",
            Colors("Forest", "Sand", "Slate"),
            Stock: 8),
        new(
            "FIELDWORK-LIGHT-DRY-BAG",
            "Lightweight rolltop dry bag",
            "packs",
            149m,
            19.95m,
            24.95m,
            "A lightweight 10-litre waterproof dry bag with a rolltop closure keeps spare clothing dry inside your hiking backpack.",
            "pouch",
            Sizes("Small", "Medium", "Large"),
            Stock: 8),
        new(
            "FIELDWORK-EXPEDITION-DRY-BAG",
            "Expedition waterproof dry bag",
            "packs",
            299m,
            39.95m,
            44.95m,
            "A tough 30-litre waterproof dry bag with a reinforced rolltop closure for wet expeditions, canoe trips and protecting bulky camping gear.",
            "pouch",
            Sizes("Small", "Medium", "Large"),
            Stock: 8),
        new(
            "FIELDWORK-POCKET-BINOCULARS",
            "Lightweight pocket binoculars",
            "accessories",
            599m,
            79.95m,
            89.95m,
            "Compact binoculars with 8x magnification and 25 mm lenses for spotting birds and wildlife on day walks. Fold into a small pocket case.",
            "binoculars",
            Colors("Forest", "Slate", "Sand"),
            Stock: 8),
        new(
            "FIELDWORK-WATERPROOF-BINOCULARS",
            "Waterproof trekking binoculars",
            "accessories",
            1199m,
            159.95m,
            174.95m,
            "Waterproof binoculars with 10x magnification and 42 mm lenses for observing distant wildlife in rain and low light.",
            "binoculars",
            Colors("Forest", "Slate", "Sand"),
            Stock: 8)
    ];

    private sealed record ProductSeed(string Sku, string Name, string Category, decimal Dkk, decimal Eur, decimal Usd,
        string Description, string Image, VariantSeed[] Variants, int? Stock = null,
        string? AdditionalCategory = null, string? OfferCurrency = null);
    private sealed record VariantSeed(string Label, string? Color, string? Size);
}
