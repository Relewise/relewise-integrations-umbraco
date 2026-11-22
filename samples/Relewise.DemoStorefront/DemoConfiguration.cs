using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

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

    internal static void Configure(RelewiseUmbracoOptionsBuilder options, IConfiguration configuration, bool connected)
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
            if (configuration["Demo:ProductPolicy"] == "None")
            {
                mapping.AdministrativeActions = AdministrativeActionPolicy.None();
            }

            mapping.Map(product => product.DisplayName, "heading");
            mapping.Map(product => product.ListPrice, "listPrice", property => property.UseConverter<PriceConverter>());
            mapping.Map(product => product.SalesPrice, "salesPrice", property => property.UseConverter<PriceConverter>());
            mapping.MapData("bodyText");
            mapping.MapData("heroImage");
            mapping.BrandFrom("brand");
            mapping.AdditionalCategoriesFrom("extraCategories");
            mapping.MapData("stock");
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
        if (connected && configuration.GetValue<bool?>("Demo:Search") != false)
        {
            options.SearchProviders.AddContent("articles", provider =>
            {
                provider.DocumentTypeAliases = ["article", "localizedArticle"];
                provider.ConfigureRequest((request, context) => request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency));
            });
            options.SearchProviders.AddContent("guides", provider =>
            {
                provider.DocumentTypeAliases = ["guide"];
                provider.ConfigureRequest((request, context) => request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency));
            });
            options.SearchProviders.AddContent("localized", provider =>
            {
                provider.DocumentTypeAliases = ["localizedArticle"];
                provider.ConfigureRequest((request, context) => request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency));
            });
            options.SearchProviders.AddContent("allEditorial", provider =>
            {
                provider.UnsupportedSegmentBehavior = UnsupportedSegmentBehavior.Ignore();
                provider.ConfigureRequest((request, context) => request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency));
            });
            options.SearchProviders.AddProduct("catalogue", ConfigureProductSearch);
            if (configuration["Demo:ProductPolicy"] == "None")
            {
                options.SearchProviders.AddProduct("commerceCatalogue", provider =>
                {
                    ConfigureProductSearch(provider);
                    provider.RequirePublishedPage = false; // The application handles Products without a published CMS page.
                });
            }

            options.SearchProviders.AddProduct("offers", provider =>
            {
                ConfigureProductSearch(provider);
                provider.DisplayedAtLocation = "Local storefront offers";
            });
            if (configuration.GetValue<bool?>("Demo:DefaultSearch") != false)
            {
                options.SearchProviders.DefaultPublishedContentProviderAlias = configuration["Demo:DefaultProvider"] ?? "articles";
            }
        }
    }

    private static void ConfigureProductSearch(ProductSearchProviderOptions provider)
    {
        provider.MapBooleanData("inStock"); // Query the same business value written by DemoProductMapper.
        foreach (var market in DemoMarket.All)
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
