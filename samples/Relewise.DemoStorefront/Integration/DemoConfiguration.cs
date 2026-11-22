using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Integration.Mapping;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Search;

namespace Relewise.DemoStorefront.Integration;

internal static class DemoConfiguration
{
    internal static string EntityId(Umbraco.Cms.Core.Models.PublishedContent.IPublishedContent content, IServiceProvider services)
        => services.GetService<RelewiseUmbracoConfiguration>()?.GetContentId(content) ?? (content.ContentType.Alias switch
        {
            "product" or "productVariant" => content.Value<string>("sku"),
            "brand" => content.Value<string>("brandId"),
            "productCategory" => content.Value<string>("categoryId"),
            _ => null
        }) ?? content.Key.ToString("D");

    internal static void Configure(RelewiseUmbracoOptionsBuilder options)
    {
        options.UseContentKeysAsIds = true;
        options.MapContent("article", mapping =>
        {
            mapping.Map(entity => entity.DisplayName, "heading");
            mapping.MapData("bodyText");
            mapping.MapData("heroImage");
            mapping.MapData("contentRows", data => data.UseConverter<EditorialBlocksConverter>());
            mapping.MapData("pageGrid", data => data.UseConverter<EditorialBlocksConverter>());
            mapping.UseMapper<EditorialMapper>(); // Runs last, after declarative fields.
        });
        options.MapContent("localizedArticle", mapping =>
        {
            mapping.Map(entity => entity.DisplayName, "heading");
            mapping.MapData("bodyText");
            mapping.MapData("score");
            mapping.MapData("contentRows", data => data.UseConverter<EditorialBlocksConverter>());
            mapping.MapData("pageGrid", data => data.UseConverter<EditorialBlocksConverter>());
            mapping.UseMapper<EditorialMapper>(); // Aggregate only the published cultures in this export.
        });
#pragma warning disable CS0618 // Deliberate example of the legacy compatibility adapter.
        options.AddContentType("guide", mapping => mapping.UseMapper(new LegacyGuideMapper()));
#pragma warning restore CS0618
        options.MapProductCategory("productCategory", mapping => mapping.IdFromProperty("categoryId"));
        options.MapBrand("brand", mapping => mapping.IdFromProperty("brandId"));
        options.MapProduct("product", mapping =>
        {
            mapping.IdFromProperty("sku");
            // For externally managed Products, use AdministrativeActionPolicy.None() here.

            mapping.Map(product => product.DisplayName, "heading");
            mapping.Map(product => product.ListPrice, "listPrice", property => property.UseConverter<PriceConverter>());
            mapping.Map(product => product.SalesPrice, "salesPrice", property => property.UseConverter<PriceConverter>());
            mapping.MapData("bodyText");
            mapping.MapData("heroImage");
            mapping.BrandFrom("brand");
            mapping.AdditionalCategoriesFrom("extraCategories");
            mapping.MapData("stock");
            mapping.MapData("productType"); // Editable classification for genuine alternatives.
            mapping.MapData("weightGrams");
            mapping.MapData("capacity");
            mapping.MapData("sizeGuide");
            mapping.UseMapper<DemoProductMapper>(); // Enrich the SDK Product after built-in properties are mapped.
            mapping.VariantsFromChildren("productVariant", variant =>
            {
                variant.IdFromProperty("sku");
                variant.Map(variant => variant.DisplayName, "heading");
                variant.MapData("bodyText");
                variant.MapData("color");
                variant.MapData("size");
                variant.MapData("optionLabel");
                variant.MapData("capacity");
            });
        });
        options.SearchProviders.AddContent("articles", provider =>
        {
            provider.DocumentTypeAliases = ["article", "localizedArticle", "guide"];
            provider.ConfigureRequest((request, context) =>
                request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency));
        });
        options.SearchProviders.AddProduct("catalogue", ConfigureProductSearch);
        options.SearchProviders.AddProduct("offers", provider =>
        {
            ConfigureProductSearch(provider);
            provider.DisplayedAtLocation = "Storefront offers";
        });
        options.SearchProviders.DefaultPublishedContentProviderAlias = "articles";
    }

    private static void ConfigureProductSearch(ProductSearchProviderOptions provider)
    {
        provider.MapBooleanData("inStock"); // Query the same business value written by DemoProductMapper.
        foreach (DemoMarket market in DemoMarket.All)
        {
            provider.MapBooleanData("onSale_" + market.Currency);
        }

        provider.ConfigureRequest((request, context) =>
        {
            request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency);
            // Match translated colour/size Variants while keeping one card per Product.
            request.Settings ??= new();
            request.Settings.VariantRequestSettings ??= new();
            request.Settings.VariantRequestSettings.MaxVariantsPerProduct = 1;
        });
    }
}
