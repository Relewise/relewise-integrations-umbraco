# Umbraco 19 configuration — review draft

**Intermediate review document, updated 2026-11-18.** These examples describe the
API implemented on this branch, targeting Umbraco 19 / .NET 11. Review the API and
remaining decisions before treating it as a stable contract. Permanent documentation
will be finalized after that review. This folder contains the design and screenshots
so they can be removed together afterward.

## 1. Bootstrap with the existing API

The existing Content configuration remains supported:

```csharp
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration)); // Existing SDK connection settings.
builder.CreateUmbracoBuilder()
    .AddBackOffice().AddWebsite().AddComposers()
    .AddRelewise(options => options
        .AddContentType("homePage", mapping => mapping.AutoMap()) // Export supported fields as Content Data.
        .AddContentType("contentPage", mapping => mapping.AutoMap()))
    .Build();

var app = builder.Build();
await app.BootUmbracoAsync();
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackContentViews(); // Existing Content view tracking.
    })
    .WithEndpoints(u => { u.UseBackOfficeEndpoints(); u.UseWebsiteEndpoints(); });
await app.RunAsync();
```

## 2. The same capability with the new API

Replace the integration callback above with:

```csharp
.AddRelewise(options => options
    .MapContent("homePage", mapping => mapping.AutoMap()) // Same Content export and automatic conversion.
    .MapContent("contentPage", mapping => mapping.AutoMap()))
```

Use `u.TrackEntityViews()` in place of `u.TrackContentViews()` to infer the view event
from the mapped entity type. Both methods remain available; these Content mappings
still produce Content views. Search-provider registration is independent.

Existing `UseMapper(new ExistingMapper())`, `IContentTypeMapping`, global property
converters, numeric IDs and the `UseContentKeysAsIds` option remain available.
The old methods are retained; any future obsolescence needs migration guidance.
Tracking-only mapping uses `mapping.Export = false`; `mapping.TrackViews = false`
suppresses automatic views for a mapping.

## 3. Entity and field mapping

Each example replaces or extends the `AddRelewise` callback. SDK entity types come
from `Relewise.Client.DataTypes`; named converters below are application code.

```csharp
.AddRelewise(options =>
{
    options.MapContent("article", mapping =>
    {
        mapping.Map(entity => entity.DisplayName, "heading"); // Built-in multilingual name.
        mapping.MapData("summary"); // Defaults to Data["summary"].
        mapping.MapData("bodyText", data => data.DataKey = "body"); // Optional destination key.
        mapping.MapData("heroImage", data =>
        {
            data.DataKey = "imageUrl"; // Customer-defined Data; no special image field.
            data.UseConverter<MediaUrlConverter>(); // Field-specific media-to-URL conversion.
        });
    });
    options.MapContent("guide", mapping => mapping.AutoMap()); // A second Content Document Type.

    options.MapBrand("brand", mapping =>
    {
        mapping.IdFromProperty("brandId"); // Canonical ID also used by Product references.
        mapping.Map(entity => entity.DisplayName, "brandName"); // Brand has a single string DisplayName.
    });
    options.MapProductCategory("productCategory", mapping =>
    {
        mapping.IdFromProperty("categoryId"); // Canonical IDs for every Category path entry.
        mapping.Map(entity => entity.DisplayName, "heading"); // Multilingual Category name.
    });
    options.MapContentCategory("topic", mapping =>
    {
        mapping.IdFromProperty("topicId"); // Separate editorial taxonomy.
        mapping.Map(entity => entity.DisplayName, "heading");
    });
    options.MapProduct("product", mapping =>
    {
        mapping.IdFromProperty("sku"); // Shared by export, references, search resolution and views.
        mapping.Map(entity => entity.DisplayName, "heading"); // Preserve published languages.
        mapping.Map(entity => entity.ListPrice, "listPrices", property => property.UseConverter<PriceConverter>()); // Typed MultiCurrency result.
        mapping.Map(entity => entity.SalesPrice, "salePrices", property => property.UseConverter<PriceConverter>()); // Source must identify currencies.
        mapping.Map(entity => entity.Assortments, "markets"); // List of integer assortment IDs.
        mapping.MapData("description"); // Custom Data entry.
        mapping.BrandFrom("brand"); // Single document picker, resolved through MapBrand.
        mapping.AdditionalCategoriesFrom("extraCategories"); // Add complete mapped Category paths.
        mapping.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku"); // Variant ID within its parent Product.
            variant.Map(entity => entity.DisplayName, "heading");
            variant.MapData("size");
        });
    });
})
```

`Map(destinationProperty, sourcePropertyAlias, configure)` selects a direct built-in
value property. Its optional callback receives `PropertyMappingOptions<TValue>`.
Identity and relationships use dedicated methods. `MapData(sourcePropertyAlias,
configure)` requires only the source alias; its optional `DataMappingOptions`
provides `DataKey` and field-specific converter selection. Summary, body and image
are ordinary customer-defined Data keys; search weighting is configured separately.

