# Umbraco search

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Migration](migration.md)

Use Relewise through Umbraco's core search abstraction with Content and Product
providers. Configure search independently of entity mapping and indexing.

## Registration

Configure the SDK connection as shown in the [README](../README.md), then configure
mapping and query providers independently:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options =>
    {
        options.MapContent("article", mapping => mapping.AutoMap());
        options.MapProduct("product", mapping => mapping.IdFromProperty("sku"));

        options.SearchProviders.AddContent("articles", provider =>
        {
            provider.DocumentTypeAliases = ["article"]; // Optional Content-only selection.
            provider.DisplayedAtLocation = "Site search"; // Stable analytics/merchandising label.
        });
        options.SearchProviders.AddProduct("catalogue", provider =>
        {
            provider.ConfigureRequest((request, context) =>
            {
                request.Currency = new Currency("DKK"); // Site-specific request currency.
            });
        });
        options.SearchProviders.DefaultPublishedContentProviderAlias = "articles";
    })
    .Build();
```

Use `Relewise.Client.DataTypes.Currency` in this example. Only Content and Product
query providers are supported. Each provider requires an exported mapping for its
entity type. For Content, omit `DocumentTypeAliases` (leave it `null`) to include all
exported Content types, or provide a nonempty list of exported Content aliases.
An empty array, unknown alias or non-exported mapping is rejected.
Several named providers may query the same entity type with different request
settings. Product providers use the single Product
mapping; `DocumentTypeAliases` is available only on Content providers.

Product providers default to `RequirePublishedPage = true`. An advanced search can
opt out when its caller handles results without a published Umbraco page:

```csharp
options.SearchProviders.AddProduct("commerceCatalogue", provider =>
{
    provider.RequirePublishedPage = false; // Include mapped Products whose Umbraco page is unavailable.
});
```

`rwUmbracoPublished` records whether the mapped source page is published, separately
from whether the Relewise Product is enabled. The integration maintains this
metadata regardless of whether a provider filters on it, including when
commerce contributes other Product fields. This option only omits that query
filter; site, mapping, access and culture restrictions remain. Content providers
always require a published page. Results still contain Umbraco document IDs;
callers must handle unavailable pages and any commerce destinations themselves.
`AdministrativeActions` is configured independently; this option does not enable
a disabled Product.

`AddRelewise` registers **one indexing pipeline**, using Umbraco's published-content
change and rebuild flow. It exports the configured entities whether or not query
providers are registered. Notification handlers do not run a second export.
Named query providers do not add indexing pipelines or create additional Relewise
indexes. Every provider uses the Dataset's default Relewise index.

Without `DefaultPublishedContentProviderAlias`, ordinary published-content queries
keep their existing provider. Setting it replaces query resolution for that alias;
Examine's indexer and change strategy remain registered. Draft content, media,
members, and elements retain their existing search providers.

## Querying

Inject `Umbraco.Cms.Core.Search.ISearcherResolver`:

```csharp
const string providerAlias = "catalogue";
var searcher = searcherResolver.GetSearcher(providerAlias)
    ?? throw new InvalidOperationException("Search provider is not registered.");
var result = await searcher.SearchAsync(providerAlias, "daypack", culture: "en-US", skip: 0, take: 10);
```

For ordinary Umbraco searches, use
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
`Services`, `User`, and `Culture`. Mandatory site/mapping membership, access control,
request user, language, query, paging, and the default Relewise index remain enforced. A blank
`DisplayedAtLocation` falls back to the configured value, then
`Umbraco search: {providerAlias}`. Keep this label stable across visitors and queries.

## Identity, access, and tracking

The backoffice dashboard uses the Umbraco session cookie for its API requests.

Register your `IRelewiseUserLocator` before building Umbraco, for example:

```csharp
builder.Services.AddScoped<IRelewiseUserLocator, MyUserLocator>();
```

Search and automatic views resolve the locator from the HTTP request's service
scope. Return the same identifiers and consent-aware User values used by the rest
of your site. The default locator returns an anonymous User; the integration does
not create visitor cookies. Facet requests reuse the main query's resolved User.
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
retains site and mapping restrictions.

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
- Keyword/text filters, numeric/date exact and range filters, sorting, and facets
  are translated from core fields. A range includes its lower bound and excludes
  its upper bound. Facets omit filters on their own field; range buckets require
  separate count requests. Requests with `maxSuggestions > 0` fail explicitly; use the SDK prediction API directly.
- Numeric Data uses doubles and dates use UTC milliseconds. Higher numeric or
  sub-millisecond precision is not retained in these core filter representations.
- Culture-specific numeric/date filters, sorters and facets use internal culture-specific
  fields. AutoMap's business Data representations remain unchanged. Segment-specific
  indexing is unsupported. Providers default to `UnsupportedSegmentBehavior.Throw()`;
  set `provider.UnsupportedSegmentBehavior = UnsupportedSegmentBehavior.Ignore()` to
  discard a query's segment argument and execute its ordinary form.
- New mappers and field-specific converters execute in an export scope. New global Data converters are scoped; legacy synchronous
  global converters retain their singleton lifetime. See [entity mapping](entity-mapping.md).
- Writer coordination is process-local. Coordinate full rebuilds and overlapping
  canonical identities across application instances externally. The integration stores
  no identity/dependency records or leases in Umbraco.

Umbraco's indexing dashboard shows a required document-count placeholder of `0`
and `Unknown` health for Relewise. Counts and health are unavailable; zero does not
indicate an empty Dataset. The Relewise dashboard includes this explanation.

In **Settings → Relewise Dashboard → Search providers**, open **Details** beside a
provider to review its source Document Type aliases, published-page requirement, analytics
location and segment behavior. The overview identifies the default published-content
provider. Custom request callbacks are reported as registered but are not executed
to inspect their settings.
