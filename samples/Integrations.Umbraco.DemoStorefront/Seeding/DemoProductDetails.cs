using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Seeding;

internal static class DemoProductDetails
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        var types = services.GetRequiredService<IContentTypeService>();
        IContentType type = types.Get(DocumentTypeAliases.Product)!;
        IDataType[] dataTypes = [.. (await services.GetRequiredService<IDataTypeService>().GetAllAsync())];
        var schemaChanged = false;

        foreach ((string alias, string name, string editor) in new[]
                     {
                         (PropertyAliases.WeightGrams, "Weight (g)", "Numeric"),
                         (PropertyAliases.Capacity, "Capacity", "Textstring")
                     })
        {
            if (!type.PropertyTypes.Any(property => property.Alias == alias))
            {
                type.AddPropertyType(
                    new PropertyType(
                            services.GetRequiredService<IShortStringHelper>(),
                            dataTypes.First(data => data.Name == editor),
                            alias)
                    { Name = name });
                schemaChanged = true;
            }
        }

        if (schemaChanged)
        {
            await types.UpdateAsync(type, Constants.Security.SuperUserKey);
        }

        using IScope scope = services.GetRequiredService<IScopeProvider>().CreateScope();

        scope.EagerWriteLock(Constants.Locks.ContentTree);

        var content = services.GetRequiredService<IContentService>();
        IEnumerable<IContent> nodes = content.GetRootContent()
            .SelectMany(root => content.GetPagedDescendants(
                root.Id,
                0,
                1000,
                out _
            ));

        foreach (IContent product in nodes.Where(node => node.ContentType.Alias == DocumentTypeAliases.Product))
        {
            if (!product.Published || product.Edited ||
                !Details.TryGetValue(product.GetValue<string>(PropertyAliases.Sku) ?? "", out ProductDetail? detail))
            {
                continue;
            }

            var changed = false;

            if (product.GetValue(PropertyAliases.WeightGrams) == null)
            {
                product.SetValue(PropertyAliases.WeightGrams, detail.WeightGrams);
                product.SetValue(PropertyAliases.Capacity, detail.Capacity);
                changed = true;
            }

            string previous = "/images/" + detail.PreviousIllustration + ".svg";
            string illustration = "/images/" + detail.Illustration + ".svg";

            if (previous != illustration && product.GetValue<string>(PropertyAliases.HeroImage) == previous)
            {
                product.SetValue(PropertyAliases.HeroImage, illustration);
                changed = true;
            }

            if (changed)
            {
                content.Save(product);

                if (!content.Publish(product, [.. product.AvailableCultures]).Success)
                {
                    throw new InvalidOperationException($"Failed publishing Product details for '{product.Name}'.");
                }
            }
        }

        scope.Complete();
    }

    private static readonly Dictionary<string, ProductDetail> Details = new(StringComparer.Ordinal)
    {
        ["FIELDWORK-HYDRATION-PACK"] = new ProductDetail(420, "8 L", "daypack", "daypack"),
        ["FIELDWORK-OVERNIGHT-PACK"] = new ProductDetail(980, "30 L", "daypack", "daypack"),
        ["FIELDWORK-EXPEDITION-PACK"] = new ProductDetail(2100, "65 L", "daypack", "daypack"),
        ["FIELDWORK-FOLDABLE-PACK"] = new ProductDetail(150, "15 L", "daypack", "daypack"),
        ["FIELDWORK-ROLLTOP-PACK"] = new ProductDetail(900, "25 L", "daypack", "daypack"),
        ["FIELDWORK-CAMERA-PACK"] = new ProductDetail(1250, "25 L", "daypack", "daypack"),
        ["FIELDWORK-CAMP-CHAIR"] = new ProductDetail(1800, null, "chair", "chair"),
        ["FIELDWORK-DRY-BAG"] = new ProductDetail(120, "20 L", "daypack", "pouch"),
        ["FIELDWORK-COMPACT-HEADLAMP"] = new ProductDetail(55, null, "headlamp", "headlamp"),
        ["FIELDWORK-HIGH-OUTPUT-HEADLAMP"] = new ProductDetail(85, null, "headlamp", "headlamp"),
        ["FIELDWORK-INSULATED-GLOVES"] = new ProductDetail(110, null, "gloves", "gloves"),
        ["FIELDWORK-WATERPROOF-GLOVES"] = new ProductDetail(95, null, "gloves", "gloves"),
        ["FIELDWORK-COMPACT-FIRST-AID"] = new ProductDetail(140, null, "first-aid", "first-aid"),
        ["FIELDWORK-FAMILY-FIRST-AID"] = new ProductDetail(350, null, "first-aid", "first-aid"),
        ["FIELDWORK-LIGHT-CAMP-CHAIR"] = new ProductDetail(650, null, "chair", "chair"),
        ["FIELDWORK-RECLINING-CHAIR"] = new ProductDetail(2800, null, "chair", "chair"),
        ["FIELDWORK-LIGHT-DRY-BAG"] = new ProductDetail(40, "10 L", "pouch", "pouch"),
        ["FIELDWORK-EXPEDITION-DRY-BAG"] = new ProductDetail(180, "30 L", "pouch", "pouch"),
        ["FIELDWORK-POCKET-BINOCULARS"] = new ProductDetail(210, null, "binoculars", "binoculars"),
        ["FIELDWORK-WATERPROOF-BINOCULARS"] = new ProductDetail(650, null, "binoculars", "binoculars"),
        ["U19-SMOKE-PACK"] = new ProductDetail(650, "20 L", "daypack", "daypack"),
        ["FIELDWORK-SUMMIT-PACK"] = new ProductDetail(1450, "45 L", "daypack", "daypack"),
        ["FIELDWORK-RIDGE-PACK"] = new ProductDetail(1200, "35 L", "daypack", "daypack"),
        ["U19-SMOKE-FLASK"] = new ProductDetail(420, "0.75 L", "flask", "flask"),
        ["FIELDWORK-WATER-BOTTLE"] = new ProductDetail(170, "0.75 L", "flask", "flask"),
        ["FIELDWORK-LARGE-THERMOS"] = new ProductDetail(900, "1.5 L", "flask", "flask"),
        ["FIELDWORK-COOKWARE"] = new ProductDetail(380, "1.2 L", "mug", "cookware"),
        ["FIELDWORK-COOKING-POT"] = new ProductDetail(220, "1.5 L", "mug", "cookware"),
        ["FIELDWORK-TREKKING-TENT"] = new ProductDetail(1700, null, "shelter", "shelter"),
        ["FIELDWORK-HEADLAMP"] = new ProductDetail(80, null, "lantern", "headlamp"),
        ["FIELDWORK-BINOCULARS"] = new ProductDetail(280, null, "accessories", "binoculars"),
        ["FIELDWORK-HIKING-GLOVES"] = new ProductDetail(75, null, "accessories", "gloves"),
        ["FIELDWORK-WEEKEND-DUFFEL"] = new ProductDetail(800, "40 L", "daypack", "duffel"),
        ["FIELDWORK-BASECAMP-DUFFEL"] = new ProductDetail(1150, "70 L", "daypack", "duffel"),
        ["FIELDWORK-CAMP-HAMMOCK"] = new ProductDetail(520, null, "shelter", "hammock"),
        ["FIELDWORK-PICNIC-BLANKET"] = new ProductDetail(600, null, "accessories", "blanket"),
        ["FIELDWORK-REPAIR-KIT"] = new ProductDetail(95, null, "accessories", "pouch"),
        ["FIELDWORK-FIRST-AID-KIT"] = new ProductDetail(180, null, "accessories", "first-aid"),
        ["FIELDWORK-SOCKS"] = new ProductDetail(65, null, "accessories", "socks"),
        ["FIELDWORK-CAP"] = new ProductDetail(80, null, "accessories", "cap"),
        ["FIELDWORK-TRAIL-BEANIE"] = new ProductDetail(90, null, "accessories", "beanie"),
        ["FIELDWORK-PACKING-CUBES"] = new ProductDetail(120, null, "daypack", "pouch"),
        ["FIELDWORK-TOILETRY-BAG"] = new ProductDetail(160, null, "daypack", "pouch"),
        ["FIELDWORK-TRAVEL-WALLET"] = new ProductDetail(45, null, "accessories", "pouch"),
        ["FIELDWORK-CAMP-PILLOW"] = new ProductDetail(85, null, "sleeping-bag", "pillow"),
        ["FIELDWORK-GEAR-STRAPS"] = new ProductDetail(50, null, "accessories", "straps"),
        ["FIELDWORK-PHONE-POUCH"] = new ProductDetail(55, null, "accessories", "phone-pouch"),
        ["FIELDWORK-TRAIL-SUNGLASSES"] = new ProductDetail(30, null, "accessories", "sunglasses")
    };

    private sealed record ProductDetail(
        int WeightGrams,
        string? Capacity,
        string PreviousIllustration,
        string Illustration);
}
