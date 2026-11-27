# Umbraco search

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Migration](migration.md)

Use Relewise through Umbraco's core search abstraction with Content and Product
providers. Select Umbraco Search indexing with `ConfigureSearch`; registering query
providers is optional and does not change entity mapping.

Install the Search package major matching both Umbraco and the integration package: 17.x,
18.x or 19.x. On Umbraco 17/18 it includes and registers the Umbraco Search add-on;
on Umbraco 19 it uses the built-in abstraction. The Relewise configuration is the same.

## Registration

Install `Relewise.Integrations.Umbraco.Search` alongside the integration package.
For example, on Umbraco 17:

```sh
dotnet add package Relewise.Integrations.Umbraco.Search --version 17.*
```

Use `18.*` for Umbraco 18. For a prerelease, specify the exact published package version.

`ConfigureSearch()` selects Umbraco Search indexing. Without that call, the
integration continues exporting through publication notifications.

Configure the SDK connection as shown in the [README](../README.md), then configure
mapping and query providers independently:

```csharp
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Search;

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options =>
    {
        options.MapContent("article", mapping => mapping.AutoMap());
        options.MapProduct("product", mapping => mapping.IdFromProperty("sku"));

        options.ConfigureSearch(search =>
        {
            search.AddContentProvider("articles", provider =>
            {
                provider.DocumentTypeAliases = ["article"]; // Optional Content-only selection.
                provider.DisplayedAtLocation = "Site search"; // Stable analytics/merchandising label.
            });
            search.AddProductProvider("catalogue", provider =>
            {
                provider.ConfigureRequest((request, context) =>
                {
                    request.Currency = new Currency("DKK"); // Site-specific request currency.
                });
            });
            search.DefaultPublishedContentProviderAlias = "articles";
        });
    })
    .Build();
```

Search options, request contexts and segment policies use the `Search` namespace.
Only Content and Product query providers are supported. Each provider requires an
exported mapping for its entity type. For Content, omit `DocumentTypeAliases` (leave it `null`) to include all
exported Content types, or provide a nonempty list of exported Content aliases.
An empty array, unknown alias or non-exported mapping is rejected.
Several named providers may query the same entity type with different request
settings. Product providers use the single Product
mapping; `DocumentTypeAliases` is available only on Content providers.

Product providers default to `RequirePublishedPage = true`. An advanced search can
opt out when its caller handles results without a published Umbraco page:

```csharp
options.ConfigureSearch(search =>
{
    search.AddProductProvider("commerceCatalogue", provider =>
    {
        provider.RequirePublishedPage = false; // Include mapped Products whose Umbraco page is unavailable.
    });
});
```

`rwUmbracoPublished` records whether the mapped source page is published, separately
from whether the Relewise Product is enabled. The integration maintains this
metadata regardless of whether a provider filters on it, including when
commerce contributes other Product fields. This option only omits that query
filter; mapping, access and culture restrictions remain. Content providers
always require a published page. Results still contain Umbraco document IDs;
callers must handle unavailable pages and any commerce destinations themselves.
`AdministrativeActions` is configured independently; this option does not enable
a disabled Product.

For application-level SDK filters and selected Data fields, use the supported metadata
constants in [RelewiseDataKeys](../src/Integrations.Umbraco/RelewiseDataKeys.cs). These
cover source identity, document type, publication, culture and protection;
export bookkeeping remains internal.

The integration does not partition a Dataset by Umbraco installation. Entity IDs and
Document Type mappings share the Dataset's scope; independent installations with overlapping
mappings must use separate Datasets if they require isolation.

Integration metadata supports the following behavior:

| Data key | Purpose |
| --- | --- |
| `rwUmbracoNodeKey` | Resolve overridden entity IDs back to Umbraco documents and identify earlier exports. |
| `rwUmbracoPublished` | Source-page availability, independently of entity administrative state. |
| `UmbracoVersionId` | Existing numeric export version; reconcile missing exports after a full export completes successfully. |
| `rwUmbracoSearchCulture` | Published cultures available in the default segment. |
| `rwUmbracoSearchProtected`, `rwUmbracoSearchAccess` | Apply Umbraco access restrictions before paging and facets. |

Querying uses the existing built-in SDK properties and mapped Data values. No additional
field projections are exported. Invariant values such as stock remain one numeric Data
value, regardless of the number of site languages. Legacy AutoMap output is unchanged.

