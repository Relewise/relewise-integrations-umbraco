# Relewise.Integrations.Umbraco [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE) [![NuGet version](https://img.shields.io/nuget/v/Relewise.Integrations.Umbraco)](https://www.nuget.org/packages/Relewise.Integrations.Umbraco) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/Relewise/relewise-integrations-umbraco/pulls)

Connect Umbraco to Relewise for search, recommendations and personalization. Export
published content, map your catalogue entities, and track page views using the same
entity and visitor identifiers throughout your integration.

## Features

- Export published content automatically and run full exports from the backoffice dashboard.
- Map Umbraco Document Types to Content, Products, Variants, Brands and Categories.
- Use automatic property conversion or customize fields with converters and entity mappers, including multilingual and block-based content.
- Track views automatically for mapped entities, with your site's visitor identity.
- Add the optional Search package to use Relewise through Umbraco's search abstraction, or consume exported data directly with a Relewise SDK.

## Installation

Install the package in your Umbraco application:

```sh
dotnet add package Relewise.Integrations.Umbraco
```

Or use the NuGet Package Manager Console:

```powershell
Install-Package Relewise.Integrations.Umbraco
```

For Umbraco Search indexing and query providers, also install the Search package:

```sh
dotnet add package Relewise.Integrations.Umbraco.Search
```

The integration package provides entity mapping, publication exports, view tracking and the
dashboard. The Search package uses the same mappings and replaces notification-based
exports when `ConfigureSearch()` is called. Installing it alone does not change indexing.

