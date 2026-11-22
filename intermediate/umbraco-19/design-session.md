# Umbraco 19 configuration — review draft

**Intermediate review document, updated 2026-11-20.** These examples describe the
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

## 2. Equivalent Content configuration with the new API

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
The legacy mapper/converter contracts are obsolete with migration guidance; adapters
preserve them until removal in the next major release of `Relewise.Integrations.Umbraco`. AutoMap retains its existing output.
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
parameter names for named-argument compatibility. Choose automatic Data mapping or
explicit fields; either can be combined with one final custom entity mapper.

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
  deduplicated paths. Arbitrary Variant-parent pickers are not supported. Brand and
  additional-Category picker properties must be culture and segment invariant.
- `MapContent` permits multiple distinct Document Types. Product, Brand, Product
  Category and Content Category each permit one registration. Duplicate registrations and
  multiple targets for one Document Type fail clearly. Full exports also detect
  conflicting canonical IDs; IDs must be unique during delta updates.
  The existing `AddContentType` merging contract remains supported. Its bare
  registration is tracking-only; bare `MapContent` enables export by default.
- Every exported entity has one scalar `contentTypeAlias`, maintained regardless of
  search configuration. Node-key and site metadata support result resolution and
  membership. `rwUmbracoPublished` records source-page availability, independently
  of the Relewise Product's enabled state; it is maintained under both administrative-action policies.
- Automatic views derive their SDK event from the mapping target. A Variant view
  includes its parent Product ID; Category views use mapped Category ancestors.
  Search and views resolve the same request-scoped `IRelewiseUserLocator`.

## 5. Converters, custom mappers and context

Built-in property converters are strongly typed:

```csharp
public interface IPropertyMappingConverter<TValue>
{
    ValueTask<TValue> ConvertAsync(PropertyMappingContext context, CancellationToken cancellationToken);
}
```

`Map(entity => entity.ListPrice, ...)` therefore requires a converter producing
`MultiCurrency` with the SDK's nullable contract; DisplayName requires its actual
SDK type (`Multilingual` for Content/Product/Category names and `string` for Brand).
A decimal alone supplies no currency. Without a selected converter,
compatible built-in conversions apply; unsupported inputs fail with source context.

`MapData` uses the built-in/global converter pipeline by default. Register custom
Data converters with:

```csharp
builder.Services.AddAsyncValueConverter<MyCustomConverter>(); // Implements IAsyncRelewisePropertyValueConverter.
```

New global Data converters are scoped. The last matching custom registration wins
and built-ins are fallbacks, regardless of registration before/after `AddRelewise`.
A field-specific `UseConverter<T>()` selects only that converter for the field and
does not register it globally. Resolve registered types in the export scope;
otherwise activate through DI and dispose within that operation. Legacy synchronous
converters retain their selection behavior and singleton registrations via adapters.

| Context | Information available |
| --- | --- |
| Typed field converter | Owning `PublishedContent`, current `SourceElement`, `Property`, `CulturesToPublish`, `DestinationProperty`, `Services`, cancellation. |
| Existing Data converter | The same owner/element/operation information when supplied, plus its existing current `Culture` and Data-writing API. |
| `IEntityMapper<TEntity>.MapAsync` | Typed destination `Entity`, owning document, cultures, scoped services and cancellation. |

Nested conversion retains the owning document while changing the current element
and property. Legacy property-only calls remain valid but cannot reconstruct an
owner. Typed and Data conversion support asynchronous work and cancellation while
AutoMap preserves existing culture-dependent scalar/multilingual/suffixed output.

```csharp
mapping.Map(entity => entity.DisplayName, "heading"); // Declarative business fields first.
mapping.MapData("bodyText");
mapping.UseMapper<EditorialMapper>(); // IEntityMapper<Content> runs last, independent of registration order.
```

`IEntityMapper<TEntity>.MapAsync(context, cancellationToken)` populates the supplied
`context.Entity` and returns `Task`, not an SDK update. Defaults, declared fields,
converters, relationships and child Variants are prepared first. The mapper may
inspect/override business fields; identity remains controlled by ID configuration.
Reserved metadata is applied afterward, and the integration constructs the SDK
update. The legacy Content adapter retains its returned-update/ID behavior; use
`IdFromProperty` during migration to keep references and tracking aligned.

