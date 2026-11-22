using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

internal static class DemoProductDetails
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        var types = services.GetRequiredService<IContentTypeService>();
        var type = types.Get("product")!;
        var dataTypes = (await services.GetRequiredService<IDataTypeService>().GetAllAsync()).ToArray();
        bool schemaChanged = false;
        foreach (var (alias, name, editor) in new[] { ("weightGrams", "Weight (g)", "Numeric"), ("capacity", "Capacity", "Textstring") })
        {
            if (!type.PropertyTypes.Any(property => property.Alias == alias))
            {
                type.AddPropertyType(new PropertyType(services.GetRequiredService<IShortStringHelper>(), dataTypes.First(data => data.Name == editor), alias) { Name = name });
                schemaChanged = true;
            }
        }
        if (schemaChanged)
        {
            await types.UpdateAsync(type, Constants.Security.SuperUserKey);
        }

        using var scope = services.GetRequiredService<IScopeProvider>().CreateScope();
        scope.EagerWriteLock(Constants.Locks.ContentTree);
        var content = services.GetRequiredService<IContentService>();
        var nodes = content.GetRootContent().SelectMany(root => content.GetPagedDescendants(root.Id, 0, 1000, out _));
        foreach (IContent product in nodes.Where(node => node.ContentType.Alias == "product"))
        {
            if (!product.Published || product.Edited || !Details.TryGetValue(product.GetValue<string>("sku") ?? "", out var detail))
            {
                continue;
            }
            bool changed = false;
            if (product.GetValue("weightGrams") == null)
            {
                product.SetValue("weightGrams", detail.WeightGrams);
                product.SetValue("capacity", detail.Capacity);
                changed = true;
            }
            string previous = "/images/" + detail.PreviousIllustration + ".svg";
            string illustration = "/images/" + detail.Illustration + ".svg";
            if (previous != illustration && product.GetValue<string>("heroImage") == previous)
            {
                product.SetValue("heroImage", illustration);
                changed = true;
            }
            if (changed)
            {
                content.Save(product);
                if (!content.Publish(product, product.AvailableCultures.ToArray()).Success)
                {
                    throw new InvalidOperationException($"Failed publishing Product details for '{product.Name}'.");
                }
            }
        }
        scope.Complete();
    }

    private static readonly Dictionary<string, ProductDetail> Details = new(StringComparer.Ordinal)
    {
        ["FIELDWORK-HYDRATION-PACK"] = new(420, "8 L", "daypack", "daypack"),
        ["FIELDWORK-OVERNIGHT-PACK"] = new(980, "30 L", "daypack", "daypack"),
        ["FIELDWORK-EXPEDITION-PACK"] = new(2100, "65 L", "daypack", "daypack"),
        ["FIELDWORK-FOLDABLE-PACK"] = new(150, "15 L", "daypack", "daypack"),
        ["FIELDWORK-ROLLTOP-PACK"] = new(900, "25 L", "daypack", "daypack"),
        ["FIELDWORK-CAMERA-PACK"] = new(1250, "25 L", "daypack", "daypack"),
        ["FIELDWORK-CAMP-CHAIR"] = new(1800, null, "chair", "chair"),
        ["FIELDWORK-DRY-BAG"] = new(120, "20 L", "daypack", "pouch"),
        ["FIELDWORK-COMPACT-HEADLAMP"] = new(55, null, "headlamp", "headlamp"),
        ["FIELDWORK-HIGH-OUTPUT-HEADLAMP"] = new(85, null, "headlamp", "headlamp"),
        ["FIELDWORK-INSULATED-GLOVES"] = new(110, null, "gloves", "gloves"),
        ["FIELDWORK-WATERPROOF-GLOVES"] = new(95, null, "gloves", "gloves"),
        ["FIELDWORK-COMPACT-FIRST-AID"] = new(140, null, "first-aid", "first-aid"),
        ["FIELDWORK-FAMILY-FIRST-AID"] = new(350, null, "first-aid", "first-aid"),
        ["FIELDWORK-LIGHT-CAMP-CHAIR"] = new(650, null, "chair", "chair"),
        ["FIELDWORK-RECLINING-CHAIR"] = new(2800, null, "chair", "chair"),
        ["FIELDWORK-LIGHT-DRY-BAG"] = new(40, "10 L", "pouch", "pouch"),
        ["FIELDWORK-EXPEDITION-DRY-BAG"] = new(180, "30 L", "pouch", "pouch"),
        ["FIELDWORK-POCKET-BINOCULARS"] = new(210, null, "binoculars", "binoculars"),
        ["FIELDWORK-WATERPROOF-BINOCULARS"] = new(650, null, "binoculars", "binoculars"),
        ["U19-SMOKE-PACK"] = new(650, "20 L", "daypack", "daypack"),
        ["FIELDWORK-SUMMIT-PACK"] = new(1450, "45 L", "daypack", "daypack"),
        ["FIELDWORK-RIDGE-PACK"] = new(1200, "35 L", "daypack", "daypack"),
        ["U19-SMOKE-FLASK"] = new(420, "0.75 L", "flask", "flask"),
        ["FIELDWORK-WATER-BOTTLE"] = new(170, "0.75 L", "flask", "flask"),
        ["FIELDWORK-LARGE-THERMOS"] = new(900, "1.5 L", "flask", "flask"),
        ["FIELDWORK-COOKWARE"] = new(380, "1.2 L", "mug", "cookware"),
        ["FIELDWORK-COOKING-POT"] = new(220, "1.5 L", "mug", "cookware"),
        ["FIELDWORK-TREKKING-TENT"] = new(1700, null, "shelter", "shelter"),
        ["FIELDWORK-HEADLAMP"] = new(80, null, "lantern", "headlamp"),
        ["FIELDWORK-BINOCULARS"] = new(280, null, "accessories", "binoculars"),
        ["FIELDWORK-HIKING-GLOVES"] = new(75, null, "accessories", "gloves"),
        ["FIELDWORK-WEEKEND-DUFFEL"] = new(800, "40 L", "daypack", "duffel"),
        ["FIELDWORK-BASECAMP-DUFFEL"] = new(1150, "70 L", "daypack", "duffel"),
        ["FIELDWORK-CAMP-HAMMOCK"] = new(520, null, "shelter", "hammock"),
        ["FIELDWORK-PICNIC-BLANKET"] = new(600, null, "accessories", "blanket"),
        ["FIELDWORK-REPAIR-KIT"] = new(95, null, "accessories", "pouch"),
        ["FIELDWORK-FIRST-AID-KIT"] = new(180, null, "accessories", "first-aid"),
        ["FIELDWORK-SOCKS"] = new(65, null, "accessories", "socks"),
        ["FIELDWORK-CAP"] = new(80, null, "accessories", "cap"),
        ["FIELDWORK-TRAIL-BEANIE"] = new(90, null, "accessories", "beanie"),
        ["FIELDWORK-PACKING-CUBES"] = new(120, null, "daypack", "pouch"),
        ["FIELDWORK-TOILETRY-BAG"] = new(160, null, "daypack", "pouch"),
        ["FIELDWORK-TRAVEL-WALLET"] = new(45, null, "accessories", "pouch"),
        ["FIELDWORK-CAMP-PILLOW"] = new(85, null, "sleeping-bag", "pillow"),
        ["FIELDWORK-GEAR-STRAPS"] = new(50, null, "accessories", "straps"),
        ["FIELDWORK-PHONE-POUCH"] = new(55, null, "accessories", "phone-pouch"),
        ["FIELDWORK-TRAIL-SUNGLASSES"] = new(30, null, "accessories", "sunglasses")
    };

    private sealed record ProductDetail(int WeightGrams, string? Capacity, string PreviousIllustration, string Illustration);
}
