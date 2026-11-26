# Relewise.DemoStorefront

Fieldwork is a synthetic outdoor shop demonstrating the Relewise Umbraco integration.
Products and brands are fictional; checkout records a demo Order without taking payment
or fulfilling purchases.

## Run locally

Install Git, the .NET 11 SDK, Node 24.13+ and npm 11+. No IDE is required.
Clone the repository, enter the DemoStorefront directory and start the site:

```text
git clone https://github.com/Relewise/relewise-integrations-umbraco.git
cd relewise-integrations-umbraco/samples/Relewise.DemoStorefront
dotnet run
```

These commands work in PowerShell, Command Prompt, Bash and Zsh. The single launch
profile is selected automatically. The build installs frontend dependencies and
builds the Vue components; no separate frontend server is needed. JavaScript is required.

First startup creates a local Umbraco SQLite database and seeds English/Danish pages,
Products, Variants, Brands, Categories, prices, editorial blocks and bundled images.
Once startup finishes, open [the homepage](http://localhost:5521/en/) in your browser.
Press **Ctrl+C** in the terminal to stop the site. The market selector switches language
and currency together: Europe (English/EUR), Denmark (Danish/DKK) and US (English/USD).
Prices are authored per currency rather than converted.

The seeded CMS content preview starts without Relewise credentials. Browse the
published pages and inspect the mapping and converter examples below. The sample
code also demonstrates Relewise search, facets, predictions, recommendations and
behavioral tracking, with shopping flows implemented in Razor and Vue.

Alternatively, open `Relewise.Integrations.Umbraco.slnx` in Visual Studio, select
**Relewise.DemoStorefront** as the startup project and press **F5**. In VS Code,
open the DemoStorefront folder and run `dotnet run` in its integrated terminal.

### Connect to Relewise

1. Use a dedicated test Dataset. The sample exports fictional catalog data and sends
   behavioral events. In My Relewise, obtain its **Dataset ID**, **API Key** and
   **Server URL**. The key must permit entity updates and administrative actions,
   entity reads, search and predictions, recommendations and behavioral tracking.
2. Create `local.settings.json` in the current DemoStorefront directory
   (`samples/Relewise.DemoStorefront`):

   ```json
   {
     "Relewise": {
       "DatasetId": "<dataset-id>",
       "ApiKey": "<api-key>",
       "ServerUrl": "<server-url>"
     }
   }
   ```

   Replace all placeholders with the Dataset's values. Use the complete Server URL
   from My Relewise. The repository's `.gitignore` excludes this file from Git.

   In production applications using the integration, store API credentials in a
   managed secret store such as Azure Key Vault and load them through configuration.
3. Stop the site with **Ctrl+C** if it is running, then restart it from the
   DemoStorefront directory:

   ```text
   dotnet run
   ```

   Startup installs and seeds Umbraco, then queues an export of published data.
   Allow ingestion to finish before checking search results. The same export runs
   on subsequent starts.
4. Open `http://localhost:5521/en/` and verify the exported entities in My Relewise.
   Try search, facets, market selection, recommendations, cart and simulated checkout.
   Invalid credentials produce a connection error; the sample does not substitute
   another search provider. Recently viewed and popularity examples need activity
   before they return results.

### Backoffice and seeded data

For backoffice editing, open `/umbraco`. The default account is `demo@example.invalid`;
[DemoSetup.cs](Setup/DemoSetup.cs) defines its generated local password. To use your own
account, set Umbraco's unattended installation credentials in `local.settings.json`
**before first startup**.

Database, indexes, logs and credentials are ignored by Git. Stop the site before
removing its ignored `umbraco/Data` directory to start with a fresh CMS database.
When changing the listening URL, also update `Umbraco:CMS:WebRouting:UmbracoApplicationUrl`
so seeded language domains use the same host and port.

## Mapping and converter examples

[DemoConfiguration.cs](Integration/DemoConfiguration.cs) is the starting point for the fixed C#
registration and named Content/Product search providers.
[DemoConstants.cs](DemoConstants.cs) groups the sample's document types, property aliases,
business Data keys and provider names. Currency-specific sale fields use a shared
key-building method; package metadata uses the integration's `RelewiseDataKeys`.

| Example | What it demonstrates | Source |
| --- | --- | --- |
| Products and Variants | SKU-based canonical IDs, built-in properties, child Variants, Brand and additional Category references | [DemoConfiguration.cs](Integration/DemoConfiguration.cs) |
| Multi-currency prices | An asynchronous typed converter reads native Umbraco Block List currency/amount entries | [PriceConverter.cs](Integration/Converters/PriceConverter.cs) |
| Product enrichment | An entity mapper runs last and writes native Boolean availability and per-currency sale flags | [DemoProductMapper.cs](Integration/Mapping/DemoProductMapper.cs) |
| Localized editorial content | Culture-aware conversion of rich text, nested Block Lists and Block Grid areas; hidden blocks are excluded | [EditorialBlocksConverter.cs](Integration/Converters/EditorialBlocksConverter.cs), [EditorialBlockText.cs](Integration/Converters/EditorialBlockText.cs) |
| Content enrichment | A custom mapper combines converted editorial fields into searchable text | [EditorialMapper.cs](Integration/Mapping/EditorialMapper.cs) |
| Media URLs | A globally registered converter resolves media properties into URLs | [ImageUrlConverter.cs](Integration/Converters/ImageUrlConverter.cs) |
| Legacy compatibility | The guide uses the obsolete Content mapper through the compatibility adapter | [LegacyGuideMapper.cs](Integration/Mapping/LegacyGuideMapper.cs) |
| Entity replacement | Articles, Products and Variants replace omitted fields on rebuild; localized articles do so on delta updates too. Brand/Category and legacy examples retain partial updates | [DemoConfiguration.cs](Integration/DemoConfiguration.cs), [update policy](../../docs/entity-mapping.md#entity-update-policy) |

Edit these published properties in Umbraco to explore the examples. Integration details
on detail pages show the document type, culture, node ID/key and canonical Relewise IDs;
Variant pages include both Product and Variant IDs.

For externally managed Products, use `EntityUpdatePolicy.ReplaceProvidedProperties()`
on the Product and any configured Variants, and set the Product's
`AdministrativeActionPolicy.None()` in C#; see the [mapping guide](../../docs/entity-mapping.md).
The demo's default Product mapping
owns its publication lifecycle.

## Search, recommendations and tracking

Search and facets use Umbraco's search abstraction with three named Relewise providers:
`articles`, `catalogue` and `offers`. Ordinary published-content searches use `articles`.
[The search guide](../../docs/search.md) covers provider configuration; the demo's
[query code](Features/Browsing/StorefrontSearch.cs) shows Category/Brand filters, stock, currency-specific
sale flags, price ranges, sorting and facets. The `shopPrice` query field binds to
the Product's built-in SalesPrice in the active market currency; it does not
require a duplicate indexed price field. Results and all displayed facet counts
use one Relewise search request. See [supported search capabilities](../../docs/search.md#product-prices).

To search text extracted from editorial blocks, include the Content Data key
`searchText` in the Dataset's search configuration. Exporting custom Data does not
change that configuration.

The shared search field uses real Relewise predictions, with keyboard navigation,
editable suggestions and a dedicated results page. Category and Brand landing pages
keep their fixed scope when shoppers apply other filters. The footer links to all
published Brands and Categories.

[StorefrontRecommendations.cs](Features/Recommendations/StorefrontRecommendations.cs) uses the C# Recommender
SDK directly, with request examples in [RecommendationRequests.cs](Features/Recommendations/RecommendationRequests.cs): Similar Products on Product pages, genuine Recently Viewed history,
and views-based Popular Products/Categories on the homepage. Empty carts show history
first, then a separately labelled popular fallback. Popularity uses the last 30 days;
a fresh Dataset without activity may return nothing, and empty sections stay hidden.
The editable `productType` field distinguishes functional alternatives. Results are
checked against current publication, access, canonical identity and market prices.

Search, recommendations, page views, Cart and Order events share
[StorefrontUserLocator.cs](Integration/Tracking/StorefrontUserLocator.cs). Product and Variant views use the
same canonical IDs as export. Recommendations do not create extra Product views.
The cart updates asynchronously and validates quantities/prices on the server.
Protected Product and Variant pages are excluded from cart actions as well as browsing.
Tracking failures prevent Cart/Order changes from being saved. Retry deduplication and
cart state are in memory and are illustrative rather than a production commerce engine.

Relewise's footer strip and Demo information dialog remain English across markets.
The dialog shows configuration and documentation links without exposing credentials.
Checkout uses compact navigation and a shorter demo notice.

## Project structure

Everything stays in this one project; namespaces follow the folders.

| Area | Responsibility |
| --- | --- |
| [Integration](Integration) | Package registration, mapper/converter examples, indexing and tracking identity |
| [Features](Features) | Browsing, Products, editorial pages, recommendations and Cart; related models stay with their feature |
| [Site](Site) | Shared view models, Razor endpoint adapter, published-content lookup, navigation and market selection |
| [Views](Views) | Razor pages, layout and shared partials; normal Razor encoding for text and attributes |
| [Setup](Setup) / [Seeding](Seeding) | Local installation/connection checks and initial Umbraco schema/content |
| [Client/src](Client/src) | Vue components grouped into shell, browsing, Cart and recommendations |

`Program.cs` composes startup and registers endpoints. Cart HTTP endpoints delegate
shopping operations, pricing, state and Relewise event construction to separate files;
typed view models feed the Razor templates. Listings, detail pages and Cart share
[ProductPrices](Features/Products/ProductPrices.cs). Razor owns forms and antiforgery inputs;
Vue keeps the interactive search, recommendations and cart experience. The existing Vue
image payload uses a small, encoded `TagBuilder` adapter rather than page templates in C#.
`Views` and `wwwroot` retain their conventional locations.

## Developing the sample

Run `npm run build` in `Client` after frontend changes, or use `npm run watch` while
editing. The sample intentionally has no frontend test suite; inspect the CMS preview's
keyboard interactions and responsive layouts in a browser.
[Storefront regression tests](../../src/Integrations.Umbraco.Tests/DemoStorefront) cover
mapping, shopping/search rules and Razor encoding within the shared backend test project.
Run `dotnet test Relewise.Integrations.Umbraco.slnx` from the repository root.
