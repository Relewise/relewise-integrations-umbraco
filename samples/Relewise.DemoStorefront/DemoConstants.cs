namespace Relewise.DemoStorefront;

internal static class DemoConstants
{
    internal static class DocumentTypeAliases
    {
        internal const string Home = "home";
        internal const string Article = "article";
        internal const string LocalizedArticle = "localizedArticle";
        internal const string Guide = "guide";
        internal const string Folder = "folder";
        internal const string Brand = "brand";
        internal const string ProductCategory = "productCategory";
        internal const string Product = "product";
        internal const string ProductVariant = "productVariant";
        internal const string EditorialText = "editorialText";
        internal const string LocalizedEditorialText = "localizedEditorialText";
        internal const string EditorialGroup = "editorialGroup";
        internal const string LocalizedEditorialGroup = "localizedEditorialGroup";
        internal const string EditorialVisibility = "editorialVisibility";
    }

    internal static class PropertyAliases
    {
        internal const string Sku = "sku";
        internal const string BrandId = "brandId";
        internal const string CategoryId = "categoryId";
        internal const string Heading = "heading";
        internal const string BodyText = "bodyText";
        internal const string HeroImage = "heroImage";
        internal const string Stock = "stock";
        internal const string ListPrice = "listPrice";
        internal const string SalesPrice = "salesPrice";
        internal const string ContentRows = "contentRows";
        internal const string PageGrid = "pageGrid";
        internal const string Items = "items";
        internal const string Text = "text";
        internal const string Hide = "hide";
        internal const string Caption = "caption";
        internal const string Color = "color";
        internal const string Size = "size";
        internal const string OptionLabel = "optionLabel";
        internal const string Capacity = "capacity";
        internal const string WeightGrams = "weightGrams";
        internal const string SizeGuide = "sizeGuide";
        internal const string ProductType = "productType";
        internal const string ExtraCategories = "extraCategories";
        internal const string Brand = "brand";
        internal const string Currency = "currency";
        internal const string Amount = "amount";
        internal const string Score = "score";
    }

    internal static class DataKeys
    {
        internal const string InStock = "inStock";
        internal const string ProductType = PropertyAliases.ProductType;
        internal const string BodyText = PropertyAliases.BodyText;
        internal const string SearchText = "searchText";
        internal const string EditorialSource = "editorialSource";

        internal static string OnSale(string currency) => "onSale_" + currency;
    }

    internal static class SearchFields
    {
        internal const string Category = "shopCategory";
        internal const string Brand = "shopBrand";

        internal const string Price = "shopPrice";
    }

    internal static class SearchProviders
    {
        internal const string Articles = "articles";
        internal const string Catalogue = "catalogue";
        internal const string Offers = "offers";
    }

    internal static class Cookies
    {
        internal const string Visitor = "storefront-visitor";
        internal const string Market = "storefront-market";
    }

    internal static class RequestItems
    {
        internal const string Checkout = "Fieldwork:Checkout";
    }

    internal static class ConfigurationKeys
    {
        internal const string DatasetId = "Relewise:DatasetId";
    }
}