Separately registered legacy custom Data converters still run, even when an async
custom converter overrides a built-in. Avoid overlapping Data keys.

Brand.DisplayName follows the SDK's single-string type. With `Synchronize()` and
no explicit mapping, its node-name default uses Umbraco's default language, resolved once per operation, or the first available published
culture in case-insensitive ordinal order.
The same selection applies to a localized property mapped to its DisplayName;
an explicit typed converter can override it.

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
rebuilds. Source values come from published content; draft saves do not export draft
values. Consecutive background callbacks accumulate native change records and schedule
one flush on Umbraco's existing background queue. Each flush consumes up to 100
change records; descendant changes can expand to more documents. Published values
and core indexing metadata are resolved during the flush, and each affected node
(and Variant parent Product) is mapped once within that batch. Isolated changes run
without an intentional debounce delay; separately arriving callbacks may form
several batches.

Captured terminal deletions, explicit exports and rebuilds separate change batches.
Operations already accepted by this writer precede the boundary; later operations
follow it. Explicit operations await their own delivery and can execute from within
Umbraco's worker without waiting on a second worker. A failed background batch is
logged without automatic retry; following queued removals remain scheduled.
Cancellation leaves other callers' accepted work intact. An inline caller finishes
its current operation's scope before cancelling between batches; shutdown also
cancels explicit waits. Explicit operations execute inline without scheduling an
additional flush. Mappers/converters must not recursively request exports or rebuilds; such
calls fail immediately rather than waiting for the active writer.
The integration stores no identity/dependency records or leases in Umbraco.
Relewise source metadata identifies exports after unpublication, including custom IDs.
A standard Umbraco deletion notification captures the configured entity ID before
the node disappears and queues removal after commit through the same writer.
Deleted Variants refresh their parent. If an unpublished draft changed its ID,
a rebuild reconciles any former identity left behind.

A completed full export reconciles missing entities by its unique export generation,
source scope and administrative policy; failures do not trigger final cleanup.
Unrelated Dataset entities remain intact. Delta updates need not refresh other
Products referencing changed Brands/Categories; re-export those Products or rebuild.
A changed canonical ID can leave its former entity until rebuilding. Previously
contributed business fields are not automatically withdrawn. Interrupted background
work can require a rebuild. Writer coordination is process-local; coordinate full
rebuilds across instances externally. Full collection remains in memory initially;
measure it before introducing additional rebuild machinery.

Umbraco can have several indexing pipelines; all Relewise providers query the
Dataset's **single Relewise index**. Registering providers never adds another writer.
Examine can continue indexing and serving default published-content searches.

```csharp
options.SearchProviders.AddContent("editorial", provider =>
{
    provider.DocumentTypeAliases = ["article", "guide"]; // Optional selection among Content mappings.
    provider.UnsupportedSegmentBehavior = UnsupportedSegmentBehavior.Throw(); // Default: reject segment-specific requests.
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
options.SearchProviders.DefaultPublishedContentProviderAlias = "editorial"; // Handles queries through Umbraco's default published-content provider.
```

Omitting `DefaultPublishedContentProviderAlias` preserves the existing provider.
Named queries remain independently selectable through `ISearcherResolver`; aliases
identify query configurations, not Document Types. Only Content and Product provider
types are exposed. Content aliases become a source-metadata filter before paging
and facets; Product providers use the one Product mapping.

Each execution, including facet queries, creates a fresh typed SDK request. The
callback gets `Services`, `User` and `Culture`; mandatory site/mapping/access,
request user, language, caller query, paging and the default Relewise index remain enforced. Language
follows the query culture with the site's default as the SDK-language fallback;
a null query culture selects invariant documents only. There is no installation-wide
language or configurable Relewise index ID.
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

## 8. Supported operations, limitations and testing

