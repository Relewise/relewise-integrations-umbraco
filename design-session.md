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
        .MapContent("homePage", mapping => mapping.AutoMap()) // Explicit Content target; same automatic conversion as today.
        .MapContent("contentPage", mapping => mapping.AutoMap()))
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
`MapContent("page", mapping => mapping.Export = false)`. For export without automatic
views, omit the view middleware or set `mapping.TrackViews = false` on that mapping.
These switches do not allow choosing an inconsistent event type.

## 3. Map built-in properties and custom Data

Use `Map(destinationProperty, sourcePropertyAlias, configure)` for built-in Relewise
value properties, such as `DisplayName`, `ListPrice`, and `Assortments`. Identity and entity
relationships use dedicated mapping methods. The optional callback receives
`PropertyMappingOptions<TValue>`, inferred from the destination property type.

Use `MapData(sourcePropertyAlias, configure)` for custom `Data` entries. Only the
source Umbraco property alias is required; the destination Data key defaults to that
alias. An optional callback configures one extensible `DataMappingOptions` type with
`DataKey` and optional field-specific converter selection:

```csharp
public void MapData(string sourcePropertyAlias, Action<DataMappingOptions>? configure = null);
```

`Map` selects the destination first. `MapData` selects the source first because its
destination override is optional. There are no separate title, summary, search-text,
or image helpers. Identity uses the dedicated `IdFromProperty` method below.

### Consistent API terminology

| Proposed method(s) | Parameter names, in order |
| --- | --- |
| `MapContent`, `MapProduct`, `MapBrand`, `MapProductCategory`, `MapContentCategory` | `documentTypeAlias`, `configure` |
| `VariantsFromChildren` | `documentTypeAlias`, `configure` |
| `Map` | `destinationProperty`, `sourcePropertyAlias`, `configure` (optional) |
| `MapData` | `sourcePropertyAlias`, `configure` (optional) |
| `IdFromProperty`, `BrandFrom`, `AdditionalCategoriesFrom` | `sourcePropertyAlias` |

`documentTypeAlias` identifies a source Umbraco Document Type; `sourcePropertyAlias`
identifies a property on a source document. `destinationProperty` is an expression
selecting a built-in Relewise property; `DataKey` names a destination entry in the
Relewise `Data` dictionary. Use `configure` for configuration callback parameters;
example callback variables use `options` at the root, `mapping` for entities,
`variant` for child Variants, `property` for built-in property mapping options, and
`data` for Data mapping options.

Apply these names in signatures, XML documentation, validation messages, and guides
when implementing the proposed API. Preserve existing public parameter names and
contracts, including `AddContentType` and `UseContentKeysAsIds`, for compatibility
with named arguments and existing configuration. This table describes proposed
method shapes, not additional overloads or changes to existing APIs.

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

Summary, body, and image are customer-defined Data keys, not special search-adapter
fields. Searchability and weighting are configured in My Relewise. Published URL
metadata remains available independently of these selections.

### Strongly typed built-in property converters

Proposed signature on the Product mapping builder (other entity builders select
their own destination entity type):

```csharp
public void Map<TValue>(
    Expression<Func<Product, TValue>> destinationProperty,
    string sourcePropertyAlias,
    Action<PropertyMappingOptions<TValue>>? configure = null);
```

`TValue` flows from the destination expression into the options and converter.
The proposed converter contract is deliberately narrow:

```csharp
public interface IPropertyMappingConverter<TValue>
{
    TValue Convert(PropertyMappingContext context);
}
```

On `PropertyMappingOptions<TValue>`, the proposed method signature is:

```csharp
public void UseConverter<TConverter>()
    where TConverter : class, IPropertyMappingConverter<TValue>;
```

A `ListPrice` converter must produce the destination's `MultiCurrency` type; a
`DisplayName` converter must produce `Multilingual`. A string-producing converter
cannot be selected for either. Preserve SDK nullability in these contracts; do not
introduce public object-returning converters or unchecked destination casts.
`PropertyMappingContext` supplies the source document, element, property, and
operation context described below. It is distinct from the existing Data-writing
`RelewisePropertyConverterContext`.

