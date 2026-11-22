# Relewise.DemoStorefront

A runnable Umbraco 19 example for entity mapping, search and behavioral tracking.

## Run with F5

Install the .NET 11 SDK, Node 24.13+ and npm 11+. Open the repository solution,
select **Relewise.DemoStorefront** as the startup project, and press F5. First startup
installs Umbraco into a local SQLite database and seeds document types, languages,
Block List and Block Grid content, English/Danish Dictionary Items, culture routing,
templates and published pages. Images are bundled under
`wwwroot/images`; nothing is downloaded from an image service.

The browser opens the homepage at `/en/` by default (Europe, English, EUR).
`/da/` opens the Danish homepage; `/en/store` and `/da/store` open the shop. With no Relewise configuration,
the storefront runs in **Content preview** mode. Published pages, seeded images,
prices, Variants and market/language selection can be explored. Search, facets,
predictions, Offers, cart and checkout require Relewise and are disabled without it,
including direct backend requests. A prominent notice links to setup guidance;
**Demo details** in the footer shows configuration state. Shop, category, Brand and
Journal pages show a small CMS-only preview, without search or filtering.
No manual Umbraco configuration is needed. If changing the listening host/port,
update both `Properties/launchSettings.json` and
`Umbraco:CMS:WebRouting:UmbracoApplicationUrl` in application settings before startup;
the seeded culture domains use that application URL.
The generated database, indexes and logs are ignored by Git. The project build
restores the locked frontend dependencies and builds the Vue frontend automatically;
no separate frontend server is required. JavaScript is required for the storefront interactions.

The site contains 13 Journal stories plus one guide, all bilingual and seeded
automatically. Ten additional articles cover packing, camping, outdoor routines
and caring for equipment, using editable native Block List rich text.

The catalogue contains 80 Products and 222 color/size Variants across packs,
drinkware, camping and accessories. Products, Variants, categories and editorial
content have English and Danish language variants. Names, descriptions, option
labels and editorial text are translated; IDs, images, relationships and prices
are shared. Prices are entered separately in DKK, EUR and USD, without currency
conversion. Three illustrative outdoor Brands—Fieldwork, Ridgeway and Embercamp—
span all four shopping categories. Brand names are shared across languages;
their descriptions and pages have English and Danish variants.

Startup adds missing seed content, upgrades the previous invariant demo to language
variants, and preserves existing English values, IDs and prices. Once seeded,
subsequent starts preserve edits in both languages and Dictionary Items. All
content sits beneath the home node, whose native Umbraco Culture and Hostnames
settings provide `/en` and `/da` URLs. Interface labels are editable in Umbraco's
Dictionary Items under the `Fieldwork.` keys.

## Connect a local Relewise Dataset

Create `local.settings.json` in this project. It is ignored by Git. A commented
example of this section also appears in `appsettings.json`:

```json
{
  "Relewise": {
    "DatasetId": "<local-dataset-guid>",
    "ApiKey": "<local-api-key>",
    "ServerUrl": "https://localhost:5000/"
  }
}
```

Use an existing local API host and disposable local Dataset, with search configured
for the mapped fields. This project does not provision a Relewise backend. Partial
or nonlocal connection settings fail with a configuration message. Credentials do
not belong in the checked-in configuration.
Configuration is not a connectivity check. Rejected credentials or an unavailable
API produce an explicit connection error, without switching to another provider
or content preview. Search and mapped-page view failures return HTTP 503; listing-data and prediction
failures return HTTP 503 as JSON. A failed in-place update keeps the previous
results visible and offers a retry. Connection details and credentials are not included in the
browser error message. Technical details are logged on the server.

Tracking is awaited: a failed Cart or Order tracking request prevents the demo
from saving that shopping action and shows a connection message. There is no
persistent retry queue or guarantee of exactly-once delivery after an ambiguous
network failure. Without Relewise configuration, shopping requests are rejected
with HTTP 503 and no tracking visitor cookie is created. Only CMS content preview
is available; requests never silently substitute another search provider.

On startup, an already seeded site exports its published content through the shared
writer. Search and automatic views use the same request-scoped visitor locator.
The demo creates a temporary visitor cookie; identifier creation is application
behavior, not behavior added by the package.