Culture-specific numeric/date queries select internal culture-specific fields for
filtering, sorting and facets. AutoMap output is unaffected. Segment-specific
indexing is unsupported: the provider policy offers `Throw()` (default) or
`Ignore()`, which executes the query without its segment argument.
Suggestions requested through Umbraco use Relewise predictions with the same
provider target, language, visitor, membership and access filters as the search.
A nonblank query with `maxSuggestions > 0` adds one prediction request.
Empty Brand pickers retain the existing relationship under SDK partial updates.

The maintained [Relewise.DemoStorefront](../../samples/Relewise.DemoStorefront/README.md)
centralizes runnable mapping, converter, search and tracking examples. F5 creates a local
SQLite database and seeds content, blocks, languages and bundled images. Valid
local Relewise settings in an ignored
`local.settings.json` connect the demo to an existing local Dataset; without those
settings, the demo exposes published CMS content preview with setup guidance.
Search, facets, predictions, Offers, cart and checkout require Relewise; disabled
features are also rejected by their backend routes. Invalid credentials remain an
explicit connection failure. The demo does not substitute another search provider.
`DemoProductMapper` writes `inStock` and per-currency `onSale_EUR`, `onSale_DKK`
and `onSale_USD` Boolean Data after price mapping, including `false` for no discount.
`provider.MapBooleanData(fieldName, dataKey)` binds an Umbraco keyword filter/facet
to Boolean Product Data; the key defaults to the query field name. Stock and sale filters
and facets share these business values without an additional indexed projection.
`StorefrontProductIndexer` supplies the remaining typed query projection.
Opt-in diagnostics support the shared acceptance tests, including publish/edit/
unpublish with no Relewise query providers registered.

Package-level installed testing covers the indexing-only/Examine combination, named
providers with either query default, publication/draft/edit/removal, child Variants,
culture membership, ID changes, commerce contributions and shared tracking IDs.
Scope validation and converter disposal/cancellation have regression coverage.

Local installed-package checks combined 100 publications into 13 write requests
and 200 publications into 16, with no remote entity reads during publication.
Batch counts depend on callback arrival timing; these small measurements do not
establish large-site capacity. Full rebuild collection remains in memory.

Umbraco's core indexing dashboard requires a numeric document count. The provider
returns its required placeholder `0` and `Unknown` health; Relewise statistics are
unavailable through this integration. A notice in the existing Relewise dashboard
explains that zero does not indicate an empty Dataset. No counter, extra SDK queries
or replacement of Umbraco's standard statistics UI is introduced.
The maintained [mapping](../../docs/entity-mapping.md) and
[search](../../docs/search.md) guides describe supported behavior. This
intermediate document retains implementation-review context and smoke-test evidence.

The demo uses server-rendered Umbraco pages with Vue components for the shared
header, listings and cart. Global search opens a dedicated results page, while
Offers has its own sale-filtered route and navigation state. The same server query
supplies initial data and reactive filter/sort/page updates. Cart changes use
asynchronous requests and server-authoritative validation; checkout uses a focused
review page and native order submission. Expandable footer details show the active
provider and configuration without interrupting shopping. SDK requests, canonical
IDs and tracking remain on the server. See the
[demo guide](../../samples/Relewise.DemoStorefront/README.md) for build and interaction details.

Local review covers desktop and mobile navigation, Product/Journal search, Offers,
market changes, asynchronous cart updates and checkout with a local Relewise
Dataset, plus content preview and backend feature gates without credentials. Concurrent add retries, shared stock across
Variants, CSRF rejection, canonical tracking IDs and repeated checkout submissions
are also exercised. Invalid credentials return an explicit error without switching
providers. These checks do not constitute external usability certification.

## 9. Screenshots for review

