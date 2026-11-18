# Advanced content mapping example

This buildable library shows the new `IEntityMapper<Content>` API. Declarative
mapping populates the entity first; the mapper then adds selected Block List/Grid
text to `Data["searchText"]`, including nested blocks and published cultures.
It preserves the mapped DisplayName, other Data and the integration's canonical ID.

## Files

- [EditorialContentMapper.cs](EditorialContentMapper.cs): asynchronous entity-level enrichment.
- [EditorialBlockText.cs](EditorialBlockText.cs): explicit field selection, nested
  blocks, visibility and HTML-to-text conversion. The runnable
  [demo storefront](../../../samples/Relewise.DemoStorefront/README.md) links this
  same source file into its field-converter example.

## Build

From the repository root, with the .NET 11 SDK and the dashboard's Node/npm prerequisites:

```sh
dotnet build docs/examples/AdvancedContentMapping/AdvancedContentMapping.csproj
```

No API credentials or Umbraco database are needed to compile the example.
HtmlAgilityPack is an example dependency, not an additional integration dependency.

## Register in your application

Copy the mapper and extractor into your application, add HtmlAgilityPack (or adapt
the extractor), and adjust the property and Document Type aliases to your schema.
Add this to your existing service registration and Umbraco builder:

```csharp
using Relewise.Integrations.Umbraco;
using Relewise.MappingExamples;

builder.Services.AddScoped<EditorialContentMapper>();

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options =>
    {
        options.MapContent("article", mapping =>
        {
            mapping.Map(entity => entity.DisplayName, "title"); // Built-in multilingual display name.
            mapping.MapData("summary"); // Data key defaults to the Umbraco property alias.
            mapping.MapData("tags"); // Uses the built-in converter for the source editor.
            mapping.UseMapper<EditorialContentMapper>(); // Runs after the declarative mappings.
        });
    })
    .Build();
```

Keep the application's existing Relewise connection registration, boot, middleware
and endpoints. `contentRows` and `pageGrid` in the mapper, and the extractor's
selected child fields and `hide` convention, are example schema choices.
Unknown values and unrelated references are not recursively stringified.

See [entity mapping](../../entity-mapping.md) for converters and scoped context,
the [legacy mapping guide](../../custom-content-mapping.md) for existing
`IContentTypeMapping` implementations, and the
[demo storefront](../../../samples/Relewise.DemoStorefront/README.md) for a running site.
