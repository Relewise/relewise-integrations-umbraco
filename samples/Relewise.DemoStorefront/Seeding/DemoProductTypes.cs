using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.DemoStorefront.Seeding;

// Explicit catalogue classification: a chair is an alternative to another chair, not a camping table.
internal static class DemoProductTypes
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        IContentTypeService types = services.GetRequiredService<IContentTypeService>();
        IContentType productType = types.Get("product")!;
        if (!productType.PropertyTypes.Any(property => property.Alias == "productType"))
        {
            IDataType text = (await services.GetRequiredService<IDataTypeService>().GetAllAsync()).First(type => type.Name == "Textstring");
            productType.AddPropertyType(new PropertyType(
                services.GetRequiredService<IShortStringHelper>(),
                text,
                "productType"
            )
            {
                Name = "Product type"
            });
            await types.UpdateAsync(productType, Constants.Security.SuperUserKey);
        }

        using IScope scope = services.GetRequiredService<IScopeProvider>().CreateScope();
        scope.EagerWriteLock(Constants.Locks.ContentTree);
        IContentService content = services.GetRequiredService<IContentService>();
        IEnumerable<IContent> products = content.GetRootContent().SelectMany(root => content.GetPagedDescendants(
            root.Id,
            0,
            1000,
            out _
        ))
            .Where(node => node.ContentType.Alias == "product");
        foreach (IContent product in products)
        {
            if (!product.Published || product.Edited || !product.AvailableCultures.All(culture => product.PublishedCultures.Contains(culture)) ||
                !string.IsNullOrWhiteSpace(product.GetValue<string>("productType")) || !Types.TryGetValue(product.GetValue<string>("sku") ?? "", out string? type))
            {
                continue;
            }
            product.SetValue("productType", type);
            content.Save(product);
            if (!content.Publish(product, product.PublishedCultures.ToArray()).Success)
            {
                throw new InvalidOperationException($"Failed publishing Product type for '{product.Name}'.");
            }
        }
        scope.Complete();
    }

    private static readonly Dictionary<string, string> Types = new(StringComparer.Ordinal)
    {
        ["U19-SMOKE-PACK"] = "Daypack",
        ["FIELDWORK-SUMMIT-PACK"] = "Trekking backpack",
        ["FIELDWORK-RIDGE-PACK"] = "Travel backpack",
        ["FIELDWORK-HYDRATION-PACK"] = "Hydration pack",
        ["FIELDWORK-OVERNIGHT-PACK"] = "Trekking backpack",
        ["FIELDWORK-EXPEDITION-PACK"] = "Trekking backpack",
        ["FIELDWORK-FOLDABLE-PACK"] = "Daypack",
        ["FIELDWORK-ROLLTOP-PACK"] = "Daypack",
        ["FIELDWORK-CAMERA-PACK"] = "Camera backpack",
        ["FIELDWORK-SLING"] = "Small shoulder bag",
        ["FIELDWORK-HIP-PACK"] = "Small shoulder bag",
        ["FIELDWORK-WEEKEND-DUFFEL"] = "Duffel bag",
        ["FIELDWORK-BASECAMP-DUFFEL"] = "Duffel bag",
        ["FIELDWORK-DRY-BAG"] = "Dry bag",
        ["FIELDWORK-LIGHT-DRY-BAG"] = "Dry bag",
        ["FIELDWORK-EXPEDITION-DRY-BAG"] = "Dry bag",
        ["U19-SMOKE-FLASK"] = "Vacuum flask",
        ["FIELDWORK-LARGE-THERMOS"] = "Vacuum flask",
        ["FIELDWORK-TEA-FLASK"] = "Vacuum flask",
        ["FIELDWORK-WATER-BOTTLE"] = "Drinking bottle",
        ["FIELDWORK-HYDRATION-BOTTLE"] = "Drinking bottle",
        ["FIELDWORK-KIDS-BOTTLE"] = "Drinking bottle",
        ["FIELDWORK-FILTER-BOTTLE"] = "Drinking bottle",
        ["FIELDWORK-STEEL-BOTTLE"] = "Drinking bottle",
        ["FIELDWORK-CAMP-MUG"] = "Drinking cup",
        ["FIELDWORK-TUMBLER"] = "Drinking cup",
        ["FIELDWORK-ESPRESSO-CUP"] = "Drinking cup",
        ["FIELDWORK-ENAMEL-MUG"] = "Drinking cup",
        ["FIELDWORK-FOLDING-CUP"] = "Drinking cup",
        ["FIELDWORK-CUP-SET"] = "Drinking cup",
        ["U19-SMOKE-SHELTER"] = "Tarp shelter",
        ["FIELDWORK-TARP"] = "Tarp shelter",
        ["FIELDWORK-TREKKING-TENT"] = "Tent",
        ["FIELDWORK-FAMILY-TENT"] = "Tent",
        ["FIELDWORK-SLEEPING-BAG"] = "Sleeping bag",
        ["FIELDWORK-WINTER-SLEEPING-BAG"] = "Sleeping bag",
        ["FIELDWORK-SLEEPING-MAT"] = "Sleeping mat",
        ["FIELDWORK-FOAM-MAT"] = "Sleeping mat",
        ["FIELDWORK-CAMP-CHAIR"] = "Camping chair",
        ["FIELDWORK-LIGHT-CAMP-CHAIR"] = "Camping chair",
        ["FIELDWORK-RECLINING-CHAIR"] = "Camping chair",
        ["FIELDWORK-COOKWARE"] = "Cooking pot",
        ["FIELDWORK-COOKING-POT"] = "Cooking pot",
        ["FIELDWORK-LANTERN"] = "Camp lantern",
        ["FIELDWORK-SOLAR-LIGHT"] = "Camp lantern",
        ["FIELDWORK-HEADLAMP"] = "Headlamp",
        ["FIELDWORK-COMPACT-HEADLAMP"] = "Headlamp",
        ["FIELDWORK-HIGH-OUTPUT-HEADLAMP"] = "Headlamp",
        ["FIELDWORK-HIKING-GLOVES"] = "Hiking gloves",
        ["FIELDWORK-INSULATED-GLOVES"] = "Hiking gloves",
        ["FIELDWORK-WATERPROOF-GLOVES"] = "Hiking gloves",
        ["FIELDWORK-FIRST-AID-KIT"] = "First-aid kit",
        ["FIELDWORK-COMPACT-FIRST-AID"] = "First-aid kit",
        ["FIELDWORK-FAMILY-FIRST-AID"] = "First-aid kit",
        ["FIELDWORK-BINOCULARS"] = "Binoculars",
        ["FIELDWORK-POCKET-BINOCULARS"] = "Binoculars",
        ["FIELDWORK-WATERPROOF-BINOCULARS"] = "Binoculars",
        ["FIELDWORK-PICNIC-BLANKET"] = "Picnic blanket",
        ["FIELDWORK-SOCKS"] = "Hiking socks",
        ["FIELDWORK-HYDRATION-RESERVOIR"] = "Hydration reservoir",
        ["FIELDWORK-FOOD-FLASK"] = "Food flask",
        ["FIELDWORK-POLES"] = "Trekking poles",
        ["FIELDWORK-CAP"] = "Walking cap",
        ["FIELDWORK-PACK-COVER"] = "Backpack rain cover",
        ["FIELDWORK-TOWEL"] = "Travel towel",
        ["FIELDWORK-REPAIR-KIT"] = "Repair kit",
        ["FIELDWORK-TENT-FOOTPRINT"] = "Tent footprint",
        ["FIELDWORK-TENT-PEG-SET"] = "Tent pegs",
        ["FIELDWORK-NECK-WARMER"] = "Neck warmer",
        ["FIELDWORK-TRAIL-BEANIE"] = "Beanie",
        ["FIELDWORK-RAIN-GAITERS"] = "Gaiters",
        ["FIELDWORK-TRAIL-COMPASS"] = "Compass",
        ["FIELDWORK-PHONE-POUCH"] = "Phone pouch",
        ["FIELDWORK-TRAIL-SUNGLASSES"] = "Sunglasses",
        ["FIELDWORK-TRAIL-SIT-PAD"] = "Sit pad",
        ["FIELDWORK-GEAR-STRAPS"] = "Gear straps",
        ["FIELDWORK-PACKING-CUBES"] = "Packing cubes",
        ["FIELDWORK-COMPRESSION-SACK"] = "Compression sack",
        ["FIELDWORK-MAP-POUCH"] = "Map pouch",
        ["FIELDWORK-TOILETRY-BAG"] = "Wash bag",
        ["FIELDWORK-TRAVEL-WALLET"] = "Travel wallet",
        ["FIELDWORK-BIKE-PANNIER"] = "Bicycle pannier",
        ["FIELDWORK-FRAME-BAG"] = "Bicycle frame bag",
        ["FIELDWORK-CAMP-JUG"] = "Insulated jug",
        ["FIELDWORK-WATER-CARRIER"] = "Water carrier",
        ["FIELDWORK-BIVVY-BAG"] = "Bivvy bag",
        ["FIELDWORK-CAMP-HAMMOCK"] = "Hammock",
        ["FIELDWORK-CAMP-PILLOW"] = "Camping pillow",
        ["FIELDWORK-CAMP-TABLE"] = "Camping table",
        ["FIELDWORK-CAMP-STOOL"] = "Camping stool",
        ["FIELDWORK-CAMP-STOVE"] = "Camping stove",
        ["FIELDWORK-CAMP-KETTLE"] = "Camping kettle",
    };
}