Full exports stamp one numeric `UmbracoVersionId` on their entities and reconcile matching
source entities with a different version only after successful ingestion. The comparison
is inequality, not chronological ordering. Automatically generated versions increase within
the running application even when the clock does not advance; explicit delta-export versions
remain caller-supplied. Coordinate writers across processes sharing the same mapping scope.

`AddRelewise` exports publication changes through Umbraco notifications by default.
Calling `options.ConfigureSearch()` uses Umbraco Search's indexing flow instead,
even when no named query providers are configured. These two mechanisms do not
export the same change twice. Explicit exports and dashboard rebuild actions use
the configured indexing flow too. Call `ConfigureSearch` only once across all
registration callbacks; repeated calls are rejected.
Named query providers do not add indexing pipelines or create additional Relewise
indexes. Every provider uses the Dataset's default Relewise index.

Without `DefaultPublishedContentProviderAlias`, queries through Umbraco's default
published-content provider keep their existing search implementation. Setting it
replaces query resolution for that alias; Examine's indexer and change strategy remain registered. Draft content, media,
members, and elements retain their existing search providers.

## Product prices

Bind a decimal query field to the Product's built-in `SalesPrice`:

```csharp
options.ConfigureSearch(search =>
{
    search.AddProductProvider("catalogue", provider =>
    {
        provider.Map(product => product.SalesPrice, "price"); // Query field for native Product prices, not a Data key.
        provider.ConfigureRequest((request, context) =>
        {
            request.Currency = new Currency("EUR"); // Use the active site's or shopper's currency.
        });
    });
});
```

Use `DecimalRangeFilter("price", ...)`, `DecimalRangeFacet("price", ...)` and
`DecimalSorter("price", ...)` through Umbraco. The provider translates them to
native SalesPrice filters, price-range facets and price sorting. Results and all
supported facet counts come back in one search request. No duplicate price Data
is exported, and prices are not converted between currencies.

A request currency is required whenever the bound field is queried. Use one SalesPrice query alias within a request. Its buckets must be nonempty and
nonoverlapping, with inclusive lower and exclusive upper bounds. A single
positive price filter becomes the native facet selection, including custom bounds
that differ from the displayed buckets. Multiple or negated price filters remain
filters and constrain all facet counts. Other filter, facet and sorter types on
this field are rejected before a search is sent.

## Direct field bindings

The provider derives query fields from declarative export mappings. A source alias mapped
to `DisplayName` queries that built-in property; an explicit `MapData` mapping queries its
stored `DataKey`. Invariant integers/decimals, scalar strings, Boolean values and tags are
inferred from the Umbraco schema. These inferred descriptions are cached in memory per
provider; restart the site after changing a queried schema. No Dataset schema requests or
persisted integration-owned schema are needed.

Use explicit provider bindings for values produced by custom mappers/converters, legacy
mappers, or a different query-field name:

```csharp
options.ConfigureSearch(search =>
{
    search.AddProductProvider("catalogue", provider =>
    {
        provider.Map(product => product.DisplayName, "name"); // Native name sorting.
        provider.Map(product => product.CategoryPaths, "category"); // Canonical Category IDs anywhere in the paths.
        provider.Map(product => product.Brand, "brand"); // Canonical Brand IDs.
        provider.MapData<bool>("onSale_EUR"); // Boolean Data already exported by the mapper.
        provider.MapData<bool>("available", field => field.DataKey = "inStock"); // Query alias and stored key differ.
        provider.MapData<int>("quantity", field => field.DataKey = "stock"); // Existing numeric Data.
        provider.MapData<string[]>("tags"); // List membership and keyword facets, without list sorting.
    });
});
```

`Map` selects a direct built-in entity property. `MapData<TValue>` describes the stored
scalar or array type; supported scalar types are `string`, `bool`, `int`, `long`, `float`,
`double` and `decimal`. String/numeric arrays support exact membership and value facets.
The same methods are available for Content providers using Content properties.
Provider bindings configure queries only: they neither export nor duplicate values.
Bind an existing `Multilingual` Data value as `string`; the request language selects
its value. This does not rename or reshape legacy AutoMap output.
Use `IntegerExactFacet` for `int` bindings and `DecimalExactFacet` for other numeric bindings.

A Boolean query uses `KeywordFilter`/`KeywordFacet` with `"true"` or `"false"`. Missing
Data matches neither positive value; negation includes missing values. Keep exporting
`false` when a previously true business condition stops applying.