For backoffice editing, set your preferred Umbraco unattended user/password in
`local.settings.json` **before first startup**. The default local account is
`demo@example.invalid`; its generated password is defined by `DemoSetup`.

## Mapping examples

- [DemoConfiguration.cs](DemoConfiguration.cs): the registration for all examples:
  Content/Product/Brand/Category mapping, child Variants, canonical IDs, field
  converters, final entity mappers, named search providers and default query routing.
- [PriceConverter.cs](PriceConverter.cs): strongly typed asynchronous conversion of
  editable currency/amount blocks into the SDK's `MultiCurrency` built-in property.
- [ImageUrlConverter.cs](ImageUrlConverter.cs): global asynchronous Data conversion,
  overriding a built-in regardless of registration order.
- [EditorialBlocksConverter.cs](EditorialBlocksConverter.cs) and
  [EditorialBlockText.cs](EditorialBlockText.cs): field-specific Block List/Grid text
  extraction, including nested elements and explicit editorial field selection.
- [EditorialMapper.cs](EditorialMapper.cs): runs after declarative mapping, combines
  converted body/block text into `Data["searchText"]`, and adds `editorialSource`.
  Keeps DisplayName, IDs and other mapped fields intact. A single published culture
  uses a scalar value; multiple cultures use the SDK's `Multilingual` value. Empty
  source text overwrites an earlier search-text value.
- [DemoProductMapper.cs](DemoProductMapper.cs): enriches the mapped SDK Product with
  Boolean `Data["inStock"]` and per-currency Boolean entries such as
  `Data["onSale_EUR"]`, calculated from stock and the Product's built-in
  multi-currency prices. This business Data is
  directly available for Relewise queries and merchandising.
- [StorefrontUserLocator.cs](StorefrontUserLocator.cs): one visitor identity for search and views.
- [LegacyGuideMapper.cs](LegacyGuideMapper.cs): compatibility with the obsolete
  `IContentTypeMapping` contract.

Adapt aliases, extraction rules and currencies to your site's model. The block
extractor uses HtmlAgilityPack only in this example application.

The extractor visits only selected fields, nested blocks and Block Grid areas,
removes HTML markup and script/style/template text, and respects the example's
`hide` setting. Open **Finding your trail** for nested `contentRows` Block List
content, rich text and `pageGrid` areas. Hidden blocks and a hidden Grid container
remain editable in Umbraco but contribute no text, including the container's children.
The detail page shows the extracted block text, with HTML removed and entities decoded,
so it is easy to compare the editorial source with the text used for export.

Open **Journal**, choose **Across the seasons**, and switch market between Denmark
and Europe/United States. Journal combines the `article` and `localizedArticle`
content types; their separate mappings remain as integration examples.
The **Across the seasons** article's translated
outer Block List contains a nested element with localized headings and rich text.
The containing group also varies by culture, so Umbraco preserves its children's
localized values. The mapper reads those elements separately for each published
culture instead of reusing the first culture's converted text. The mapper and
converter receive Umbraco's variation context through DI, set the culture while
reading blocks, and restore it even when conversion fails. This also works during
background exports, where there is no frontend request setting the culture.
See [DemoEditorialSchema.cs](DemoEditorialSchema.cs)
for the editor setup and [DemoBlockValues.cs](DemoBlockValues.cs) for native seed values.

### Multiple currencies

Each Product has separate **List prices** and **Sales prices** Block Lists. Add one
entry per currency, choosing a currency and entering a decimal amount. The demo
offers DKK, EUR and USD; edit the **Demo currencies** data type in Umbraco to change
those choices. Add a corresponding entry in `DemoMarket.All` to expose a new
shopper market with that currency. The converter reads the selected codes from the published blocks;
it does not assign a fixed currency or calculate exchange rates. For example, the
daypack's list prices are DKK 899.00, EUR 119.95 and USD 129.95.

The converter rejects duplicate currency codes and incomplete entries. A null
published value produces no price value; an empty Block List produces an empty
`MultiCurrency`. Neither invents a zero price. A missing source property is a
mapping error. The storefront displays the selected market's currency. A missing price is shown as
unavailable; the storefront does not substitute another currency.

