# Advanced content mapping example

[Documentation home](../../README.md) · [Mapping guide](../../custom-content-mapping.md) · [Block editor guide](../../block-editors.md)

This is a compileable library example, not a running Umbraco site. It shows a reusable `IContentTypeMapping` that preserves selected ordinary fields and builds `searchText` from Block List and Block Grid content, including nested properties and grid areas.

## Files

- [EditorialContentMapper.cs](EditorialContentMapper.cs): standard field conversion, culture-aware aggregation, and mutation of the integration-supplied update.
- [EditorialBlockText.cs](EditorialBlockText.cs): block traversal, explicit field selection, a site-specific visibility rule, and rich-text extraction.
- [AdvancedContentMapping.csproj](AdvancedContentMapping.csproj): reproducible package references for compilation.

## Build

From the repository root, with the .NET 10 SDK installed:

```sh
dotnet build docs/examples/AdvancedContentMapping/AdvancedContentMapping.csproj
```

The example pins Relewise.Integrations.Umbraco 17.0.0 and Umbraco 17.6.2. It references the released package instead of rebuilding this repository's dashboard frontend. Building does not require API credentials, an Umbraco database, or a dataset. It does not validate live export behavior.

For another supported release, use package versions compatible with your host application and rebuild. HtmlAgilityPack is an explicit **example dependency** for HTML text extraction; it is not a new dependency added to the integration package itself.

## Adapt to your content model

1. Copy both `.cs` files into your Umbraco application and adjust their namespace if desired.
2. Add HtmlAgilityPack to your application, or replace `PlainText` with your existing HTML-to-text implementation.
3. In `EditorialContentMapper`, change `StandardFields` (`title`, `summary`, `tags`) and `BlockFields` (`contentRows`, `pageGrid`) to your page property aliases. The ordinary converter must support the editors used by the selected standard fields.
4. In `EditorialBlockText`, change `TextFields` (`heading`, `text`, `caption`) and `NestedFields` (`items`, `contentRows`, `pageGrid`) to the fields inside your blocks. Use per-element-type rules if their schemas differ.
5. Adapt `IsHidden` to your own settings. The example only recognizes a Boolean `hide` field; this is not an Umbraco-wide convention.
6. Register the mapper for your containing page document types and verify the [test cases](../../validation-and-troubleshooting.md#useful-test-cases).

## Register in your application

Add these imports and service registration to your existing `Program.cs`, before the app is built:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using Relewise.MappingExamples;

builder.Services.AddSingleton<EditorialContentMapper>();
```

Configure the mapper in your Umbraco builder. Merge this into the existing builder chain; do not create a second Umbraco builder:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise((options, services) =>
    {
        var mapper = services.GetRequiredService<EditorialContentMapper>();
        options.AddContentType("article", type => type.UseMapper(mapper));
        options.AddContentType("landingPage", type => type.UseMapper(mapper));
        options.AddContentType("simplePage", type => type.AutoMap());
    })
    .Build();
```

Keep the existing Relewise connection registration and normal Umbraco boot/middleware/endpoints. The example does not replace them. The aliases `article`, `landingPage`, and `simplePage` must match your own document types.

## Behavior and limits

- The mapper emits only selected ordinary fields plus `searchText`; the integration adds its metadata afterward.
- Each published page culture is read explicitly. For more than one culture, `searchText` is multilingual.
- Block text is emitted in a deterministic order with newline separators. Exact visual reading order can require your own layout rules.
- Hidden grid containers skip their children. Empty compositions still provide an empty `searchText` value.
- Only selected string and `IHtmlEncodedString` fields contribute text. Unknown editor values are ignored.
- Media/link/content pickers, legacy Grid Layout, and rich-text embedded-block models need explicit adapters; this example does not recursively stringify or follow them.
- No view rendering, network calls, raw storage JSON parsing, or internal converter replacement is involved.

Read the [block editor guide](../../block-editors.md) for those design choices and the [validation guide](../../validation-and-troubleshooting.md) before exporting to an existing dataset.