These are local smoke-test captures from implementation iterations, rather than
a guarantee that every capture shows the current layout. The latest dashboard
provider and connection layouts are linked below. The maintained
storefront is included as a sample; raw execution reports and credentials stay local.

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
| [F5 without Relewise credentials](images/12-f5-content-preview.jpg) | Seeded CMS content preview with disabled search and shopping and a setup link. |
| [Installed Product search](images/13-installed-product-search.jpg) | Named Product provider in the maintained storefront. |
| [Installed Variant page](images/14-installed-variant-view.jpg) | Canonical Variant ID with its parent Product. |
| [Installed tracking correlation](images/15-installed-identity-correlation.jpg) | One visitor across searches and all supported view events. |
| [Local My Relewise Product](images/16-local-my-relewise-product.jpg) | Exported display name, image and Brand. |
| [Local My Relewise Variants](images/17-local-my-relewise-variants.jpg) | Both canonical child Variant IDs. |
| [English nested blocks](images/18-localized-nested-blocks-en.png) | Published nested editorial text in English. |
| [Danish nested blocks](images/19-localized-nested-blocks-da.png) | The same nested elements resolve their Danish values. |
| [Multiple currencies](images/20-multi-currency-product.png) | Independently entered list and sales prices in DKK, EUR and USD. |
| [Europe market](images/21-europe-market-product.png) | Header market selection drives the Product's displayed EUR prices. |
| [Denmark market](images/22-denmark-market-article.png) | Switching market opens the corresponding Danish article and nested blocks. |
| [Dashboard overview](images/23-dashboard-overview.png) | Integration branding and configuration overview. |
| [Product mapping details](images/25-dashboard-product-mapping-details.png) | Field converters, relationships and canonical IDs. |
| [Custom mapper details](images/26-dashboard-mapper-names-and-publication.png) | Mapper type names and publication/removal behavior. |
| [Export and Maintenance](images/29-dashboard-export-and-maintenance.png) | Full export and scoped permanent-deletion controls. |
| [Connection details](images/30-dashboard-connection-details.png) | Current separate default/named tables and usage explanations. |
| [Final dashboard provider review](images/32-dashboard-final-review.png) | Current Content/Product provider details and index-statistics notice. |
| [Brand filters](images/33-storefront-brand-filters.jpg) | Multiple Brand selections, counts, removable chips and actual Brand labels on Product cards. |
| [Brand page](images/37-storefront-brand-page.jpg) | Ridgeway's fixed Brand scope retains other facets and omits redundant Brand selection. |
| [Shopping on desktop](images/38-storefront-shopping-desktop.jpg) | Shared search header, separate navigation row, compact introduction and reactive listings. |
| [Shopping on mobile](images/39-storefront-shopping-mobile.jpg) | Active-filter count, removable chips and market-specific savings badges. |
| [Cart](images/34-storefront-cart.jpg) | Asynchronous quantity/removal controls and separate order summary. |
| [Checkout](images/35-storefront-checkout.jpg) | Focused review with a clear demo disclaimer and no shopping navigation. |
| [Mobile checkout](images/36-storefront-mobile-checkout.jpg) | Stacked review and summary with the same order flow. |
| [Product specifications](images/40-storefront-product-specifications.jpg) | Representative illustration and editable native CMS specification fields. |
| [Mobile filter chips](images/41-storefront-mobile-filter-chips.jpg) | Reactive mobile filtering, result counts and removable chips; no separate Apply/Sort step. |
| [Global search](images/42-storefront-global-search.jpg) | Dedicated results page with Product and Journal scopes, independent of the previous collection or Offers filter. |
| [Offers](images/43-storefront-offers.jpg) | Dedicated landing page, active navigation and sale prices for the selected market. |
| [Added to cart](images/44-storefront-added-to-cart.jpg) | Confirmation drawer with authoritative item, quantity and basket total. |
| [Market selection](images/45-storefront-market-selection.jpg) | Compact header control opens language/currency choices. |
| [Desktop shopping filters](images/46-storefront-price-facet-desktop.jpg) | Inclusive custom price range combined with market-specific On sale filtering. |
| [Mobile shopping filters](images/47-storefront-price-facet-mobile.jpg) | Multiple Brand selections alongside price and Show only filters. |
| [Meaningful Variant capacity](images/48-storefront-variant-capacity.jpg) | Editable capacity labels and specifications while canonical Variant IDs remain unchanged. |
| [Empty cart confirmation](images/49-storefront-empty-cart-confirmation.jpg) | Secondary action requires confirmation before removing all items. |
| [Market confirmation](images/50-storefront-market-confirmation.jpg) | Danish language/currency feedback after selecting Denmark. |
| [Demo without Relewise](images/51-storefront-without-relewise.jpg) | Content preview notice, disabled controls and configuration guidance. |
