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

## 3. Map built-in properties and custom Data

Use `Map(destinationProperty, sourcePropertyAlias)` for built-in Relewise properties
except identity. Use `MapData(propertyAlias, configure)` for custom `Data` entries:
only the Umbraco property alias is required, and the destination key defaults to it.
An optional callback configures one extensible `DataMappingOptions` type with
`DataKey` and optional field-specific converter selection:

```csharp
public void MapData(string propertyAlias, Action<DataMappingOptions>? configure = null);
```

`Map` selects the destination first. `MapData` selects the source first because its
destination override is optional. There are no separate title, summary, search-text,
or image helpers. Identity uses the dedicated `IdFromProperty` method below.

Replace the `AddRelewise` callback in example 2 with:

```csharp
.AddRelewise(options => options.MapContent("article", mapping =>
{
    mapping.Map(entity => entity.DisplayName, "heading"); // Built-in multilingual DisplayName from published cultures.
    mapping.MapData("summary"); // Umbraco "summary" becomes Data["summary"].
    mapping.MapData("bodyText", data =>
    {
        data.DataKey = "body"; // Override the destination key; keep body separate for search weighting.
    });
    mapping.MapData("heroImage", data =>
    {
        data.DataKey = "imageUrl"; // Override the destination key.
        data.UseConverter<MediaUrlConverter>(); // Select only this converter for this field.
    });
}))
```

Keep extraction/conversion separate from destination selection and retain existing
converter APIs. Built-in destinations require typed conversion; the extension
contract remains to be agreed. An expression is not a raw assignment. Unsupported
conversions must fail validation rather than silently lose data. Summary, body, and
image are customer-defined Data keys, not special search-adapter fields.
Searchability and weighting are configured in My Relewise.
Published URL metadata remains available independently of these selections.

### Converter defaults and custom registration

Without `UseConverter`, use the existing automatic pipeline: registered built-in
Relewise converters and globally registered custom converters whose `CanHandle`
accepts the property. This does not imply support for every Umbraco editor or an
automatic media-to-URL conversion. Retain existing culture handling.

Register a custom converter globally using today's API, before building the site:

```csharp
builder.Services.AddValueConverter<MyCustomConverter>(); // Implements IRelewisePropertyValueConverter; CanHandle selects properties.
```

All matching converters run today, not just the first. Registration order does not
provide an override: writing the same Data key twice can throw. Preserve that
behavior and the existing singleton lifetime; global converters must be safe to
share and must not constructor-inject scoped dependencies.

With `data.UseConverter<MediaUrlConverter>()`, run only the selected converter for
that field, bypassing automatic selection. The converter does not enter the global
pipeline or affect other mappings. `UseConverter<T>()` arranges concrete-type DI
registration, so no separate `AddValueConverter<T>()` call is needed. Resolve it
within each export operation's scope; field-only instances may use scoped
dependencies. Honor explicit DI registrations and never capture a scoped converter
in singleton configuration. Fail clearly if the selected converter cannot handle
the input; do not silently fall back to another converter.

`MyCustomConverter` and `MediaUrlConverter` are illustrative application classes,
not new built-in types. Prefer reusing `IRelewisePropertyValueConverter` and its
culture-aware context rather than adding another public interface. Before settling
the adapter contract, prove field isolation and `DataKey` remapping, including
existing converters that emit multiple keys. Preserve multilingual values and
report conversion failures with document, property, and culture context. Typed
conversion for built-in entity properties remains a separate design decision.

`AutoMap()` retains today's behavior. Do not silently combine it, declarative
field mappings, or `UseMapper` when they would write the same destination. Existing
custom mappers remain the escape hatch for complex extraction; their replacement
contracts and conflict validation must be settled before implementation.

## 4. Map Products, child Variants, Categories, and Brands

Use the same bootstrap and SDK connection. Entity methods name the Relewise target;
the first argument identifies the source Umbraco document type.

