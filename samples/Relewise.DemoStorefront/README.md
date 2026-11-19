# Relewise.DemoStorefront

A runnable Umbraco 19 example for entity mapping, search and behavioral tracking.

## Run with F5

Install the .NET 11 SDK, Node 24.13+ and npm 11+. Open the repository solution,
select **Relewise.DemoStorefront** as the startup project, and press F5. First startup
installs Umbraco into a local SQLite database and seeds document types, languages,
Block List content, templates and published pages. Images are bundled under
`wwwroot/images`; nothing is downloaded from an image service.

The browser opens `/store`. With no Relewise configuration, Examine serves the
storefront's searches. Subsequent starts preserve content edits. The generated
Umbraco database, indexes and logs are ignored by Git. If a later checkout changes
the demo schema, stop the site and move its ignored `umbraco/Data` directory aside
to create a fresh demo installation.

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
  converters, final entity mappers, named profiles and default query routing.
- [PriceConverter.cs](PriceConverter.cs): strongly typed asynchronous built-in conversion.
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
- [StorefrontUserLocator.cs](StorefrontUserLocator.cs): one visitor identity for search and views.
- [LegacyGuideMapper.cs](LegacyGuideMapper.cs): compatibility with the obsolete
  `IContentTypeMapping` contract.

Adapt aliases, extraction rules and currencies to your site's model. The block
extractor uses HtmlAgilityPack only in this example application.

The extractor visits only selected fields, nested blocks and Block Grid areas,
removes HTML markup and script/style/template text, and respects the example's
`hide` setting. `contentRows` is seeded as Block List content; `pageGrid` is an
optional alias supported by the mapper/extractor when adapting the demo to a Grid.
An invariant outer block property can contain localized elements: the mapper reads
those elements separately for each published culture instead of reusing the first
culture's converted text. The multilingual article uses the same mapper.

Build all examples with:

```sh
dotnet build samples/Relewise.DemoStorefront/Relewise.DemoStorefront.csproj
```

No Relewise credentials are required to compile or run the Examine-backed demo.
The converters implement asynchronous contracts but perform local conversions;
they do not make external network requests.

## Acceptance testing

Opt into `Demo:EnableDiagnostics=true` locally to expose `/smoke/*` fixture mutation,
query and sanitized SDK evidence endpoints. They are disabled by default. The
application requires the Development environment and local Relewise endpoints.

Configuration switches exercise the same application:

- `Demo:Search=false`: no named Relewise providers; indexing and tracking still work.
- `Demo:DefaultSearch=false`: named profiles exist; Examine handles ordinary queries.
- `Demo:DefaultProvider=catalogue`: ordinary queries use Product search.
- `Demo:ProductPolicy=None`: preserve externally controlled administrative state.

Use the storefront and Umbraco backoffice to exercise publishing, draft edits,
unpublication, deletion and rebuilding. Check both destinations when Examine serves
ordinary queries. Diagnostics are optional local investigation tools; leave them
disabled in shared settings and keep raw reports outside tracked files.

See [entity mapping](../../docs/entity-mapping.md) and
[Umbraco search](../../docs/umbraco-19-search.md) for package behavior and limitations.
