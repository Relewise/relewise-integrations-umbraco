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
registrations remain usable. See the existing [custom Content mapping](custom-content-mapping.md)
and [block editor](block-editors.md) guides for those contracts.

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

`PriceConverter` and `MediaUrlConverter` represent application code. A decimal alone
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
Brand behavior. Product category ancestors skip unmapped folders; Variants must be
direct children of their Product.

## Conversion and custom mapping

`Map` infers `PropertyMappingOptions<TValue>` from its destination expression.
`UseConverter<T>()` requires `IPropertyMappingConverter<TValue>`, so a price converter
must asynchronously return `MultiCurrency` and a DisplayName converter must asynchronously return `Multilingual`.
Built-in conversion accepts compatible published values; it does not guess currency
or a culture for a localized source mapped to Brand's single-string DisplayName.
Without an explicit DisplayName mapping, a Brand uses the node name in Umbraco's default language when published, otherwise the first published culture in ordinal order.

`MapData` uses existing built-in and global converters by default. Selecting a
field-specific `IAsyncRelewisePropertyValueConverter` bypasses that automatic selection
for the field. A custom Data key requires the converter to emit under the source
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
registration order; the last matching custom registration wins. Existing synchronous
global converters remain singletons through the compatibility adapter.
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
for removal in the next major version. Legacy Content mappers retain their returned
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

Search providers are configured separately; see [Umbraco core search](umbraco-19-search.md).

## Publication and rebuilds

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