Accept only direct, supported destination properties; reject computed expressions,
identity, and relationships. Without an explicit converter, select only compatible
built-in typed conversions. Destination compatibility is checked at compile time;
the source editor/value still needs runtime validation because it is selected by
alias. Define null/missing-value handling and report unsupported input or conversion
failures with document, source property alias, and culture context.

### Context for mappers and converters

Mapping/conversion methods receive contextual source information, not just a scalar
value. Keep the following concepts consistent across mapper and converter contexts:

| Context | Purpose |
| --- | --- |
| Owning document (`PublishedContent`, `IPublishedContent`) | Read sibling properties, ancestors, and site context. |
| Current element (`SourceElement`, `IPublishedElement`) | Read surrounding fields on the current document or nested block. |
| Source property (`Property`, `IPublishedProperty`; converters only) | Access its editor, alias, and culture-dependent values. |
| Cultures | Published cultures for complete multilingual output; the current culture for existing per-culture Data conversion. |
| Destination | The mapper's typed target update, or the converter's selected built-in property/Data key. |
| Operation | Cancellation and explicitly supplied operation-specific context. |

At document level, `SourceElement` is the owning document. Nested conversion changes
`SourceElement` and `Property` while retaining `PublishedContent` and the operation
context. For example, a price converter can read currency from its enclosing block
and market information from the owning page's ancestors. Preserve this information
through recursive block conversion. Typed converters return the complete `TValue`;
existing Data converters retain their per-culture invocation and writing behavior.

Keep contextual metadata read-only and destination writes limited to the mapping's
responsibility. Share context plumbing internally; a public base-class hierarchy
or untyped global service/state dictionary is not required.

Constructor-inject services and application configuration into mapper/converter
instances. Resolve new DI-managed instances in the export operation's scope; do not
capture scoped services in singleton builders or converters. Existing globally
registered converters remain singletons, and existing supplied mapper instances
retain their lifetime requirements. Preserve existing context service access for
compatibility; richer execution must use the operation's service scope.

Background indexing, explicit exports, and rebuilds establish the required Umbraco
context and resolve site/culture context explicitly, without requiring an HTTP
request. Before finalizing the new typed converter interface, settle asynchronous
conversion and cancellation for service lookups; preserve synchronous legacy APIs.

Preserve `IContentTypeMapping`, `ContentMappingContext`, and
`IRelewisePropertyValueConverter` contracts. Extend context additively and provide
a context-aware aggregate conversion overload. Existing property/culture-only calls
remain valid for existing converters, but cannot supply an owning document that
was never passed. Context-dependent converters must require that information and
report its absence clearly. The shared export engine supplies full source context;
adapters must preserve it through legacy mappers and nested conversions.

### Converter defaults and custom registration

For `MapData` without `UseConverter`, use the existing automatic pipeline:
registered built-in Relewise converters and globally registered custom converters whose `CanHandle`
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

For both `data.UseConverter<TConverter>()` and `property.UseConverter<TConverter>()`,
run only the selected converter for that mapping, bypassing automatic selection.
The converter does not enter the global pipeline or affect other mappings. `UseConverter<TConverter>()` arranges concrete-type
DI registration, so no separate global registration call is needed. Resolve it
within each export operation's scope; field-only instances may use scoped
dependencies. Honor explicit DI registrations and never capture a scoped converter
in singleton configuration. Fail clearly if the selected converter cannot handle
the input; do not silently fall back to another converter.

`MyCustomConverter` and `MediaUrlConverter` are illustrative application classes,
not new built-in types. For `MapData`, prefer reusing
`IRelewisePropertyValueConverter` and its culture-aware context rather than adding
another Data converter interface. Before settling
the adapter contract, prove field isolation and `DataKey` remapping, including
existing converters that emit multiple keys. Preserve multilingual values and
report conversion failures with document, source property alias, and culture context.
Legacy Data-writing converters do not automatically qualify as typed converters
for `Map`; share DI/error-handling behavior internally, not a forced public contract.

