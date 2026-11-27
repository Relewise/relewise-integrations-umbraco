# Contributing

Follow [AGENTS.md](AGENTS.md) for code style, public API compatibility and delivery checks.
Package-user troubleshooting belongs in the [public guide](docs/validation-and-troubleshooting.md);
repository build, test and packaging instructions belong here.

## Build and test

From the repository root, run the solution's backend tests:

```sh
dotnet test Relewise.Integrations.Umbraco.slnx
```

The default build targets Umbraco 19, including Search and DemoStorefront. Core and Search each share
one source project with separate build profiles for each supported CMS major:

```sh
dotnet test src/Integrations.Umbraco.Tests/Integrations.Umbraco.Tests.csproj -c Release -p:UmbracoMajorVersion=17
dotnet test src/Integrations.Umbraco.Tests/Integrations.Umbraco.Tests.csproj -c Release -p:UmbracoMajorVersion=18
```

These profiles compile against the matching CMS APIs and isolate their outputs. Use the same
`UmbracoMajorVersion` property when packing the Core and Search projects.
DemoStorefront requires the default v19 profile.

The [package test project](src/Integrations.Umbraco.Tests) groups Core and Search tests by mapping,
indexing, search, dashboard, tracking and compatibility, independently of the sample.
The [DemoStorefront test project](src/Integrations.Umbraco.DemoStorefront.Tests) covers sample mapping,
shopping/search rules and Razor encoding. Run it separately with:

```sh
dotnet test src/Integrations.Umbraco.DemoStorefront.Tests/Integrations.Umbraco.DemoStorefront.Tests.csproj
```

Both projects run with the default solution command above. These tests need no running
Umbraco site or Relewise API. They do not replace installed-package and live integration checks.

## Mapping regression coverage

Exercise extraction helpers with representative published models. Block List/Block Grid tests
can use fake `IPublishedElement` and `IPublishedProperty` values. Exercise mapper/export services
in an Umbraco test host for real context creation, culture selection and metadata behavior;
do not construct mapping contexts through reflection.

Cover these cases when changing mapping or converters:

- Ordinary fields, empty/missing values, unknown editors and stable output types/order.
- Block List single/list values, Block Grid areas, and nested lists/grids.
- Hidden blocks and hidden parents with otherwise visible children.
- Published cultures, missing translations and ancestor publication changes.
- Rich-text spacing/entities and exclusion of script/style text, settings and raw object values.
- Removal of the last source value, delta/full update policies and cancellation.

Inspect exported IDs, metadata, languages, field types and values in a dedicated test Dataset.
Verify installation, delta/full exports, search and tracking against the built package.
Use the maintained [DemoStorefront](samples/Integrations.Umbraco.DemoStorefront/README.md) for UI examples
and browser checks; do not add a separate frontend test suite.

## Code style and documentation

C# namespaces must match their folders, and using directives must follow the shared
[EditorConfig](.editorconfig) ordering. CI verifies both. Before submitting, run:

```sh
dotnet format Relewise.Integrations.Umbraco.slnx --verify-no-changes --diagnostics IDE0005 IDE0065 IDE0130 --severity info
```

Keep documentation changes alongside their implementation. Use relative Markdown links,
keep examples consistent with the implementation, and build the sample when changing
mapping examples. GitHub renders the guides directly; no documentation deployment is required.