Use `documentTypeAlias` for a mapping's Document Type, `sourcePropertyAlias` for a
source field and `providerAlias` for a query registration. Preserve existing public
parameter names for named-argument compatibility. Use one field-mapping approach
per entity: automatic Data mapping, explicit fields, or a custom mapper.

## 4. Identity, relationships and metadata

- IDs default to numeric Umbraco node IDs. `options.UseContentKeysAsIds = true`
  selects GUID keys for mappings without an override. `IdFromProperty` requires an
  invariant, nonempty scalar value. Changing identity after ingestion is a migration.
- Every reference applies the **referenced entity's** ID strategy, for single
  pickers and every list item. With `MapBrand(... IdFromProperty("brandId"))`,
  `BrandFrom("brand")` uses that picked document's `brandId`, not its native node ID.
  The same resolution applies to Category paths, additional Categories and Variants.
- Variants are direct children of their Product. Category ancestors form paths;
  unmapped folders are skipped. Additional picked Categories contribute complete,
  deduplicated paths. Arbitrary Variant-parent pickers are not supported.
- `MapContent` permits multiple distinct Document Types. Product, Brand, Product
  Category and Content Category each permit one registration. Duplicate registrations,
  multiple targets for one Document Type and conflicting canonical IDs fail clearly.
  The existing `AddContentType` merging contract remains supported.
- Every exported entity has one scalar `contentTypeAlias`, maintained regardless of
  search configuration. Node-key and site metadata support result resolution and
  membership. `rwUmbracoPublished` records source-page availability, independently
  of the Relewise Product's enabled state; it is maintained in both ownership models.
- Automatic views derive their SDK event from the mapping target. A Variant view
  includes its parent Product ID; Category views use mapped Category ancestors.
  Search and views resolve the same request-scoped `IRelewiseUserLocator`.

## 5. Converters, custom mappers and context

Built-in property converters are strongly typed:

```csharp
public interface IPropertyMappingConverter<TValue>
{
    TValue Convert(PropertyMappingContext context);
}
```

`Map(entity => entity.ListPrice, ...)` therefore requires a converter producing
`MultiCurrency` with the SDK's nullable contract; DisplayName requires its actual
SDK type. A decimal alone supplies no currency. Without a selected converter,
compatible built-in conversions apply; unsupported inputs fail with source context.

`MapData` uses the built-in/global converter pipeline by default. Register custom
Data converters with the existing API:

```csharp
builder.Services.AddValueConverter<MyCustomConverter>(); // Implements IRelewisePropertyValueConverter.
```

All globally matching converters run, preserving existing behavior and singleton
lifetimes. A field-specific `UseConverter<T>()` selects only that converter for the
field. It does not add it globally. Resolve explicitly registered types in the
export scope; otherwise activate through DI and dispose within that operation.
Global singleton converters must not capture scoped dependencies.

| Context | Information available |
| --- | --- |
| Typed field converter | Owning `PublishedContent`, current `SourceElement`, `Property`, `CulturesToPublish`, `DestinationProperty`, `Services`, cancellation. |
| Existing Data converter | The same owner/element/operation information when supplied, plus its existing current `Culture` and Data-writing API. |
| `IEntityMapper<TEntity>.MapAsync` | Typed destination `Entity`, owning document, cultures, scoped services and cancellation. |

Nested conversion retains the owning document while changing the current element
and property. Legacy property-only calls remain valid but cannot reconstruct an
owner. Typed field conversion is synchronous; async mapping work can use an entity
mapper. Current custom mappers must retain the shared resolver's ID; computed-ID
extensibility remains a review decision.

## 6. Contribute to a commerce Product

```csharp
options.MapProduct("productEditorialPage", mapping =>
{
    mapping.IdFromProperty("commerceProductId"); // Address the commerce integration's Product.
    mapping.AdministrativeActions = AdministrativeActionPolicy.None(); // Commerce controls enable, disable and delete.
    mapping.MapData("summary", data => data.DataKey = "editorialSummary"); // Declare the contribution.
});
```

| Policy | Behavior |
| --- | --- |
| `AdministrativeActionPolicy.Synchronize()` | Default; follows Umbraco publication and removal with enable/disable/delete actions. |
| `AdministrativeActionPolicy.None()` | Sends declared contributions and required metadata without administrative actions or replacement of externally owned Variants. |

The policy type is immutable, with named factories and internal construction.
Referenced Brand/Category mappings retain their own policies. With `None()`,
unpublish stops business-field exports and marks the source page unavailable;
previous contributions remain. A partial update can create a missing entity, so
this is not atomic update-if-existing behavior. `ClearAndReplace` is incompatible
with `None()`; declared collections are not an automatic merge with external values.