`AutoMap()` retains today's behavior. Do not silently combine it, declarative
field mappings, or `UseMapper` when they would write the same destination. Existing
custom mappers remain the escape hatch for complex extraction; their replacement
contracts and conflict validation must be settled before implementation.

## 4. Map Products, child Variants, Categories, and Brands

Use the same bootstrap and SDK connection. Entity methods name the Relewise target;
`documentTypeAlias` identifies the source Umbraco Document Type.

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
        mapping.Map(entity => entity.ListPrice, "listPrices", property =>
        {
            property.UseConverter<PriceConverter>(); // Produces MultiCurrency from currency-labelled source values.
        });
        mapping.Map(entity => entity.SalesPrice, "salePrices", property =>
        {
            property.UseConverter<PriceConverter>(); // Same typed converter; independently mapped source property.
        });
        mapping.Map(entity => entity.Assortments, "markets"); // Built-in list of integer assortment IDs.
        mapping.MapData("description"); // Customer-defined Data, not a built-in description property.
        mapping.BrandFrom("brand"); // Single picker to a mapped Brand; resolves its canonical ID.
        mapping.AdditionalCategoriesFrom("extraCategories"); // Multi-picker adds paths alongside mapped category ancestors.

        mapping.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku"); // Variant ID within this parent Product.
            variant.MapData("size"); // Same source property alias and destination Data key.
        });
    });

    options.MapContentCategory("topic", mapping =>
    {
        mapping.IdFromProperty("topicId"); // Editorial taxonomy; separate from Product Categories.
        mapping.Map(entity => entity.DisplayName, "heading"); // Multilingual category name.
    });
})
```

`PriceConverter` is an illustrative application implementation of
`IPropertyMappingConverter<MultiCurrency>` (with the SDK's nullable annotations).
The source must supply currency codes; an ordinary decimal has no implied currency.
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

## 5. One Relewise indexing pipeline; independent search configuration

One Relewise indexing pipeline uses Umbraco 19's content-change and rebuild flow
to maintain mapped entities. It replaces the integration's existing notification
handlers as independent exporters. Existing mapping configuration remains supported;
customers do not select between indexing mechanisms.

All writes use a shared export engine for mapping, identity resolution, converters,
relationships, and administrative actions. The dashboard's full export and existing
public export/rebuild entry points use that same engine and coordinate with ongoing
updates. They do not create a second writer. Dependency changes, such as a Brand
change affecting Products, also enter this pipeline. Repeated processing and
concurrent updates must preserve one entity identity and the latest intended state,
including when multiple application instances are running.

Preserve Examine's existing indexing registration and add a separate internal
Umbraco registration for the Relewise pipeline. Each backend continues receiving
relevant changes. **There is one fixed Relewise index:** the internal Umbraco
registration alias identifies a pipeline, not another Relewise index. Export and
search use that same single Relewise index; infrastructure aliases remain internal.

RC1 requires both an indexer type and a searcher type in a content-index registration.
The internal registration satisfies that requirement without changing the searcher
selected for ordinary published-content queries. Indexing itself does not invoke
the searcher. The complete registration arrangement still requires an RC1 integration
test.

Configure search through Umbraco's published-content search abstraction separately:

```csharp
options.Search.UseRelewiseForPublishedContent = true; // Execute searches through Umbraco's published-content abstraction in Relewise.
options.Search.Target = SearchTarget.Products; // Search nodes mapped as Relewise Products.
```

| Setting | Default | Behavior |
| --- | --- | --- |
| `UseRelewiseForPublishedContent` | `false` | Leave the existing search provider selected; Relewise indexing continues. |
| `Target` | `SearchTarget.Content` | When enabled, select `ContentSearchRequest` for nodes mapped as Relewise Content. `SearchTarget.Products` selects `ProductSearchRequest` for nodes mapped as Relewise Products. |

Enabling search changes query routing only: it neither registers another Relewise
indexing pipeline nor stops the existing provider's indexing. `Target` applies only
when Relewise is selected for these queries. Both targets return Umbraco document
keys through the shared identity association and must honor filtering, access,
culture, sorting, and pagination semantics. A mixed Content/Product target requires
an agreed ranking and pagination contract before it can be exposed. Future search
settings belong on the mutable `Search` options object.

A separate frontend can query Relewise directly regardless of the selected Umbraco
search provider. Tracking is independently configured. Search-page implementation
and draft, media, member, and Library Element pipelines are outside this example.

Export all published cultures and derive each search request's language from its
effective Umbraco culture, falling back to the site's default when none exists.
Relewise language keys must match exported values and requests. Preserve core
culture-filter semantics; expose no site-wide search `Language` or Relewise `IndexId`.

## 6. Contribute Product data without administrative actions

Content mappings can coexist with editorial contributions to commerce Products:

```csharp
.AddRelewise(options =>
{
    options.MapContent("guide", mapping => mapping.AutoMap()); // Existing Content export capabilities remain available.
    options.MapProduct("productEditorialPage", mapping =>
    {
        mapping.IdFromProperty("commerceProductId"); // Use the same Product ID as the commerce integration.
        mapping.AdministrativeActions = AdministrativeActionPolicy.None(); // Export data; leave enable, disable, and delete to commerce.
        mapping.MapData("summary", data =>
        {
            data.DataKey = "editorialSummary"; // Update only this declared editorial contribution.
        });
    });
})
```

Use one immutable `AdministrativeActionPolicy` type with named factory methods,
internal construction, and no public implementation interface:

| Factory | Behavior |
| --- | --- |
| `AdministrativeActionPolicy.Synchronize()` | Default: follow the integration's documented Umbraco publication/removal rules. Preserve existing Content behavior; unpublish does not automatically mean permanent deletion. |
| `AdministrativeActionPolicy.None()` | Send no enable, disable, or delete actions from this mapping, including full-export cleanup and actions for its exported Variants. |

This follows Relewise's separation of data updates (`ProductUpdate`) and lifecycle
actions (`ProductAdministrativeAction`). Referenced Brands and Categories retain
their own mappings' policies; disabling actions on a Product mapping does not change
those independently configured mappings.

With `None()`, publish/update exports only declared fields. Unpublish/removal stops
further exports but leaves contributed fields in place. Automatic field withdrawal
is deferred. Do not send implicit defaults, replace unrelated data or externally
managed Variants, or use `ClearAndReplace`: that update mode can also re-enable a
disabled Product. Verify partial-update and collection semantics before implementation.

This policy does not guarantee that a Product already exists. Standard Product
update modes can create missing Products; both integrations must use the same ID,
and import order may otherwise produce a partially populated Product. An existence
read followed by an upsert does not provide atomic update-if-existing behavior.
Document this limit rather than introducing an unsupported promise.

Additional named factories or overloads can express future policies with parameters.
Retain the original factory signatures and their semantics; expose no raw SDK
administrative-action enum or configurable cleanup machinery at this stage.

### Extensibility review of the remaining configuration

Keep the public surface small and preserve the first two bootstrap examples:

| Configuration | Recommendation |
| --- | --- |
| `MapData` | Keep its optional `DataMappingOptions` callback; add settings there as concrete needs arise. |
| `Map` | Keep the optional `PropertyMappingOptions<TValue>` callback for typed converter selection and future property settings; short calls remain valid. |
| Relationship methods | Add configuration overloads when picker selection or relationship behavior needs them; do not expose empty options types now. |
| Identity | Keep `IdFromProperty` and the existing `UseContentKeysAsIds` default. Add a dedicated overload if custom calculation needs configuration; every reference must still use the shared resolver. |
| Converters and mappers | Preserve existing Data converter/mapper contracts and registrations; add only the narrow typed converter contract needed by `Map`. Prove adapters and scope behavior. |
| Search | Keep the mutable options object with `UseRelewiseForPublishedContent` and `Target`; add independent settings as needed. Search opt-in never controls indexing. |
| Export and view tracking | Keep genuine on/off switches; add separate settings or overloads if requirements expand, without repurposing existing booleans. |

Preserve existing method signatures when adding overloads; adding an optional
parameter to an existing signature alone does not preserve binary compatibility.
Keep policy types immutable and options callbacks concerned with configuration;
dispatch, resolution, transport, and lifecycle machinery remain internal.

## 7. Decisions before implementation

1. Agree these names and conversion/conflict rules. Keep existing APIs working;
   introduce obsolescence only with migration guidance. Follow existing code style
   and XML documentation, and keep public builders/contracts to the minimum needed.
2. Prove shared identity across export, search, relationships, and lifecycle actions.
   Search and automatic views use the same request-scoped `IRelewiseUserLocator`.
   Preserve Content IDs and culture/segment semantics; handle Variant moves and
   affected Product/category/brand aggregates, including indexing-only sites.
3. Resolve Product/Variant result collapse, mixed Content/Product ranking and
   paging, unmapped document types, protected/headless access, and partial-update
   guarantees. Prove administrative-action suppression across cleanup and Variants;
   confirm which new entity mappings expose the policy. Keep dispatch and transport
   internal.

Before accepting the mapping/indexing/search arrangement, verify locally on RC1 that:

- Nested converters retain the owning document and operation context while selecting
  the correct block, property, and culture. Legacy mapper/converter calls keep working.
- Scoped dependencies are resolved and disposed per export operation, and contextual
  mapping works during background indexing and rebuilds without an HTTP request.
- Indexing-only installations process publish, update, unpublish/removal, and rebuild
  events through Umbraco's flow while Examine remains current and searchable.
- Enabling Relewise for Umbraco's search abstraction changes query routing only,
  preserving one Relewise indexing pipeline and the existing provider's indexing.
  The selected target returns correct totals, paging, access, and culture behavior.
- The old notification-driven exporters are not independently registered. Dashboard
  exports, public export/rebuild calls, and core indexing use the shared export engine.
- Overlapping exports, rebuilds, repeated events, and multiple application instances
  preserve the latest published/removal state without duplicate entities.
- Brand and Category changes and Variant changes/moves update affected entities;
  the integration handles dependencies not covered by Umbraco's change strategy.
- Rebuild/removal operations respect administrative-action policies and affected
  descendants/Variants. A failed or cancelled rebuild cannot trigger destructive
  cleanup; a rebuild never clears unrelated entities from the single Relewise index.

Deliver in that order: compatible Content configuration, shared search semantics,
catalogue mapping, then data contributions with administrative actions suppressed.
This document proposes configuration; it does not authorize implementation.

## References

- [Existing setup and tracking](README.md#using-relewiseintegrationsumbraco)
- [Existing custom mapper and converter contracts](docs/custom-content-mapping.md)
- [AutoMapper: type and destination-member mapping](https://docs.automapper.io/en/latest/Projection.html)
- [Mapster: destination-first Map configuration](https://github.com/MapsterMapper/Mapster/wiki/Configuration)
- [Umbraco core: mapping definitions](https://docs.umbraco.com/umbraco-cms/13.latest/reference/mapping)
- [Community UmbracoMapper: property mapping and conversion](https://github.com/AndyButland/UmbracoMapper)
- [Umbraco UI Builder: field value mappers](https://docs.umbraco.com/umbraco-ui-builder/advanced/value-mappers)
- [Umbraco 19 RC1 indexing registrations and content-change strategies](https://github.com/umbraco/Umbraco-CMS/blob/release-19.0.0-rc1/src/Umbraco.Core/Search/Configuration/IndexOptions.cs)
- [Relewise entity updates and administrative actions](https://docs.relewise.com/docs/developer/implementation-steps.html)
- [Relewise Product update modes and creation behavior](https://docs.relewise.com/docs/developer/sdks/dotnet/product-update/entity-integration.html)
- [Relewise Search Index](https://docs.relewise.com/docs/myrelewise/search-index.html)
- [Relewise multilingual data and request language](https://docs.relewise.com/docs/intro/languages-currencies.html)

Framework APIs informed the naming; no mapping-library dependency is proposed.
Built-in Product properties were checked against the Relewise Developer MCP and
SDK. Only example 1 uses entirely existing APIs; all other examples are proposals.