### Market selection

The header's **Market** dropdown selects language and currency together:

| Market | Content language | Currency |
| --- | --- | --- |
| Europe (default) | English (`en-US`) | EUR |
| Denmark | Danish (`da-DK`) | DKK |
| United States | English (`en-US`) | USD |

The selection is remembered for 30 days in a local preference cookie and follows
navigation, searches and pagination. Switching market on a detail page opens the
same content item in the selected language, using its translated URL. Explicit
`/en` or `/da` URLs take priority over a conflicting remembered language. Both
European and US markets use English content, with their own currency.

[DemoMarket.cs](DemoMarket.cs) defines these example markets. The search callbacks
in [DemoConfiguration.cs](DemoConfiguration.cs) read the request-scoped market to
set currency; storefront queries supply its language. Indexing always exports all
entered currencies and published languages, independently of the visitor's market.

Build all examples with:

```sh
dotnet build samples/Relewise.DemoStorefront/Relewise.DemoStorefront.csproj
```

No Relewise credentials are required to compile or run content preview. A local
Dataset is required for the search and tracked-shopping examples.
The converters implement asynchronous contracts but perform local conversions;
they do not make external network requests.

## Shopping navigation

The header has a central global search, a Cart link with live item count, compact
market selection, and a separate row of category/Offers/Journal links. On mobile,
search remains visible and the Menu button opens category navigation and market
selection. The market dialog selects language and currency together.

Global search opens `/en/search` (or `/da/search`) and starts a fresh search in
the selected Product or Journal scope (Products by default), independent of the
current category, Offers and price filters. Its
Products and Journal tabs provide explicit entity scopes. The same global field
is used on category and results pages; there is no second category search box.
Predictions follow the selected entity scope and language.

`/en/offers` is a separate landing page with its own title, breadcrumb and active
navigation state. Its sale restriction remains applied while filtering, sorting
and paging. Only actual reductions in the selected currency qualify. Legacy
`/en/store?provider=offers` links redirect there. Global search leaves Offers.

**Demo details** in the footer contains provider, configuration state, Dataset ID
and setup links. It is separate from shopping controls. Request failures are
shown beside the affected search or purchase action. Checkout uses a minimal
header with a return-to-cart link and omits shopping navigation, global search,
market controls and demo-details disclosure.

## Exploring filters and facets

Shop has enough Products for several pages, with low, middle and high prices in
DKK, EUR and USD. The expanded catalogue includes trekking and travel packs,
camping shelters and kitchen equipment, drinkware and trail accessories. Seven
Products are out of stock; others have low or plentiful stock. Category and price
facets can be combined with **In stock**,
search text and price/name sorting through Umbraco's search abstraction.

| Seeded example | What to try |
| --- | --- |
| Trail repair kit | Visible in the catalogue but out of stock; **In stock** removes it and cart addition is rejected. |
| Packable picnic blanket | Stock is two: try adding three. Its DKK 400 and USD 50 prices sit exactly at the middle price range's lower boundary. |
| Picnic blanket and repair kit | Belong to Camping and Accessories; selecting both categories returns each Product once. |
| Picnic blanket | Discounted in EUR only; it appears in Offers for Europe, but not Denmark or the US. |
| Repair kit | Discounted in USD only; US Offers includes it until **In stock** is applied. |
| Existing discounted packs, flask, shelter, chair and lantern | Offers remain populated in every market, alongside full-price Products. |
| Journal and Across the seasons | Explore localized rich text, nested Block List, Block Grid and hidden-block extraction separately from Product facets. |

On an untouched installation, Offers contains 14 Products in Europe, 14 in the US
and 13 in Denmark. The catalogue contains 73 in-stock Products and seven sold-out
Products. Additional category memberships demonstrate overlapping facet selections.
Missing seed Products are added on startup; authored edits to existing Products
are preserved and can change these counts.