If you are starting a new site, create it using the
[Umbraco templates](https://docs.umbraco.com/umbraco-cms/get-started/installation#install-the-template).
See [compatibility and migration](#compatibility-and-migration) when choosing a package version.
The commands above select the latest stable package. For a prerelease,
specify its published version explicitly (`--version <version>` or `-Version <version>`).

## Getting started

Provide your Relewise connection settings through your application's configuration:

```json
{
  "Relewise": {
    "DatasetId": "insert-dataset-id-here",
    "ApiKey": "insert-api-key-here",
    "ServerUrl": "insert-server-url-here"
  }
}
```

Use your application's secret/configuration mechanism for the actual credentials.
See the [Relewise SDK configuration](https://github.com/Relewise/relewise-sdk-csharp-extensions)
for further connection options.

In `Program.cs`, register the SDK connection and choose the Document Types to export.
Merge this into your existing Umbraco builder chain, replacing the aliases with those
used by your site:

```csharp
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddRelewise(options => options
        .MapContent("contentPage", mapping => mapping.AutoMap())
        .MapContent("blogEntry", mapping => mapping.AutoMap()))
    .Build();
```

`AutoMap()` converts supported published properties into Relewise Data fields.
Use explicit field mappings, custom converters or an entity mapper when you need
control over the exported values. The [entity mapping guide](docs/entity-mapping.md)
also covers Products, child Variants, Brands, Categories and contributing fields to
an existing commerce catalogue.

Publication changes keep mapped entities synchronized. The Relewise backoffice
dashboard provides a full export when you need to ingest existing content or
refresh your mappings. See [validation and troubleshooting](docs/validation-and-troubleshooting.md)
for update semantics and removal behavior.

## Search and recommendations

Exported entities can be used through the Relewise SDKs for search and recommendations,
including from a separate frontend. Mapping a field does not automatically configure
its search relevance; use your Dataset's Relewise search configuration for that.

To use Relewise through Umbraco's search abstraction, add a provider inside the
`.AddRelewise(...)` callback:

```csharp
using Relewise.Integrations.Umbraco.Search;

options.ConfigureSearch(search =>
{
    search.AddContentProvider("site");
    search.DefaultPublishedContentProviderAlias = "site";
});
```

You can register Content and Product search providers with different request settings.
Indexing works independently of these providers, and you can keep your existing
Umbraco search provider while exporting data to Relewise. See the
[Umbraco search guide](docs/search.md) for registration, querying, access
control and supported operations.

## Automatic view tracking

Enable view tracking in your existing Umbraco middleware setup:

```csharp
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackEntityViews();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });
```

The mapped entity type determines the view event. For personalized searches and
recommendations, register your own `IRelewiseUserLocator` before building the application:

```csharp
builder.Services.AddScoped<IRelewiseUserLocator, MyUserLocator>();
```

`MyUserLocator` implements `GetUserAsync()`. Return the same temporary or authenticated
identifiers used by your other Relewise behavioral tracking, according to your site's
consent policy. The default locator returns `User.Anonymous()` and creates no visitor
cookies. Search and automatic views share the request-scoped locator. See
[identity and tracking](docs/search.md#identity-access-and-tracking).

## Content IDs across environments

Numeric Umbraco node IDs are the default Relewise IDs. Set
`options.UseContentKeysAsIds = true` in the integration callback to use stable Umbraco
content keys instead, or configure `Relewise:Umbraco:UseContentKeysAsIds` through your
application settings. Use `IdFromProperty(...)` when an entity should use an existing
business identifier, such as a Product SKU.

Export, relationships and automatic tracking use the same ID resolution. Changing
an ID strategy on an existing installation requires a migration plan; it does not
merge existing entities or their behavioral history. Matching GUIDs also do not
isolate environments that share a Dataset.

See [content identity and migration](docs/content-identity.md) for configuration,
custom references and guidance on switching an existing installation.

## Documentation and examples

### Try the DemoStorefront

Explore the integration in a running Umbraco site. The demo includes seeded Content,
Products, Variants, Brands and Categories, plus examples of custom mapping,
converters, multilingual search and behavioral tracking. The sample's Vue shopping UI
includes reactive filters, market-specific Offers, Similar Products, Popular Products
and Categories, Recently Viewed recommendations, an asynchronous cart and a demo checkout.

Set **Relewise.DemoStorefront** as the startup project and press **F5**. It runs with
a seeded CMS content preview out of the box. Browse the published pages and inspect
the sample code for search, facets, predictions, recommendations, export and tracking.
To use the connected shopping features, follow the
[Relewise connection setup](samples/Relewise.DemoStorefront/README.md#connect-to-relewise)
with a dedicated test Dataset.

Follow the [DemoStorefront setup guide](samples/Relewise.DemoStorefront/README.md)
for prerequisites, installation, connecting Relewise and exploring the examples.

### Guides

For implementation details, start with the [documentation overview](docs/README.md)
or go directly to:

- [Entity mapping](docs/entity-mapping.md): fields, IDs, Products, Variants, Brands, Categories and converters.
- [Block List, Block Grid and nested content](docs/block-editors.md): text extraction, visibility and cultures.
- [Umbraco search](docs/search.md): providers, queries, filtering, facets and access control.
- [Validation and troubleshooting](docs/validation-and-troubleshooting.md): checking exports and diagnosing missing or stale data.
- [Legacy Content mapping](docs/custom-content-mapping.md): existing `IContentTypeMapping` and converter implementations.

## Compatibility and migration

Choose the integration major matching your Umbraco major: **17.x**, **18.x** or **19.x**.
Versions 17 and 18 use .NET 10; version 19 uses .NET 11. They share the mapping,
export, tracking and dashboard capabilities described here. The separate Search package
and DemoStorefront currently require Umbraco 19.
Review [the migration guide](docs/migration.md) before upgrading an existing site.

Existing `AddContentType(...)`, `TrackContentViews()` and `IContentTypeMapping` remain
supported. Existing synchronous converter registrations using `AddValueConverter<T>()`
continue to work; use `AddAsyncValueConverter<T>()` for new asynchronous implementations.
The legacy mapper/converter
contracts are obsolete with migration guidance; use the entity mapping and asynchronous
converter APIs for new code. Obsolete APIs produce compiler warnings; projects that
treat warnings as errors must address or explicitly suppress them during migration.
Existing `AutoMap()` behavior and numeric ID defaults are preserved. See [entity mapping](docs/entity-mapping.md) for the corresponding APIs.

## Resources

- [Relewise documentation](https://docs.relewise.com)
- [Umbraco Marketplace](https://marketplace.umbraco.com/package/relewise.integrations.umbraco)
- [Book a Relewise demo](https://www.relewise.com/book-a-demo)

Contact [Relewise](https://www.relewise.com) to learn more or get access to the API.

## Contributing

Pull requests are welcome. Fork the repository and open a PR with your changes,
or report a problem in the [issue tracker](https://github.com/Relewise/relewise-integrations-umbraco/issues).

C# namespaces must match their folders, and using directives must follow the shared
[EditorConfig](.editorconfig) ordering. CI verifies both. Before submitting, run:

```shell
dotnet format Relewise.Integrations.Umbraco.slnx --verify-no-changes --diagnostics IDE0005 IDE0065 IDE0130 --severity info
```

## License

Relewise.Integrations.Umbraco is [MIT licensed](./LICENSE).