```csharp
.AddRelewise(options =>
{
    options.MapProductCategory("productCategory", mapping =>
    {
        mapping.IdFromProperty("categoryId"); // Stable ID reused in Product category paths.
        mapping.Map(entity => entity.DisplayName, "heading"); // Multilingual category name.
    });

    options.MapBrand("brand", mapping =>
    {
        mapping.IdFromProperty("brandId"); // Stable ID referenced by Products.
        mapping.Map(entity => entity.DisplayName, "name"); // SDK Brand name is a string, not Multilingual.
        mapping.MapData("logo", data =>
        {
            data.DataKey = "logoUrl"; // Custom Data; requires media-to-URL conversion.
        });
    });

    options.MapProduct("product", mapping =>
    {
        mapping.IdFromProperty("sku"); // Canonical Product ID for export, search, and tracking.
        mapping.Map(entity => entity.DisplayName, "heading"); // Built-in Multilingual value from published cultures.
        mapping.Map(entity => entity.ListPrice, "listPrices"); // Built-in MultiCurrency; source must include currency codes.
        mapping.Map(entity => entity.SalesPrice, "salePrices"); // Built-in MultiCurrency; same conversion requirement.
        mapping.Map(entity => entity.Assortments, "markets"); // Built-in list of integer assortment IDs.
        mapping.MapData("description"); // Customer-defined Data, not a built-in description property.
        mapping.BrandFrom("brand"); // Single picker to a mapped Brand; resolves its canonical ID.
        mapping.AdditionalCategoriesFrom("extraCategories"); // Multi-picker adds paths alongside mapped category ancestors.

        mapping.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku"); // Variant ID within this parent Product.
            variant.MapData("size"); // Same source alias and destination Data key.
        });
    });

    options.MapContentCategory("topic", mapping =>
    {
        mapping.IdFromProperty("topicId"); // Editorial taxonomy; separate from Product Categories.
        mapping.Map(entity => entity.DisplayName, "heading"); // Multilingual category name.
    });
})
```

The price properties above require currency-labelled source values and a converter
that produces `MultiCurrency`; an ordinary decimal has no implied currency.
`Assortments` requires integer IDs. Brand's string DisplayName needs an explicit
culture selection when its source varies by culture; that conversion policy remains
to be agreed. Product and Category DisplayName preserve published languages.

### Identity defaults and overrides

All mappings (Content, Product, Variant, Brand, and both Category types) default to
the Umbraco node's numeric ID. Preserve the existing root option to use GUID keys:

```csharp
options.UseContentKeysAsIds = true; // Existing option; selects Umbraco keys for mappings without an ID override.
```

`IdFromProperty("sku")` overrides that default for one mapping. Use it instead of
`Map(entity => entity.Id, ...)`. One internal identity resolver applies the same
strategy for entity export, references, search, tracking, and lifecycle actions.

**Every supported entity reference uses the referenced entity's mapping**, for
both single references and every item in a list. The referencing entity's ID
strategy does not determine the target ID. For example, `BrandFrom("brand")` reads
the picked Brand document and applies its `MapBrand` configuration: with
`IdFromProperty("brandId")`, both the exported Brand and the Product's Brand
reference use that document's `brandId` value, not its Umbraco node ID or key.

This also applies to all Product and Content Category path entries, additional
picked categories, and Product/Variant relationships. Resolve each target through
its own mapping; a Variant retains both its resolved parent Product ID and its own
resolved Variant ID. Without an override, use the target mapping's node ID/key
default. Missing target mappings, incompatible entity types, or empty/invalid
overridden IDs produce a clear mapping error, never a silent node-ID fallback.

This is automatic behavior of the existing proposed relationship methods. No extra
ID selector, resolver registration, or per-reference configuration is required.

Custom ID calculation must remain possible through custom mapping; settle its
precedence with `IdFromProperty` before introducing another public method. Changing
an ID mapping after ingestion is an identity migration, not an ordinary field update.

Relationship methods remain explicit because they resolve documents and hierarchy,
rather than merely copy property values:

- One primary entity target per document type; no dual Content/Product mapping.
- Variants are direct child documents of their Product. No arbitrary parent picker,
  cross-tree links, or block-based variants initially.
- Product category paths come from mapped category ancestors, skipping folders.
  Additional picked categories contribute complete paths without duplicates.
- Brand references use the mapped Brand ID. Variants inherit their Product's
  category/brand relationships. Umbraco personalization segments are not variants.