Product listings display 24 items per page. Mobile shows the active-filter count
beside the filter control. A **Show products** link inside mobile filters closes
the panel and focuses the already updated results;
it does not apply filters or send another search. The displayed range, such as
**Showing 25–48 of 80**, appears in the result toolbar and pagination.
Desktop has a filter sidebar; mobile uses a compact listing header,
a sticky Filter/Sort toolbar and a collapsible
native disclosure. Product names and prices precede secondary card information. Applied-filter chips remove individual
selections, while clearing filters retains the search, provider and sorting. Cards
show stock, Variant options, sale prices and original prices.
Sale cards and Product/Variant pages also show savings calculated from the active
currency's prices. Whole percentages are rounded down; reductions below 1% show
the monetary saving instead. Missing prices and non-discounts have no savings badge.
All filtering, counting, sorting and paging happen through Umbraco's search abstraction. Category
pages keep their category fixed by the URL and offer price/stock facets plus child
category navigation; Shop and Brand pages additionally offer category facets.
This keeps the fixed scope intact when the Relewise adapter excludes a facet's
selected filters to calculate alternative counts. Unselected zero-count choices
are disabled; selected choices remain removable.

Search suggestions use that abstraction's `maxSuggestions` argument. The Relewise
adapter translates it to a scoped prediction request. The demo debounces typing,
cancels superseded requests and uses a native HTML datalist. Explicit search
continues to work when predictions fail. Relewise returns language- and
provider-scoped predictions; neither operation is enabled in content preview.
No-results links let visitors remove filters or browse without the query while
retaining their current collection.

[StorefrontListing.cs](StorefrontListing.cs) supplies initial JSON data and the
`/{language}/store/results` endpoint. It uses the same query path for Shop, Journal,
Brand and Category listings. [StorefrontSearch.cs](StorefrontSearch.cs) builds the
native query, facets and filter links. The
[Vue listing](Client/src/Listing.vue) owns reactive filters, sorting, cards, paging
and loading/error presentation. [Header.vue](Client/src/Header.vue) and
[SearchBox.vue](Client/src/SearchBox.vue) provide global navigation and search/predictions. [ListingCard.vue](Client/src/ListingCard.vue)
renders cards from server-supplied presentation data. Product and editorial detail
pages remain server-rendered; SDK requests and credentials stay on the server.

With Relewise, **Recommended** leaves the default personalized ranking intact for
both text searches and browsing/category pages. Explicit price sorting uses
relevance descending as its tie-breaker. Name A–Z/Z–A remains an explicit choice.

Checkboxes and pointer-selected sorting apply immediately. Keyboard sorting commits
on Enter or leaving the control. Price bounds apply when leaving the price group
or pressing Enter, after validation. Results and counts update in place; filters
retain focus, scroll and the open mobile panel. The URL remains shareable and
Back/Forward restores the selection. Failed updates leave the previous results
visible with an explicit error and retry action. Superseded requests are cancelled
and their responses cannot replace newer results.

For frontend development, run `npm run watch` in `Client` while running the site.
Run `npm test` there for component regressions and `npm run build` for a production
bundle. Generated bundles and `node_modules` are ignored. The .NET project builds
the bundle automatically when its inputs change.
[StorefrontProductIndexer.cs](StorefrontProductIndexer.cs) supplies typed fields
for the Umbraco search abstraction. They are projected into the adapter's
`rwUmbracoField_*` metadata on Relewise entities. Category/Brand membership and
numeric price queries use this projection.
Stock and sale queries bind directly to Boolean business Data written by
[DemoProductMapper.cs](DemoProductMapper.cs). The demo leaves Examine at its
standard Umbraco configuration. Connected startup
runs the integration's shared full export, as described above.
[DemoProductDetails.cs](DemoProductDetails.cs)
seeds editable weight and capacity properties for selected Products and more
representative bundled SVG illustrations. Specifications are fictional example
data. The demo does not simulate customer reviews, product photography, image
zoom or a multi-angle gallery.

The shopper-facing category facet contains the four direct children of the
Equipment category. Parent groupings and the additional mapping-example categories
remain indexed but do not appear as peer shopping categories.