Unknown fields and inconsistent automatic destinations across Content mappings fail
explicitly. Supply one consistent explicit binding or separate providers. Custom converter
output must be bound explicitly because it can change Data keys, types and cardinality.
Culture-varying numeric and Boolean Data also require an explicit binding to a stable stored key;
the adapter does not guess legacy culture suffixes or duplicate invariant values.

| Query operation | Support |
| --- | --- |
| Ordinary search terms, relevance, paging and totals | Native Relewise search. |
| Scalar keyword equality and StringList membership | Native exact filters and keyword facets. |
| Boolean keyword filters/facets | Native Boolean Data. |
| Scalar numeric exact/range filters and value/range facets | Native numeric Data; ranges have inclusive lower and exclusive upper bounds. |
| Scalar string/numeric sorting | Native Data sorting; built-in ID, DisplayName, Brand ID and SalesPrice sorting are also supported where applicable. |
| Brand/Category filtering and facets | Canonical IDs from native relationships; Categories match anywhere in the exported paths. |
| Localized text Data | Existing Multilingual values and the request language. |
| List sorting and numeric-list ranges | Rejected; no artificial min/max or object copies are exported. |
| Field-specific full-text and date-specific operations | Rejected; use ordinary search terms or explicitly stored numeric timestamps with numeric operators. |
| Nested-object traversal and unknown/incompatible fields | Rejected before sending a search. |

Numeric buckets must be explicit, finite where bounded, nonempty and nonoverlapping.
A single positive compatible filter becomes the native facet selection. Negated or
multiple predicates remain hard filters and constrain all counts. Multiple facets on the
same stored target are rejected. Results and all supported facet counts use one search;
a missing facet response is an error, not a fabricated zero count. SalesPrice retains its
specialized currency-aware behavior described above.

The adapter does not discover or coerce externally stored values. Explicit bindings are
contracts for that Data; keep converter/custom-mapper output consistent with them.

## Querying

Umbraco 17/18 query models use `Umbraco.Cms.Search.Core.Models.Searching`
and its `Filtering`, `Faceting` and `Sorting` namespaces. Their searcher services
use `Umbraco.Cms.Search.Core.Services`. The examples below use the corresponding
Umbraco 19 namespaces under `Umbraco.Cms.Core.Search`.

Inject `Umbraco.Cms.Core.Search.ISearcherResolver`:

```csharp
const string providerAlias = "catalogue";
var searcher = searcherResolver.GetSearcher(providerAlias)
    ?? throw new InvalidOperationException("Search provider is not registered.");
var result = await searcher.SearchAsync(providerAlias, "daypack", culture: "en-US", skip: 0, take: 10);
```

To query Umbraco's default published-content provider, use
`Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent` instead. Results
contain the source Umbraco GUIDs, including when Relewise IDs come from a property.
A Variant match resolves to its Product page rather than creating a second result
for the same Product.

The SDK request language comes from the requested culture, falling back to Umbraco's
default language when the culture is null or blank. Result membership is separate:
a null culture selects invariant documents only; a specified culture includes
invariant documents and documents published in that culture. There is no
installation-wide language setting in the integration. Searchable fields and
relevance follow the Dataset's Relewise configuration; mapping a Data key does not automatically change its search settings.

`ConfigureRequest` receives a fresh, typed SDK request and runtime context with
`Services`, `User`, and `Culture`. Mandatory mapping membership, access control,
request user, language, query, paging, and the default Relewise index remain enforced. A blank
`DisplayedAtLocation` falls back to the configured value, then
`Umbraco search: {providerAlias}`. Keep this label stable across visitors and queries.

## Identity, access, and tracking

The backoffice dashboard uses the Umbraco session cookie for its API requests.

Register your `IRelewiseUserLocator` before building Umbraco, for example:

```csharp
builder.Services.AddScoped<IRelewiseUserLocator, MyUserLocator>();
```

Search and automatic views call `GetUserAsync()` on the locator resolved from the
HTTP request's service scope. Return the same identifiers and consent-aware User values used by the rest
of your site. The default locator returns an anonymous User; the integration does
not create visitor cookies. Results and facets share the same request and resolved User.
Automatic views and exports use the integration's Tracker. Search uses its Searcher;
entity reconciliation uses its Data accessor. They resolve through the integration's
reserved named SDK registration, inheriting default connection settings unless
overridden. The integration configures a two-minute Tracker timeout for exports.
They do not select an arbitrary application-named connection. Review the effective
values in the dashboard's **Connection details** section.