- `TrackEntityViews()` derives the event and canonical ID from the target mapping;
  a Variant view includes its Product and Variant IDs. Listing variants does not
  emit a view for each one. No separately configurable view-event type.

## 5. Opt into Umbraco's search provider

Add this inside the same configuration callback when Relewise should serve
Umbraco's published-content search abstraction:

```csharp
options.Search.UseRelewiseForPublishedContent = true; // Replace the native published-content search provider with Relewise.
```

This does not create a search page. Exporting data or querying Relewise directly
does not require it. Draft, media, member, and Library Element indexes are outside
this example.

Use the Dataset's default Relewise index; expose no `IndexId`. Export all published
cultures and derive each request's language from its effective Umbraco culture,
falling back to the site's default when none exists. Do not expose a site-wide
search `Language`, force English, or change core culture-filter semantics.
Relewise language keys must match exported values and requests.

## 6. Supplement an externally managed Product

Content mappings can coexist with editorial contributions to commerce Products:

```csharp
.AddRelewise(options =>
{
    options.MapContent("guide", mapping => mapping.AutoMap()); // Existing Content export capabilities remain available.
    options.MapProduct("productEditorialPage", mapping =>
    {
        mapping.IdFromProperty("commerceProductId"); // Address the existing commerce Product.
        mapping.Ownership = EntityOwnership.Supplement; // Contribute fields without owning Product lifecycle.
        mapping.MapData("summary", data =>
        {
            data.DataKey = "editorialSummary"; // Update only this declared editorial contribution.
        });
    });
})
```

New entity mappings otherwise own their entity. Supplementation must update only
approved fields and withdraw only its contributions on unpublish/removal; it must
never disable/delete the external Product or replace externally managed variants.
This matters to mapping because it determines what a declared field may update
and remove. Do not introduce a mandatory public `Source` setting for this purpose.

Safe selective removal and missing-target behavior are implementation gates.
An existence read followed by an upsert does not guarantee update-if-existing;
we must resolve this before promising that partial Products cannot be created.

## 7. Decisions before implementation

1. Agree these names and conversion/conflict rules. Keep existing APIs working;
   introduce obsolescence only with migration guidance. Follow existing code style
   and XML documentation, and keep public builders/contracts to the minimum needed.
2. Prove shared identity across export, search, relationships, and lifecycle actions.
   Search and automatic views use the same request-scoped `IRelewiseUserLocator`.
   Preserve Content IDs and culture/segment semantics; handle Variant moves and
   affected Product/category/brand aggregates, including indexing-only sites.
3. Resolve Product/Variant result collapse, mixed Content/Product ranking and
   paging, unmapped document types, protected/headless access, and supplementation
   guarantees. Keep dispatch, transport, and contribution storage internal.

Deliver in that order: compatible Content configuration, shared search semantics,
owned catalogue mapping, then supplementation. This document proposes configuration;
it does not authorize implementation.

## References

- [Existing setup and tracking](README.md#using-relewiseintegrationsumbraco)
- [Existing custom mapper and converter contracts](docs/custom-content-mapping.md)
- [AutoMapper: type and destination-member mapping](https://docs.automapper.io/en/latest/Projection.html)
- [Mapster: destination-first Map configuration](https://github.com/MapsterMapper/Mapster/wiki/Configuration)
- [Umbraco core: mapping definitions](https://docs.umbraco.com/umbraco-cms/13.latest/reference/mapping)
- [Community UmbracoMapper: property mapping and conversion](https://github.com/AndyButland/UmbracoMapper)
- [Umbraco UI Builder: field value mappers](https://docs.umbraco.com/umbraco-ui-builder/advanced/value-mappers)
- [Relewise default index and optional additional indexes](https://docs.relewise.com/docs/myrelewise/search-index.html)
- [Relewise multilingual data and request language](https://docs.relewise.com/docs/intro/languages-currencies.html)

Framework APIs informed the naming; no mapping-library dependency is proposed.
Built-in Product properties were checked against the Relewise Developer MCP and
SDK. Only example 1 uses entirely existing APIs; all other examples are proposals.
