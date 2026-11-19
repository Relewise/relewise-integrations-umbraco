using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Queries;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using Umbraco.Cms.Core.Services;

internal static class StorefrontChecks
{
    internal static async Task<IResult> Run(ISearcherResolver searchers, IContentService content, IRelewiseClientFactory clients, IConfiguration configuration,
        Microsoft.Extensions.Options.IOptions<Umbraco.Cms.Core.Search.Configuration.IndexOptions> indexes)
    {
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Smoke check failed: " + name);
            }

            checks.Add(name);
        }
        var products = searchers.GetSearcher("catalogue")!;
        var articles = searchers.GetSearcher("articles")!;
        var all = await products.SearchAsync("catalogue");
        Check(all.Total == 3 && all.Documents.Count() == 3, "Three Products, one result per Product");
        Check((await products.SearchAsync("catalogue", "forest")).Total == 1, "Variant search resolves its Product page");
        Check((await searchers.GetSearcher("offers")!.SearchAsync("offers")).Total == 1, "Independent Product profile applies its pre-request filter");
        Check((await articles.SearchAsync("articles", "finding")).Total == 1, "Content provider searches articles");
        string published = global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
        var ordinary = await searchers.GetSearcher(published)!.SearchAsync(published, "finding");
        if (configuration.GetValue<bool?>("Demo:DefaultSearch") == false)
        {
            Check(indexes.Value.GetContentIndexRegistration(published)!.Searcher.Name == "IExamineSearcher" && ordinary.Total == 1, "Examine serves ordinary queries alongside named Relewise providers");
        }
        else
        {
            Check(ordinary.Total == (configuration["Demo:DefaultProvider"] == "catalogue" ? 0 : 1), "Ordinary Umbraco query uses the selected Relewise default provider");
        }

        var paged = await products.SearchAsync("catalogue", skip: 1, take: 1);
        Check(paged.Total == 3 && paged.Documents.Count() == 1, "Product pagination retains total hits");
        var count = await products.SearchAsync("catalogue", take: 0);
        Check(count.Total == 3 && !count.Documents.Any(), "Count-only Product query");
        var exact = await products.SearchAsync("catalogue", filters: [new KeywordFilter("Umb_Name", ["Trail daypack"], false)]);
        Check(exact.Total == 1, "Core keyword filter");
        var range = await products.SearchAsync("catalogue", filters: [new IntegerRangeFilter("stock", [new(10, 20)], false)]);
        Check(range.Total == 1 && content.GetById(range.Documents.Single().Id)!.Name == "Trail daypack", "Integer range includes lower and excludes upper boundary");
        var sorted = await products.SearchAsync("catalogue", sorters: [new KeywordSorter("Umb_Name", global::Umbraco.Cms.Core.Direction.Ascending)]);
        Check(content.GetById(sorted.Documents.First().Id)!.Name == "Camp shelter", "Core sorting maps to Product Data sorting");
        var faceted = await products.SearchAsync("catalogue", facets: [new KeywordFacet("Umb_Name")]);
        Check(faceted.Facets.Single().Values.Count() == 3, "Product keyword facets");
        Check((await searchers.GetSearcher("guides")!.SearchAsync("guides")).Total == 1, "Content provider selects the second Document Type");
        Check((await searchers.GetSearcher("allEditorial")!.SearchAsync("allEditorial")).Total == 3, "Content provider without aliases includes both mapped Document Types");
        var localized = searchers.GetSearcher("localized")!;
        Check((await localized.SearchAsync("localized", culture: "en-US")).Total == 1, "Localized Content is searchable in English");
        Check((await localized.SearchAsync("localized", culture: "da-DK")).Total == 1, "The same Content entity is searchable in Danish");
        Check((await localized.SearchAsync("localized", culture: "DA-dk")).Total == 1, "Culture membership is case-insensitive");
        Check((await localized.SearchAsync("localized")).Total == 0, "An invariant query excludes culture-only documents");
        Check((await localized.SearchAsync("localized", culture: "da-DK", filters: [new IntegerExactFilter("score", [20], false)])).Total == 1, "Danish numeric filtering selects the Danish field");
        Check((await localized.SearchAsync("localized", culture: "en-US", filters: [new IntegerExactFilter("score", [20], false)])).Total == 0, "English numeric filtering does not use Danish values");
        var localizedFacet = await localized.SearchAsync("localized", culture: "da-DK", facets: [new IntegerExactFacet("score")]);
        Check(localizedFacet.Facets.Single().Values.Single() is IntegerExactFacetValue { Key: 20 }, "Danish numeric facets select the Danish field");
        var localizedSort = await localized.SearchAsync("localized", culture: "da-DK", sorters: [new IntegerSorter("score", global::Umbraco.Cms.Core.Direction.Ascending)]);
        Check(localizedSort.Total == 1, "Culture-specific numeric sorting executes");
        Check((await searchers.GetSearcher("allEditorial")!.SearchAsync("allEditorial", segment: "vip")).Total == 3, "Ignore segment policy executes an ordinary query");
        try
        {
            await articles.SearchAsync("articles", segment: "vip");
            throw new InvalidOperationException("Expected unsupported segment rejection.");
        }
        catch (NotSupportedException) { checks.Add("Default segment policy rejects the request explicitly"); }
        var data = clients.GetClient<IDataAccessor>();
        var guideNode = content.GetRootContent().SelectMany(root => content.GetPagedDescendants(root.Id, 0, 1000, out _)).Single(node => node.ContentType.Alias == "guide");
        var guide = (await data.QueryAsync(new ContentQuery(guideNode.Key.ToString("D")))).Contents.Single();
        Check(guide.DisplayName.Values?.Any(value => value.Text == "Packing for your first camp") == true, "Legacy mapper receives and returns its existing typed Content update");
        Check(guide.CategoryPaths.Length == 0, "Legacy mapper's category path override is retained");
        Check(guide.Data.ContainsKey("url") && guide.Data.ContainsKey("createdAt"), "Legacy mapper receives the standard URL and creation metadata after replacing Data");
        var stored = (await data.QueryAsync(new ProductQuery("U19-SMOKE-PACK") { IncludeDisabledProducts = true, IncludeDisabledVariants = true })).Products.Single();
        Check(string.Equals(stored.Brand?.BrandId, "U19-SMOKE-BRAND", StringComparison.OrdinalIgnoreCase), "Brand reference uses the overridden Brand ID");
        Check(stored.CategoryPaths.Any(path => path.BreadcrumbPathStartingFromRoot.Count == 1 && path.BreadcrumbPathStartingFromRoot[0].Id == "U19-SMOKE-EQUIPMENT"), "Category path uses the overridden Category ID");
        Check(stored.CategoryPaths.Length == 2 && stored.CategoryPaths.Any(path => path.BreadcrumbPathStartingFromRoot.Select(category => category.Id).SequenceEqual(new[] { "U19-SMOKE-ADVENTURES", "U19-SMOKE-DAYHIKES" })), "Multiple picked categories use canonical IDs, complete paths, skip folders, and deduplicate");
        Check(stored.AllVariants.Select(variant => variant.VariantId).Order().SequenceEqual(new[] { "U19-SMOKE-PACK-FOREST", "U19-SMOKE-PACK-SAND" }), "Direct child Variants use their overridden IDs");
        Check(stored.Data["contentTypeAlias"].ValueAs<string>() == "product", "Document Type metadata is one scalar value");
        var articleNode = content.GetRootContent().First(node => node.ContentType.Alias == "article");
        var storedArticle = (await data.QueryAsync(new ContentQuery(articleNode.Key.ToString("D")))).Contents.Single();
        Check(storedArticle.Data["editorialSource"].ValueAs<string>() == "Umbraco demo storefront", "Custom entity mapper runs with declarative mapping");
        Check(storedArticle.Data["searchText"].ValueAs<string>().Contains("reusable flask"), "Field-specific block converter feeds the final entity mapper");
        Check(storedArticle.Data["heroImage"].ValueAs<string>().StartsWith(configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!.TrimEnd('/') + "/images/"), "Global Data converter emits an absolute image URL");
        Check(stored.ListPrice?.Values?.Any(value => value.Currency.Value == "DKK" && value.Amount == 899) == true, "Typed async price conversion populates the built-in ListPrice");
        Check(stored.ListPrice?.Values?.Any(value => value.Currency.Value == "EUR" && value.Amount == 119.95m) == true
            && stored.SalesPrice?.Values?.Any(value => value.Currency.Value == "USD" && value.Amount == 99.95m) == true,
            "Typed price conversion reads independently entered currencies and amounts");
        string articleText = storedArticle.Data["searchText"].ValueAs<string>();
        Check(articleText.Contains("Inside the Grid area") && articleText.Contains("Nested trail advice")
            && !articleText.Contains("secret-list-note") && !articleText.Contains("secret-grid-note"),
            "Published nested blocks and Grid areas exclude hidden blocks and hidden subtrees");
        var localizedNode = content.GetRootContent().Single(node => node.ContentType.Alias == "localizedArticle");
        var localizedContent = (await data.QueryAsync(new ContentQuery(localizedNode.Key.ToString("D")))).Contents.Single();
        var localizedText = localizedContent.Data["searchText"].ValueAs<Multilingual>().Values!;
        string english = localizedText.Single(value => string.Equals(value.Language.Value, "en-US", StringComparison.OrdinalIgnoreCase)).Text ?? string.Empty;
        string danish = localizedText.Single(value => string.Equals(value.Language.Value, "da-DK", StringComparison.OrdinalIgnoreCase)).Text ?? string.Empty;
        Check(english.Contains("English nested heading") && !english.Contains("Dansk indlejret")
            && danish.Contains("Dansk indlejret overskrift") && !danish.Contains("English nested"),
            "Invariant outer blocks retain the language of their localized nested elements");
        var duplicate = await data.QueryAsync(new ContentQuery("U19-SMOKE-PACK"));
        Check(!duplicate.Contents.Any(), "Product is not also indexed as Content");
        return Results.Ok(new
        {
            passed = checks.Count,
            checks
        });
    }
}
