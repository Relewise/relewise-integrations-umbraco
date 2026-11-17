# RFC: configuring the Umbraco integration

**Draft for discussion, revised 2026-11-17. No implementation is authorized by this RFC.**

The proposal is additive: keep today's API working, show its replacement with the
same behavior, then add Product, Variant, Category, and Brand mapping. Umbraco 19 /
.NET 11 remains the target. Only example 1 uses existing APIs throughout; subsequent
examples contain proposed methods and are not instructions for the current package.

## 1. Bootstrap a Content site with today's API

This uses the existing sample's configuration: two document types exported as
Content, with automatic page-view tracking. Connection settings still come from
the existing `Relewise` configuration section (`DatasetId`, `ApiKey`, `ServerUrl`).

```csharp
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration)); // Existing SDK connection setup.

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("homePage", type => type.AutoMap()) // Export supported properties as Content data.
        .AddContentType("contentPage", type => type.AutoMap()))
    .Build();

var app = builder.Build();
await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackContentViews(); // Send ContentView for these document types.
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

This must keep working. Content IDs remain numeric unless the existing GUID option
is enabled. Existing custom mappers, property converters, multilingual values,
category paths, metadata, and publication/removal capabilities remain available.
The default user locator remains anonymous; an existing custom
`IRelewiseUserLocator` remains supported.

## 2. Bootstrap the same site with the proposed API

Same document types, data mapping, IDs, and tracking. This does **not** opt into
Umbraco's new search provider or change the exported Content model.

```csharp
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration)); // Unchanged SDK connection setup.

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options => options
        .MapContent("homePage", type => type.AutoMap()) // Explicit Content target; same automatic conversion as today.
        .MapContent("contentPage", type => type.AutoMap()))
    .Build();

