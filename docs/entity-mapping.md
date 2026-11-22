# Entity mapping

The existing Content API remains supported:

```csharp
.AddRelewise(options => options.AddContentType("article", mapping => mapping.AutoMap()))
```

Its equivalent in the new API is:

```csharp
.AddRelewise(options => options.MapContent("article", mapping => mapping.AutoMap()))
```

Existing `IContentTypeMapping` instances and global `AddValueConverter<T>()`
registrations remain usable through obsolete compatibility contracts. Use
[custom Content mapping](custom-content-mapping.md) for the legacy API, and
[block editors](block-editors.md) for extraction behavior and current examples.

## Fields and IDs

```csharp
.AddRelewise(options =>
{
    options.MapBrand("brand", mapping => mapping.IdFromProperty("brandId"));
    options.MapProductCategory("productCategory", mapping => mapping.IdFromProperty("categoryId"));
    options.MapProduct("product", mapping =>
    {
        mapping.IdFromProperty("sku"); // Shared by export, relationships, search resolution and tracking.
        mapping.Map(entity => entity.DisplayName, "heading"); // Multilingual built-in property.
        mapping.Map(entity => entity.ListPrice, "listPrices", property => property.UseConverter<PriceConverter>());
        mapping.Map(entity => entity.SalesPrice, "salePrices", property => property.UseConverter<PriceConverter>());
        mapping.Map(entity => entity.Assortments, "markets"); // Source must produce integer assortment IDs.
        mapping.MapData("description"); // Data key defaults to the source property alias.
        mapping.MapData("heroImage", data =>
        {
            data.DataKey = "imageUrl";
            data.UseConverter<MediaUrlConverter>(); // Application converter producing a URL.
        });
        mapping.BrandFrom("brand"); // Single document picker; uses the Brand mapping's ID strategy.
        mapping.AdditionalCategoriesFrom("extraCategories"); // Adds complete mapped category paths.
        mapping.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku"); // Unique within its parent Product.
            variant.MapData("size");
        });
    });
})
```

`MediaUrlConverter` is an illustrative application type. The demo provides a working
[PriceConverter](../samples/Relewise.DemoStorefront/Integration/Converters/PriceConverter.cs) for a Block List
containing `currency` and `amount` properties. A decimal alone
does not identify a currency; conversion must supply currency codes explicitly.

Without an override, IDs use the numeric node ID. The existing
`UseContentKeysAsIds = true` option selects GUIDs instead. ID properties must contain
an invariant, nonempty scalar ID. References always apply the referenced mapping's
strategy, including every item in a list. Invalid references fail rather than falling
back to another ID. Changing an ID configuration after ingestion is an identity migration.

Configure Umbraco's canonical domains and
`Umbraco:CMS:WebRouting:UmbracoApplicationUrl` for background exports. Without a
configured site URL, Umbraco may resolve absolute URLs against its fallback host.

Multiple `MapContent` registrations are allowed for different Document Types.
`MapProduct`, `MapBrand`, `MapProductCategory`, and `MapContentCategory` each allow one
registration. A Document Type has one target. Canonical IDs must identify distinct
entities; duplicate IDs are rejected within an export batch/full rebuild. Delta
exports do not maintain a cross-batch identity registry. Brand identity comparisons follow Relewise's case-insensitive
Brand behavior. Brand and additional-Category picker properties must be culture and segment
invariant. Product category ancestors skip unmapped folders; Variants must be
direct children of their Product.

## Conversion and custom mapping

`Map` infers `PropertyMappingOptions<TValue>` from its destination expression.
`UseConverter<T>()` requires `IPropertyMappingConverter<TValue>`, so a price converter
must asynchronously return the SDK property's `MultiCurrency?` type. A converter for
Content/Product/Category DisplayName returns `Multilingual`; Brand.DisplayName uses
`string`.
Built-in conversion accepts compatible published values; it does not guess currency.
Invariant content shares its multilingual values across all configured site languages.
Culture-varying content exports only its published cultures, preserving translation boundaries.
For a localized source mapped to a single-string destination such as Brand.DisplayName,
it selects Umbraco's default language when published, otherwise the first published
culture in case-insensitive ordinal order. A typed converter can override that selection.
With the default `Synchronize()` policy and no explicit DisplayName mapping, a
Brand uses its node name with the same language selection. `None()` omits these
default business fields so they can remain under external control.

`MapData` uses existing built-in and global converters by default. Selecting a
field-specific `IAsyncRelewisePropertyValueConverter` bypasses that automatic selection
for the field. Omitting an alias from the Umbraco schema fails an explicit `Map` or
`MapData` mapping; it is not treated as an empty value. A custom Data key requires the converter to emit under the source
property alias; converters that emit several custom keys cannot be arbitrarily renamed.