## 7. One indexing pipeline; independent search providers

One shared writer handles core change events, dashboard/explicit exports and
rebuilds. It resolves IDs, mapping, dependencies and lifecycle actions consistently.
Removal events for already-unpublished documents also enter that pipeline; draft
saves do not export draft values. Rebuilds reconcile known source associations and
leave unrelated Dataset entities intact. Independent notification exporters are
not registered. Publication and rebuild operations share coordination with short
database locks; no outer database transaction surrounds mapping or network calls.

Umbraco can have several indexing pipelines; all Relewise providers query the
Dataset's **single Relewise index**. Registering providers never adds another writer.
Examine can continue indexing and serving ordinary queries.

```csharp
options.SearchProviders.AddContent("editorial", provider =>
{
    provider.DocumentTypeAliases = ["article", "guide"]; // Optional selection among Content mappings.
});
options.SearchProviders.AddProduct("catalogue", provider =>
{
    provider.DisplayedAtLocation = "Website product search"; // Stable analytics/merchandising label.
    provider.ConfigureRequest((request, context) =>
    {
        request.Currency = new Currency("DKK"); // Per-request Product search defaults.
    });
});
options.SearchProviders.AddProduct("offers", provider =>
{
    provider.ConfigureRequest((request, context) =>
    {
        request.Filters!.Add(new ProductDataFilter("isOnSale", new EqualsCondition(true))); // Add an exported-Data condition.
    });
});
options.SearchProviders.DefaultPublishedContentProviderAlias = "editorial"; // Handles ordinary Umbraco published-content queries.
```

Omitting `DefaultPublishedContentProviderAlias` preserves the existing provider.
Named queries remain independently selectable through `ISearcherResolver`; aliases
identify query configurations, not Document Types. Only Content and Product provider
types are exposed. Content aliases become a source-metadata filter before paging
and facets; Product providers use the one Product mapping.

Each execution, including facet queries, creates a fresh typed SDK request. The
callback gets `Services`, `User` and `Culture`; mandatory site/mapping/access,
caller query, paging and the default Relewise index remain enforced. Language
follows the query culture with the site's default as the SDK-language fallback;
there is no installation-wide language or configurable Relewise index ID.
Variant matches resolve to one Product page before paging.

`DisplayedAtLocation` is always nonempty: use the callback value, then the configured
provider value, then `Umbraco search: {providerAlias}`; ignore blank values.

Product providers default to requiring a published page. Advanced callers can opt out:

```csharp
options.SearchProviders.AddProduct("commerceCatalogue", provider =>
{
    provider.RequirePublishedPage = false; // Allow mapped Products whose Umbraco page is unavailable.
});
```

This omits only the `rwUmbracoPublished` filter. The metadata is always maintained;
site, mapping, access and culture restrictions remain. Content providers always
require a published page. This option neither enables a disabled Product nor adds
unmapped commerce Products. Results remain Umbraco document IDs; callers disabling
the requirement handle unavailable pages and commerce destinations themselves.

## 8. Review decisions still open

- Shared computed-ID resolution and compatibility with legacy custom mappers.
- Removing an existing Brand relationship through an emptied picker.
- Async converter requirements and stable culture-specific Data/field-withdrawal behavior.
- Brand DisplayName's default language selection.
- Query capability boundaries and rebuild/coordination requirements at scale.

Follow-up work is tracked separately. Current behavior and limitations have concise
notes in the [mapping guide](../../docs/entity-mapping.md) and
[search guide](../../docs/umbraco-19-search.md); a full documentation refresh awaits
stabilization. Preserve existing public method signatures and XML documentation,
and expose additional options/types only when a concrete capability requires them.

## 9. Screenshots for review

These are local smoke-test captures from implementation iterations. The local
storefront and raw execution reports are not included in this branch.

| Screenshot | Review focus |
| --- | --- |
| [Product search](images/01-storefront-product-search.jpg) | Product results and storefront. |
| [Product page](images/02-product-detail.jpg) | Product identity on a CMS-rendered page. |
| [Variant page](images/03-variant-detail.jpg) | Variant identity with its parent Product. |
| [Content search](images/04-content-search.jpg) | Content query results. |
| [Product in My Relewise](images/05-my-relewise-product.jpg) | Ingested Product fields. |
| [Variants in My Relewise](images/06-my-relewise-variants.jpg) | Child Variant ingestion. |
| [Unpublished page excluded](images/07-unpublished-product-search.jpg) | Publication lifecycle outcome. |
| [Danish search](images/09-multilingual-search.jpg) | Multilingual Content search. |
| [SDK identity correlation](images/10-storefront-identity-correlation.jpg) | Shared visitor and canonical entity IDs. |
| [Received view events](images/11-my-relewise-all-view-types.jpg) | Content/Product/Variant/Brand/Category events and Category paths. |