The price facet offers preset bands with counts and a separate **Custom price
range** with **Min** and **Max** in the selected currency. Both custom endpoints
are inclusive; equal endpoints find an exact price. Demo price entries must be
nonnegative amounts with at most two decimal places. Umbraco's native range filter
has an exclusive upper bound, so the demo translates Max to the next cent without
changing the displayed amount. Preset band boundaries remain lower-inclusive and
upper-exclusive. Editing a custom range clears presets; selecting a preset clears
both inputs. Mixed preset/custom URLs and invalid bounds are rejected by the server.
Custom edits apply on Enter or leaving the input group, after validation, and
produce one removable chip that clears both bounds.

**Show only** groups the independent **In stock** and **On sale** checkboxes, with
counts. On sale uses the active currency's sales price below list price and combines
with category, search, stock and price filters. It applies immediately and has a
removable chip. Offers already enforces this restriction, so it omits the On sale
checkbox and chip. Market changes recalculate sale eligibility and counts.
`DemoProductMapper` compares the mapped SDK `SalesPrice` and `ListPrice` for each
currency and writes `onSale_EUR`, `onSale_DKK` and `onSale_USD` as Booleans,
including `false` when no discount exists. Product providers bind these keys with
`provider.MapBooleanData("onSale_EUR")`. Filters and facets read the same Boolean
Data directly; no additional offer fields or price-to-price query comparison is
required. In stock similarly binds to `Data["inStock"]`. The common Product provider
configuration also enables Variant matching consistently for Shop and Offers.

The **Brands** facet supports several selections (OR), combined with category,
price, stock and sale filters (AND). Product cards and detail pages show the actual
Brand. Brand pages fix that scope in the URL and omit the redundant Brand facet.
The native `shopBrand` field holds the picked Umbraco node key for filtering;
the integration resolves the same picker to the configured canonical `brandId`
when exporting the Product's Relewise Brand reference.

New sample Products receive a deterministic Brand assignment from their SKU.
When upgrading the previous single-Brand seed, creation of both additional seed
Brands triggers a one-time redistribution of known sample Products still pointing
to Fieldwork. Drafts, partially published Products and other Brand choices are
preserved. Later starts retain manual Brand changes, including choosing Fieldwork
again. Authored Products outside the seed catalogue are not reassigned.

Colour/size facets across Product Variants are not included. Relewise supports
Variant facets through its SDK; the current Umbraco bridge addresses document
fields, and does not express a Variant target or automatically aggregate child
fields onto Product documents. This needs an explicit bridge/indexing design.

## Shopping and checkout

The homepage leads into category collections, the Shop and the Journal. Category,
Brand and search pages share the same search-driven listing, scoped through
native Umbraco keyword filters. Category membership includes additional category
pickers; Brand scope uses the referenced Brand node key. Breadcrumbs follow the page hierarchy;
article pages link back to the Journal. Technical entity identifiers stay inside
collapsed Integration details on detail pages.

Select a Product option, such as colour or capacity, to add that Variant.
[Purchase.vue](Client/src/Purchase.vue) submits the addition asynchronously. After
server confirmation, the header count updates and an accessible drawer shows the
item, Variant, quantity and basket total, with View cart, Checkout and Continue
shopping actions. Mobile uses a bottom sheet. Escape/Close restores focus; motion
respects the browser's reduced-motion preference. Stock errors remain beside the
purchase controls. Prices, quantities and stock are validated on the server;
client-supplied prices are never accepted.

[Cart.vue](Client/src/Cart.vue) provides asynchronous quantity changes and removal,
with an order summary and primary Checkout action. Empty cart is a secondary
action beneath the items and requires confirmation. Checkout is disabled during
updates and validates unsaved quantity edits before normal navigation. Continue shopping retains
the previous collection or search/Offers view, query, filters and sorting.

Add requests use a command ID that remains stable across network retries. The
server serializes basket writes and remembers the latest 16 completed add
commands per basket, preventing retained commands from being applied twice.
This history is in memory, not a durable delivery guarantee. Legacy form posts
still use redirects; the storefront UI uses JSON responses and inline errors.

Checkout has one enclosed review step and **Place demo order**, with no payment,
address or personal-information form. Nothing is shipped. The demo validates
against the Product's shared stock limit across Variants; it does not reserve or
decrement inventory. Real inventory and order persistence belong to the commerce
platform. Market changes reprice the basket and invalidate its checkout review.

