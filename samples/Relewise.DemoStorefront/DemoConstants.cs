namespace Relewise.DemoStorefront;

internal static class DemoConstants
{
    public static class DocumentTypeAliases
    {
        public const string Home = "home";
        public const string Article = "article";
        public const string LocalizedArticle = "localizedArticle";
        public const string Guide = "guide";
        public const string Folder = "folder";
        public const string Brand = "brand";
        public const string ProductCategory = "productCategory";
        public const string Product = "product";
        public const string ProductVariant = "productVariant";
        public const string EditorialText = "editorialText";
        public const string LocalizedEditorialText = "localizedEditorialText";
        public const string EditorialGroup = "editorialGroup";
        public const string LocalizedEditorialGroup = "localizedEditorialGroup";
        public const string EditorialVisibility = "editorialVisibility";
    }

    public static class PropertyAliases
    {
        public const string Sku = "sku";
        public const string BrandId = "brandId";
        public const string CategoryId = "categoryId";
        public const string Heading = "heading";
        public const string BodyText = "bodyText";
        public const string HeroImage = "heroImage";
        public const string Stock = "stock";
        public const string ListPrice = "listPrice";
        public const string SalesPrice = "salesPrice";
        public const string ContentRows = "contentRows";
        public const string PageGrid = "pageGrid";
        public const string Items = "items";
        public const string Text = "text";
        public const string Hide = "hide";
        public const string Caption = "caption";
        public const string Color = "color";
        public const string Size = "size";
        public const string OptionLabel = "optionLabel";
        public const string Capacity = "capacity";
        public const string WeightGrams = "weightGrams";
        public const string SizeGuide = "sizeGuide";
        public const string ProductType = "productType";
        public const string ExtraCategories = "extraCategories";
        public const string Brand = "brand";
        public const string Currency = "currency";
        public const string Amount = "amount";
        public const string Score = "score";
    }

    public static class DataKeys
    {
        public const string InStock = "inStock";
        public const string ProductType = PropertyAliases.ProductType;
        public const string BodyText = PropertyAliases.BodyText;
        public const string SearchText = "searchText";
        public const string EditorialSource = "editorialSource";

        public static string OnSale(string currency) => "onSale_" + currency;
    }

    public static class SearchFields
    {
        public const string Category = "shopCategory";
        public const string Brand = "shopBrand";

        public const string Price = "shopPrice";
    }

    public static class SearchProviders
    {
        public const string Articles = "articles";
        public const string Catalogue = "catalogue";
        public const string Offers = "offers";
    }

    public static class Cookies
    {
        public const string Visitor = "storefront-visitor";
        public const string Market = "storefront-market";
    }

    public static class RequestItems
    {
        public const string Checkout = "Fieldwork:Checkout";
    }

    public static class ConfigurationKeys
    {
        public const string DatasetId = "Relewise:DatasetId";
    }
}