Call `u.TrackEntityViews()` in the Umbraco middleware setup. The mapped entity type
determines the event; a Variant view includes both Product and Variant IDs.
`TrackContentViews()` remains an alias for compatibility. Export, references,
result resolution, lifecycle actions, and tracking share ID mapping.

`AccessContext` describes authorization, not behavioral identity. Protection and
member/group permissions apply before paging and facet counting. An empty principal
cannot gain access through supplied group IDs. Explicit protection bypass still
retains mapping restrictions.

## Behavior and limitations

- Background changes combine into batches on Umbraco's existing queue. Terminal
  deletions, explicit exports and rebuilds preserve their accepted order. Explicit
  operations await delivery; background failures are logged and may require a rebuild.
- Rebuild and explicit export share the same writer as background indexing. A rebuild
  reconciles source metadata in Relewise after successful ingestion; it does not clear
  the Dataset or an independent search index.
- Relewise ingestion is asynchronous. Publication changes may take time to appear
  in searches. Repeated exports update canonical entities rather than adding a
  separate set of search entities.
- Results and supported facet counts use **one Relewise search request**. There
  are no supplementary searches per facet or range bucket.
- Supported filters, facets and sorting use the [direct field bindings](#direct-field-bindings)
  above, including native numeric Data range facets. Unsupported operations throw
  before a search request.
- A single positive filter matching a supported facet's type becomes a native
  facet selection: counts exclude its own selection while retaining other
  selections. Negated predicates, multiple filters on the same field, filters
  of an incompatible type remain ordinary filters and constrain all counts.
  Multiple facets on the same stored field are rejected. Mandatory mapping, publication and access
  restrictions always remain filters.
- For a nonblank query, `maxSuggestions > 0` adds one Relewise prediction request.
  `SearchResult.Suggestions` contains the returned terms, restricted to the provider's
  Content or Product target, language, visitor and configured filters, including
  mapping membership, access protection and publication requirements. Post-filters
  also constrain predictions. No prediction request is sent for a blank query.
- Numeric Data uses SDK doubles; higher numeric precision is not retained.
  Date-specific operations are unsupported.
- Segment-specific indexing is unsupported. Providers default to `UnsupportedSegmentBehavior.Throw()`;
  set `provider.UnsupportedSegmentBehavior = UnsupportedSegmentBehavior.Ignore()` to
  execute the query without its segment argument.
- New mappers and field-specific converters execute in an export scope. New global Data converters are scoped; legacy synchronous
  global converters retain their singleton lifetime. See [entity mapping](entity-mapping.md).
- Writer coordination is process-local. Coordinate full rebuilds and overlapping
  canonical identities across application instances externally. The integration stores
  no identity/dependency records or leases in Umbraco.

Umbraco's Search dashboard reads Relewise metadata through the Data Accessor, independently
of search-provider registration. `Relewise_MappedEntities` counts stored top-level entities
matching the exported entity types and Document Type mappings, including disabled
entities. Translations and embedded Variants do not add to the count. Named provider aliases
count only their configured entity type and Document Types. These are stored-entity counts,
not shopper-visible result counts: publication requirements, culture, access restrictions
and request callbacks are not applied. Entities outside the configured mapping types and
Document Type aliases are excluded.

Metadata is cached in memory for 30 seconds per alias, including failed reads. Concurrent
requests for the same alias share one refresh. Each refresh has a three-second cancellation
timeout and makes one total-count query per mapped entity type. The Product query also
fetches one Product because its supported constructor requires a positive page size.

`Healthy` confirms that the configured credentials can read the relevant backing data;
it does not verify search configuration, export freshness or write permissions. If a read
fails, a total is missing or no exported mappings exist, the status is `Unknown`. Umbraco
requires a numeric count, so unavailable metadata is represented by `0`; this is not evidence
of an empty Dataset. Failures are logged, and the next refresh retries after cache expiry.
Metadata requires Data Accessor read permission; credentials that only permit tracking may
still export successfully while metadata remains unavailable.

In **Settings → Relewise Dashboard → Search providers**, open **Details** beside a
provider to review its source Document Type aliases, published-page requirement, analytics
location and segment behavior. The overview identifies the default published-content
provider. Custom request callbacks are reported as registered but are not executed
to inspect their settings.