Contexts expose the owning `PublishedContent`, current `SourceElement`, source
`Property`, cultures, cancellation and operation-scoped `Services`. Nested block
conversion retains its owner and changes the current element. Old property-only
conversion calls remain valid but cannot reconstruct an owner.

Constructor injection is supported for field converters and new
`IEntityMapper<TEntity>` implementations selected through `UseMapper<T>()`. Registered
instances resolve from the operation scope; unregistered concrete types are activated
and disposed within that operation. `AddAsyncValueConverter<T>()` registers scoped
global Data converters, which take precedence over built-in defaults regardless of
registration order; the last matching custom registration wins. Separately registered
legacy custom converters still run and can collide if they write the same Data key.
Existing synchronous global converters remain singletons through the compatibility adapter.
Do not capture a scoped service in an existing singleton mapper or converter.
Do not request another export/rebuild from inside a mapper or converter; recursive
indexing is rejected to prevent deadlocks.
Typed field converters use `ValueTask<TValue> ConvertAsync(PropertyMappingContext, CancellationToken)`.
Data converters use `ValueTask ConvertAsync(RelewisePropertyConverterContext, CancellationToken)`. Data converters retain their existing
culture-dependent scalar, multilingual or culture-suffixed output shapes.

Choose automatic Data mapping or explicit fields. Either may be combined with one
`IEntityMapper<TEntity>`, which runs after fields, converters, relationships and child
Variants. It populates the supplied SDK entity and returns `Task`; the integration
constructs the corresponding update. New mappers must keep the resolved ID; reserved
metadata is applied afterward. `IContentTypeMapping` and synchronous Data converters
are obsolete, remain functional through compatibility adapters, and are scheduled
for removal in the next major release of `Relewise.Integrations.Umbraco`. Legacy Content mappers retain their returned
update/ID behavior; migrate identity into `IdFromProperty` to align references and tracking.

## Contributing to an existing catalogue

```csharp
options.MapProduct("productEditorialPage", mapping =>
{
    mapping.IdFromProperty("commerceProductId");
    mapping.AdministrativeActions = AdministrativeActionPolicy.None();
    mapping.MapData("editorialDescription");
});
```

The default `Synchronize()` policy follows publication, unpublication, and removal.
`None()` sends declared fields and required source metadata without enabling,
disabling, deleting, or replacing externally owned Variants. Unpublishing leaves
the business fields in place, while source metadata excludes its Umbraco page from
searches requiring a published page. Product providers can opt out with
`RequirePublishedPage = false`; callers then handle unavailable pages themselves.
It is a partial update, not an update-existing-only
operation: a missing Product can be created. `ClearAndReplace` is incompatible
with this policy because it can reset externally controlled state.

Previously contributed business fields are not automatically withdrawn. Partial
updates replace declared collections as complete values; they do not merge parts
of those collections with values supplied by another integration. An empty
`BrandFrom` picker currently omits Brand from the partial update, retaining an
existing Relewise Brand relationship.

Search providers are configured separately; see [Umbraco core search](search.md).

## Publication and rebuilds

In **Settings → Relewise Dashboard**, review the configured entity targets, ID sources,
automatic indexing and view tracking, and named search providers. Open **Details** beside
a mapping to inspect its fields, relationships, custom mapper type, and publication
and removal behavior. The overview does not execute custom mappers, converters or
request callbacks. Connection settings do not constitute a connectivity
or credentials check.

Use **Export all mapped entities** to export published data and reconcile missing
entities according to the configured lifecycle policies. **Maintenance → Export and
permanently delete missing entities** additionally permits permanent deletion for synchronized
mappings within this site's scope. Mappings using `None()` retain their entities.
Child Variants are updated with their parent Product. Completion messages describe
the current dashboard visit; no export history is persisted by the integration.

Published content supplies exported values. One writer handles background changes,
explicit exports and rebuilds without integration-owned database records. Relewise
source metadata identifies earlier exports after unpublication, including property-based
IDs. Umbraco deletion notifications capture the configured ID and queue removal after
commit through the same writer; deleted Variants refresh their parent. A draft-only
ID change can leave its previously exported identity until a full export. A successful full export reconciles missing entities within its
configured scope and administrative policy; it does not clear the Dataset.

Changed Brand/Category references are not automatically propagated to other entities.
Re-export the affected Product or rebuild. Changed canonical IDs can leave an earlier
entity until the next full export. Previously contributed business fields are retained
unless explicitly updated/cleared. Interrupted background work can require a rebuild;
coordinate full rebuilds across processes sharing the same source/Dataset scope.
