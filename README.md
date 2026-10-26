# Relewise.Integrations.Umbraco [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE) [![NuGet version](https://img.shields.io/nuget/v/Relewise.Integrations.Umbraco)](https://www.nuget.org/packages/Relewise.Integrations.Umbraco) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://https://github.com/Relewise/relewise-sdk-csharp-extensions/pulls)

## Documentation

Start with [the documentation overview](docs/README.md) to choose between automatic mapping, a custom content mapper, and a property converter.

- [Custom content mapping](docs/custom-content-mapping.md): the mapping pipeline, `UseMapper(...)`, registration, field ownership, and cultures.
- [Block List, Block Grid, and nested content](docs/block-editors.md): default support, traversal, rich text, visibility, and referenced content.
- [Complete mapper example](docs/examples/AdvancedContentMapping/README.md): buildable C# code you can adapt to your content model.
- [Validation and troubleshooting](docs/validation-and-troubleshooting.md): export checks, missing fields, duplicate keys, and stale data.


### Installing Relewise.Integrations.Umbraco 

First make sure to have [Umbraco installed](https://docs.umbraco.com/umbraco-cms/fundamentals/setup/install/install-umbraco-with-templates#install-the-template):
> dotnet new umbraco

Then you can install the `Relewise.Integrations.Umbraco` Package through the .NET CLI by running this command:
> dotnet add package Relewise.Integrations.Umbraco

... or from the NuGet Package Manager Console by running this command:
> Install-Package Relewise.Integrations.Umbraco

### Using Relewise.Integrations.Umbraco

Open `Program.cs` and add Relewise to the `IServiceCollection`-instance: 

```csharp
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
```

... where the above configuration, requires Relewise configuration in `appsettings.json`:

```json
"Relewise": {
  "DatasetId": "insert-dataset-id-here",
  "ApiKey": "insert-api-key-here",
  "ServerUrl": "insert-server-url-here"
}
```

Find more details about this here: https://github.com/Relewise/relewise-sdk-csharp-extensions

To integrate with Umbraco, you need to add Relewise to the UmbracoBuilder (`.AddUmbraco(...)`), and optionally specify which ContentTypes, that you would like exported into Relewise for content search and recommendations. 

In the example below we are exporting four content types into Relewise:
```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("landingPage", contentType => contentType.AutoMap())
        .AddContentType("blogList", contentType => contentType.UseMapper(new BlogMapper()))
        .AddContentType("contentPage", contentType => contentType.AutoMap())
        .AddContentType("blogEntry", contentType => contentType.AutoMap()))
    .Build();
```

`BlogMapper` above represents your own `IContentTypeMapping` implementation; it is not included in the package. See [how to write and register a mapper](docs/custom-content-mapping.md#write-a-minimal-mapper), or use the [complete Block List and Block Grid example](docs/examples/AdvancedContentMapping/README.md).

If you'd also like these content types to be automatically tracked, you can add our middleware to the UmbracoBuilder (`.UseUmbraco(...)`):
```csharp
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackContentViews();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });
```

## Content IDs across environments

Numeric Umbraco node IDs remain the default. To export stable Umbraco content keys
(GUIDs) instead, add this environment setting alongside your Relewise client settings:

```json
"Relewise": {
  "DatasetId": "insert-dataset-id-here",
  "ApiKey": "insert-api-key-here",
  "ServerUrl": "insert-server-url-here",
  "Umbraco": {
    "UseContentKeysAsIds": true
  }
}
```

Use `appsettings.Development.json` for a local override, or the environment variable
`Relewise__Umbraco__UseContentKeysAsIds=true`. Restart the application after changing
this setting. Configuration is read when the integration is initialized; it is not
an editor or per-document setting. Omitting it (or setting it to `false`) preserves
existing numeric IDs. An explicit `options.UseContentKeysAsIds = ...` in the Umbraco
`.AddRelewise(...)` callback overrides the environment setting.

With this enabled, exported content, ancestor IDs in category paths, automatic
content-view tracking, and unpublish/recycle/delete actions use the content key in
lowercase, hyphenated GUID format (for example, `1a234567-89ab-4cde-8f01-23456789abcd`).
Umbraco's internal database and published-cache lookups still use local identities.
Custom mappers receive an update initialized with the selected ID and should preserve
that ID. For references
that you export yourself, resolve `RelewiseUmbracoConfiguration` from the mapping
context and call `GetContentId(referencedContent)`; use the same convention in
custom tracking, searches, filters, and code that resolves Relewise results back to
Umbraco. For example, inside a custom mapper:

```csharp
var configuration = context.GetRequiredService<RelewiseUmbracoConfiguration>();
var referenceId = configuration.GetContentId(referencedContent);
```

Here `referencedContent` is a resolved `IPublishedContent`; add
`using Microsoft.Extensions.DependencyInjection;` for `GetRequiredService`. The setting does not rewrite arbitrary IDs inside property data, rich text,
URLs, media IDs, or application-owned integrations. Existing property converters
and custom mappers still determine which referenced values are exported.

Matching content keys let two environments update the same Relewise entity even
when their numeric node IDs differ. The content must actually share a key (for
example, preserved by your content deployment process); independently authored
copies with different GUIDs remain different entities. All writers sharing a
Dataset must agree on the ID convention. GUID identity does **not** isolate the
environments: publishing locally can overwrite shared fields, including absolute
URLs, display names, and content data. Unpublishing or deleting locally can also
disable or delete the shared entity. Use a separate development Dataset when those
side effects are undesirable.

### Switching an existing installation

Changing the setting is an identity change, not a migration. The integration does
not rename, merge, or delete existing entities when this setting changes. Subsequent
publishes create GUID-based entities while existing numeric entities and their
history remain separate; changing back has the corresponding effect. Category IDs,
custom references, saved filters, and tracking/history can also be affected.

Before switching, plan the transition for every writer and consumer, preferably
validate it in a separate Dataset, and explicitly decide how to re-export content
and handle the old entities/history. Do not assume that old numeric history will
move to the GUID entity. The dashboard's **explicit full export** retains its existing
stale-content cleanup behavior: it disables entities not carrying the current export
version, or deletes them if permanent deletion is selected. Review that operation
before using it on an existing or shared Dataset; it is not a GUID migration tool.

## Book a Demo

You can click here to [Book a Relewise demo](https://www.relewise.com/book-a-demo).

## Resources

Find more information on the [Umbraco Marketplace](https://marketplace.umbraco.com/package/relewise.integrations.umbraco).

Documentation can be found at https://docs.relewise.com.

**Please don't hesitate to reach out to us - www.relewise.com - if you'd like to know more, including how to gain access to our API.**

## Contributing

Pull requests are always welcome.  
Please fork this repository and make a PR when you are ready with your contribution.  

Otherwise you are welcome to open an Issue in our [issue tracker](https://github.com/Relewise/relewise-integrations-umbraco/issues).

## License

Relewise.Integrations.Umbraco is [MIT licensed](./LICENSE).