var app = builder.Build();
await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackEntityViews(); // Derive the event from the mapping target: ContentView here.
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
```

Only two names change: `AddContentType` becomes `MapContent`, and
`TrackContentViews` becomes the generalized `TrackEntityViews`. The reason for
`MapContent` is to make the target clear beside `MapProduct`, `MapBrand`, etc.
The old methods forward internally and may later become obsolete, with migration
instructions. They are not removed in this release.

Existing `UseMapper(new ExistingMapper())` and `IContentTypeMapping` remain usable
inside `MapContent`; migration does not require rewriting the mapper. New mapper
contracts should be introduced only where the other entity types require them.

The existing GUID setting stays explicit. Tracking-only Content registrations
also remain supported; their proposed equivalent is
`MapContent("page", type => type.Export = false)`. For export without automatic
views, omit the view middleware or set `type.TrackViews = false` on that mapping.
These switches do not allow choosing an inconsistent event type.

## 3. Choose readable fields and optionally use native Umbraco search

Starting from example 2, replace its `AddRelewise` callback with:

```csharp
.AddRelewise(options =>
{
    options.MapContent("article", article =>
    {
        article.TitleFrom("heading"); // Readable DisplayName/title; defaults to the published page name.
        article.SummaryFrom("summary"); // Export a summary field for result presentation.
        article.SearchTextFrom("bodyText"); // Export selected searchable text; do not append it to DisplayName.
        article.ImageFrom("heroImage"); // Export a result image URL from this property.
    });

    options.Search.UseRelewiseForPublishedContent = true; // Replace Umbraco's published-content search provider with Relewise.
})
```

These are opt-in field selections, not changes to `AutoMap()` in example 2.
Published URLs and the selected fields should be usable by a frontend calling
Relewise directly. Use custom mapping for block extraction or other complex data.
Custom mapping takes over application field mapping, as it does today; it is not
silently combined with AutoMap. Fields selected declaratively must have one owner.

The search switch means exactly that Relewise serves Umbraco's published-content
search abstraction. It does not create a search page, enable every Umbraco index,
or become necessary for exporting data. Keep it off for an indexing-only/headless
integration. Draft/media/member/Library Element indexes remain separate explicit
choices, not part of this example.

**No configurable Relewise `IndexId`.** Use the Dataset's default index and document
which exported fields should be indexed and weighted in My Relewise. Do not create
or rewrite another index automatically. The documentation confirms one default
index is sufficient in the vast majority of cases, including multiple languages;
additional indexes are an optional special case. That capability does not justify
another option in this integration's initial public API.

**No site-wide search `Language`.** Export the page's published cultures as
multilingual data. Derive each search request's Relewise language from its effective
Umbraco culture; use the site's default culture when no effective culture exists.
This language selection must not change the core API's culture-filter semantics.
Relewise's configured language keys must match those used in the exported data and
requests. Do not silently force every request to English. Culture aliases or
fallback policies need a concrete use case before adding more configuration.

Keep title, summary, and body separate so the customer can configure weighting.
Do not hide unwanted page text in a catch-all DisplayName merely to use the default
index without configuration.

## 4. Map a Product catalogue with hierarchical variants

This replaces the mapping callback in the same bootstrap; it does not introduce
another SDK connection or integration registration system.

```csharp
.AddRelewise(options =>
{
    options.MapProductCategory("productCategory", category =>
    {
        category.IdFromProperty("categoryId"); // Stable ID reused by Product category paths.
        category.TitleFrom("title"); // Category display name.
    });

    options.MapBrand("brand", brand =>
    {
        brand.IdFromProperty("brandId"); // Stable ID referenced by Products.
        brand.ImageFrom("logo"); // Optional presentation data.
    });

    options.MapProduct("product", product =>
    {
        product.IdFromProperty("sku"); // Product ID; can match an existing commerce identifier.
        product.TitleFrom("title"); // Product display name.
        product.BrandFrom("brand"); // Single-item picker pointing to a mapped Brand document.
        product.AdditionalCategoriesFrom("additionalCategories"); // Optional multi-item picker of mapped Product Categories.
        product.UseMapper<ProductMapper>(); // Application mapper for prices, specifications, and other Product data.

        product.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku"); // Variant ID within the parent Product, not a standalone Product ID.
            variant.UseMapper<VariantMapper>(); // Application mapper for variant fields.
        });
    });
})
```

`ProductMapper` and `VariantMapper` are application implementations. Their exact
contracts are a later design decision; do not add a parallel public framework for
each entity type just to support these examples. Declarative identity and
relationships remain integration responsibilities when a mapper fills other data.

The rules are deliberately limited:

- One primary entity target per document type. No dual Content/Product mapping.
- Variants must be direct child documents of their Product. No arbitrary parent
  picker, cross-tree variant links, or block-based variants initially.
- Product category paths come from mapped category ancestors, skipping folders.
  Additional picker targets add their complete category paths, without duplicates.
- Brand references use the Brand mapping's ID. Variants inherit the Product's
  category/brand relationships. Umbraco personalization segments are not variants.
- `MapContentCategory` provides the corresponding editorial taxonomy option;
  Product Categories and Content Categories remain distinct.
- Automatic events follow the target: ContentView, ProductView (with Variant ID
  for a variant), the matching category view, or BrandView. Listing variants on a
  Product page does not emit a view for every variant.

## 5. Supplement an externally managed catalogue

Ordinary Content mappings can coexist with Product supplementation:

```csharp
.AddRelewise(options =>
{
    options.MapContent("guide", guide => guide.AutoMap()); // Keep exporting guides as ordinary Content.

    options.MapProduct("productEditorialPage", product =>
    {
        product.IdFromProperty("commerceProductId"); // Address the existing commerce Product.
        product.Ownership = EntityOwnership.Supplement; // Contribute selected fields without owning Product lifecycle.
        product.DataFrom("editorialSummary", "summary"); // Destination Data key, then Umbraco property alias.
        product.DataFrom("editorialImage", "image"); // Update only this declared editorial field.
    });
})
```

New typed mappings otherwise default to owning their entity. Supplementation sends
only approved fields: no implicit DisplayName, URL, price, brand, category, or
variant replacement. Unpublishing withdraws the editorial contribution; it does
not disable/delete the external Product. Removed fields must be removable without
clearing other systems' data. Replacing an external Product's variant collection
is not allowed.

Validate selective removal and missing-target behavior before implementation.
An existence read followed by an upsert is not an atomic update-if-existing
operation. We must not promise that supplementation cannot create partial Products
until that backend behavior has been resolved.

**Why the earlier `Source` setting?** It was intended to identify ownership for
cleanup and field withdrawal, not to configure mapping or search. That ownership
problem is real, but a mandatory public `Source` string is not established as the
right solution. Remove it from the proposed bootstrap. Track local contributions
internally and design explicit ownership/adoption rules for shared catalogues.
Neither an automatic installation ID nor a Source string alone isolates cloned
environments or prevents an external writer overwriting a field. Do not solve
this by adding another unexplained setter.

## 6. Decisions to settle before implementation

1. **Compatibility and public API:** agree the first two examples, aliases and
   eventual obsolescence. Keep existing mapper/converter capabilities. Follow the
   current code style and XML documentation; keep dispatch, transport, ownership
   storage, and lifecycle machinery internal.
2. **Identity and lifecycle:** export, search, references, and tracking must address
   the same entity, using the same request-scoped `IRelewiseUserLocator`. Preserve
   existing Content IDs. Prove culture/segment behavior before accepting another
   separate search-entity family. Handle variant moves and shared category/brand
   changes through their affected aggregates, including indexing-only use.
3. **Search and supplementation:** settle Product/variant result collapse, mixed
   Content/Product ranking and paging, unmapped document types, protected/headless
   access, and safe contribution cleanup. These are implementation gates, not
   additional configuration knobs to expose prematurely.

Recommended order: agree the equivalent Content bootstrap first; prove shared
identity and search semantics; add owned Product/Variant/Category/Brand mapping;
then add supplementation once its update guarantees are clear. No implementation
changes are requested by this document.

## References

- [Existing setup and tracking](README.md#using-relewiseintegrationsumbraco)
- [Existing custom mapper and converter contracts](docs/custom-content-mapping.md)
- [Relewise Search Index: default index and optional additional indexes](https://docs.relewise.com/docs/myrelewise/search-index.html)
- [Relewise languages, multilingual data, and request language](https://docs.relewise.com/docs/intro/languages-currencies.html)

The index/language statements were checked against the Relewise Developer MCP and
the SDK's `SearchRequest.IndexSelector`, `SearchIndexSelector.Default`, and
per-request `Language`. Existing guides describe current behavior, not the
proposed APIs in examples 2–5.
