using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search.Settings;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Integration.Mapping;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Integration;

internal static class DemoConfiguration
{
    internal static string EntityId(
        IPublishedContent content,
        IServiceProvider services)
    {
        return
            services.GetService<RelewiseUmbracoConfiguration>()?.GetContentId(content) ??
            content.ContentType.Alias switch
            {
                DocumentTypeAliases.Product
                    or DocumentTypeAliases.ProductVariant => content.Value<string>(PropertyAliases.Sku),
                DocumentTypeAliases.Brand => content.Value<string>(PropertyAliases.BrandId),
                DocumentTypeAliases.ProductCategory => content.Value<string>(PropertyAliases.CategoryId),
                _ => null
            } ?? content.Key.ToString("D");
    }

    internal static void Configure(RelewiseUmbracoOptionsBuilder options)
    {
        options.UseContentKeysAsIds = true;

        options.MapContent(
            DocumentTypeAliases.Article,
            mapping =>
            {
                mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace(policy => policy.DeltaUpdates = false);
                mapping.Map(entity => entity.DisplayName, PropertyAliases.Heading);
                mapping.MapData(PropertyAliases.BodyText);
                mapping.MapData(PropertyAliases.HeroImage);
                mapping.MapData(PropertyAliases.ContentRows, data => data.UseConverter<EditorialBlocksConverter>());
                mapping.MapData(PropertyAliases.PageGrid, data => data.UseConverter<EditorialBlocksConverter>());
                mapping.UseMapper<EditorialMapper>(); // Runs last, after declarative fields.
            });

        options.MapContent(
            DocumentTypeAliases.LocalizedArticle,
            mapping =>
            {
                mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace(); // Full entity in both export flows.
                mapping.Map(entity => entity.DisplayName, PropertyAliases.Heading);
                mapping.MapData(PropertyAliases.BodyText);
                mapping.MapData(PropertyAliases.Score);
                mapping.MapData(PropertyAliases.ContentRows, data => data.UseConverter<EditorialBlocksConverter>());
                mapping.MapData(PropertyAliases.PageGrid, data => data.UseConverter<EditorialBlocksConverter>());
                mapping.UseMapper<EditorialMapper>(); // Aggregate only the published cultures in this export.
            });

#pragma warning disable CS0618 // Deliberate example of the legacy compatibility adapter.
        options.AddContentType(DocumentTypeAliases.Guide, mapping => mapping.UseMapper(new LegacyGuideMapper()));
#pragma warning restore CS0618

        options.MapProductCategory(
            DocumentTypeAliases.ProductCategory,
            mapping => mapping.IdFromProperty(PropertyAliases.CategoryId));

        options.MapBrand(DocumentTypeAliases.Brand, mapping => mapping.IdFromProperty(PropertyAliases.BrandId));

        options.MapProduct(
            DocumentTypeAliases.Product,
            mapping =>
            {
                mapping.IdFromProperty(PropertyAliases.Sku);
                mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace(policy => policy.DeltaUpdates = false);
                // Commerce contributions use the default partial-update policy and AdministrativeActionPolicy.None().

                mapping.Map(product => product.DisplayName, PropertyAliases.Heading);
                mapping.Map(
                    product => product.ListPrice,
                    PropertyAliases.ListPrice,
                    property => property.UseConverter<PriceConverter>());
                mapping.Map(
                    product => product.SalesPrice,
                    PropertyAliases.SalesPrice,
                    property => property.UseConverter<PriceConverter>());

                mapping.MapData(PropertyAliases.BodyText);
                mapping.MapData(PropertyAliases.HeroImage);

                mapping.BrandFrom(PropertyAliases.Brand);

                mapping.AdditionalCategoriesFrom(PropertyAliases.ExtraCategories);

                mapping.MapData(PropertyAliases.Stock);
                mapping.MapData(PropertyAliases.ProductType); // Editable classification for genuine alternatives.
                mapping.MapData(PropertyAliases.WeightGrams);
                mapping.MapData(PropertyAliases.Capacity);
                mapping.MapData(PropertyAliases.SizeGuide);

                mapping.UseMapper<DemoProductMapper>(); // Enrich the SDK Product after built-in properties are mapped.

                mapping.VariantsFromChildren(
                    DocumentTypeAliases.ProductVariant,
                    variant =>
                    {
                        variant.IdFromProperty(PropertyAliases.Sku);
                        // Variant fields have their own update policy; the Product policy does not cascade.
                        variant.UpdatePolicy =
                            EntityUpdatePolicy.ClearAndReplace(policy => policy.DeltaUpdates = false);

                        variant.Map(productVariant => productVariant.DisplayName, PropertyAliases.Heading);

                        variant.MapData(PropertyAliases.BodyText);
                        variant.MapData(PropertyAliases.Color);
                        variant.MapData(PropertyAliases.Size);
                        variant.MapData(PropertyAliases.OptionLabel);
                        variant.MapData(PropertyAliases.Capacity);
                    });
            });

        options.SearchProviders.AddContent(
            SearchProviders.Articles,
            provider =>
            {
                provider.DocumentTypeAliases = [
                    DocumentTypeAliases.Article,
                    DocumentTypeAliases.LocalizedArticle,
                    DocumentTypeAliases.Guide
                ];

                provider.ConfigureRequest((request, context) =>
                {
                    request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency);
                });
            });

        options.SearchProviders.AddProduct(SearchProviders.Catalogue, ConfigureProductSearch);

        options.SearchProviders.AddProduct(
            SearchProviders.Offers,
            provider =>
            {
                ConfigureProductSearch(provider);

                provider.DisplayedAtLocation = "Storefront offers";
            });

        options.SearchProviders.DefaultPublishedContentProviderAlias = SearchProviders.Articles;
    }

    private static void ConfigureProductSearch(ProductSearchProviderOptions provider)
    {
        provider.MapSalesPrice(SearchFields.Price); // Native price facets and sorting use the active market currency.
        provider.MapBooleanData(DataKeys.InStock); // Query the same business value written by DemoProductMapper.

        foreach (DemoMarket market in DemoMarket.All)
        {
            provider.MapBooleanData(DataKeys.OnSale(market.Currency));
        }

        provider.ConfigureRequest((request, context) =>
        {
            request.Currency = new Currency(context.Services.GetRequiredService<DemoMarket>().Currency);
            // Match translated colour/size Variants while keeping one card per Product.
            request.Settings ??= new ProductSearchSettings();
            request.Settings.VariantRequestSettings ??= new VariantSearchRequestSettings();
            request.Settings.VariantRequestSettings.MaxVariantsPerProduct = 1;
        });
    }
}