[StorefrontCart.cs](StorefrontCart.cs) resolves the integration's Tracker and the
same request-scoped `IRelewiseUserLocator` used for search and view tracking. Every
cart mutation sends the complete basket. Changing market reprices and tracks the
basket in the selected currency. Checkout tracks an Order and an empty Cart with
the same User and canonical Product/Variant IDs. A session checkout token prevents
refreshes or repeated submissions from creating duplicate orders.

Cart and confirmation state use a shared in-memory basket keyed by the ASP.NET
Core session for this single-process demo. Writes are serialized so overlapping
requests cannot overwrite a newer basket with a stale session snapshot. Restarting the host clears shopping sessions; published
Umbraco content remains in SQLite. Without Relewise configuration, shopping
is disabled and no tracking requests are sent. A real shop would use its own
persistent cart/order store rather than this demonstration's session state.

[DemoVariantDetails.cs](DemoVariantDetails.cs) adds localized, editable
`optionLabel` values to the seeded size-based Variants: reservoir capacities,
shoe sizes, lengths, dimensions, pack quantities and occupancy. The original SKUs,
node names and URLs remain stable; untouched seed headings are updated to match
the descriptive labels. A localized glove size guide explains how to
measure. Capacity fields and descriptive labels are also exported to Relewise.
[DemoIllustrations.cs](DemoIllustrations.cs) uses a bundled SVG sprite to reflect
the selected colour; all illustrations remain local assets. Custom
image URLs continue to render as images.

[StorefrontNavigation.cs](StorefrontNavigation.cs) maintains listing context and
one-time feedback. Basket state remains in the shared in-memory store; navigation
state does not serialize or overwrite basket contents.

## Developer search examples

The global search offers Products and Journal scopes. The technical provider
selector is intentionally absent. Explicit example URLs remain available; their
filters and sorting preserve the chosen provider. Starting another global search
uses the selected Products/Journal scope rather than retaining that profile:

| Example | URL |
| --- | --- |
| Shop / Product browsing | [Shop](http://localhost:5521/en/store) |
| Global Product search | [Search](http://localhost:5521/en/search?q=flask) |
| Journal / articles and seasonal story | [Journal](http://localhost:5521/en/store?type=article) |
| Guide / legacy Content mapper | [Guides](http://localhost:5521/en/store?provider=guides) |
| All editorial content, including guides | [All editorial](http://localhost:5521/en/store?provider=allEditorial) |
| Seasonal story / localized nested blocks | [Localized content](http://localhost:5521/en/store?provider=localized) |
| Predefined Product filter | [Offers](http://localhost:5521/en/offers) |
| Umbraco default published-content search alias | [Default search](http://localhost:5521/en/store?provider=Umb_PublishedContent) |
| Externally managed Product example (`Demo:ProductPolicy=None`) | [Commerce catalogue](http://localhost:5521/en/store?provider=commerceCatalogue) |

Replace `/en/` with `/da/` to test Danish; adjust the host/port if configured
differently. Relewise providers are registered when a local connection is configured.
Without one, search URLs return an unavailable response; ordinary CMS routes
provide content preview only. The default search alias uses Relewise when
`Demo:DefaultSearch` is enabled. Setting it to `false` leaves named providers
available but disables that alias in the storefront. The package's ability to
leave Umbraco's own query provider registered is unchanged; the demo does not use it.

## Acceptance testing

Configuration switches exercise the same application:

- `Demo:Search=false`: no named Relewise providers; indexing and tracking still work.
- `Demo:DefaultSearch=false`: named Relewise providers exist; the storefront rejects default-alias searches instead of falling back.
- `Demo:DefaultProvider=catalogue`: default published-content searches use Product search.
- `Demo:ProductPolicy=None`: preserve externally controlled Product state and register
  `commerceCatalogue`, which omits the published-page requirement. Other providers
  keep that requirement.

These switches require a local Relewise connection and a site restart.

Use the storefront and Umbraco backoffice to exercise publishing, draft edits,
unpublication, deletion and rebuilding. Verify the disconnected preview and invalid
credential states too. Keep raw reports outside tracked files.

See [entity mapping](../../docs/entity-mapping.md) and
[Umbraco search](../../docs/search.md) for package behavior and limitations.
