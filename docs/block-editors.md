# Mapping block editors

[Documentation home](README.md) · [Custom mappers](custom-content-mapping.md) · [Complete example](examples/AdvancedContentMapping/README.md)

A page builder stores an editorial composition: text, links, images, nested blocks, layout, and settings. Search normally needs a deliberate subset of that composition. Export the content a reader should find, with a stable field structure, instead of serializing the editor's storage model.

## Contents

- [What the integration handles by default](#what-the-integration-handles-by-default)
- [Block List](#block-list)
- [Block Grid and areas](#block-grid-and-areas)
- [Select text intentionally](#select-text-intentionally)
- [Rich text and embedded blocks](#rich-text-and-embedded-blocks)
- [Links, media, and referenced content](#links-media-and-referenced-content)
- [Legacy Grid Layout and custom editors](#legacy-grid-layout-and-custom-editors)
- [Expected output](#expected-output)

## What the integration handles by default

The current `AddRelewise` registration includes converters for:

| Editor alias | Default behavior |
| --- | --- |
| `Umbraco.TextBox` | String |
| `Umbraco.TrueFalse` | Boolean |
| `Umbraco.Integer` | Integer |
| `Umbraco.Decimal` | Number |
| `Umbraco.Tags` | String list |
| `Umbraco.NestedContent` | Convert element properties, then collect string representations |
| `Umbraco.BlockList` | Convert block content properties, then collect string representations |

Source: [default registrations](../src/Integrations.Umbraco/UmbracoBuilderExtensions.cs).

The repository also includes public `TextAreaPropertyValueConverter`, `RichTextEditorPropertyValueConverter`, `MediaPickerValueConverter`, and `ImageCropperValueConverter` classes, but they are **not registered by default**. If their supported aliases and value types match your site, register the converters you need:

```csharp
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.PropertyValueConverters;

builder.Services.AddValueConverter<TextAreaPropertyValueConverter>();
```

Do not infer support from a class name alone. For example, the included rich-text converter checks `Umbraco.TinyMCE` and does not implement general modern rich-text or embedded-block extraction. The complete example reads its chosen block fields itself and does not depend on that converter.

There is **no default Block Grid converter** in these registrations. Block Grid and the older Grid Layout editor are also different editors; neither gains support merely because Block List is supported.

## Block List

The built-in converter reads a `BlockListModel`, loops through each item's `Content.Properties`, and delegates to the property converter service. It then collects the resulting values using `Value.ToString()` into a string-list `DataValue` under the original property alias.

This is useful for simple supported values, but it is not a general recursive plain-text renderer:

- It does not export the original block structure or preserve child property names.
- It does not apply your site's visibility rules from block settings.
- Nested lists and complex converted values are stringified at the parent boundary; do not assume nested collections become useful prose.
- It only gets values from converters that match the child editors.
- It expects `BlockListModel`; inspect the value type if single-block mode returns a `BlockListItem`.

Source: [Block List converter](../src/Integrations.Umbraco/PropertyValueConverters/BlockListPropertyValueConverter.cs).

For editorial control, use a content mapper and read the Block List's published value. The [example extractor](examples/AdvancedContentMapping/EditorialBlockText.cs) explicitly handles both `BlockListModel` and `BlockListItem`, visits selected child fields, and recursively visits selected nested block properties.

## Block Grid and areas

Block Grid adds **areas**, which are collections of child blocks inside a block. Reading only a grid item's `Content.Properties` misses content inside those areas.

A complete traversal needs both paths:

```text
pageGrid
└── grid item
    ├── selected content fields
    ├── selected nested Block List / Block Grid properties
    └── areas
        └── child grid items
            └── their fields and areas
```

In the Umbraco published model, each `BlockGridArea` is enumerable. The core traversal in the example is:

```csharp
// Inside the example's VisitGridItem method:
VisitElement(item.Content, culture, fragments, token);
foreach (var area in item.Areas)
    foreach (var child in area)
        VisitGridItem(child, culture, fragments, token);
```

The complete method checks visibility before both the parent's text and its children. It preserves model order: selected parent fields first, then configured nested properties, then area children. This is deterministic but not necessarily the exact visual reading order of your Razor views. If layout-specific reading order matters, add rules per element type or area alias.

Row spans, column spans, area aliases, and layout settings should usually stay out of the searchable prose. See [Umbraco's Block Grid documentation](https://docs.umbraco.com/umbraco-cms/fundamentals/backoffice/property-editors/built-in-umbraco-property-editors/block-editor/block-grid-editor) for the editing and rendering model.

## Select text intentionally

The example assumes this illustrative schema:

| Location | Alias | Example value |
| --- | --- | --- |
| Page | `contentRows` | Block List |
| Page | `pageGrid` | Block Grid |
| Block content | `heading`, `text`, `caption` | Plain text or rich text |
| Block content | `items`, `contentRows`, `pageGrid` | Nested blocks |
| Block settings | `hide` | Boolean |

**These aliases are application choices, not required Umbraco or Relewise conventions.** Replace the lists in the example to match your document and element types. For a complex site, switch on `element.ContentType.Alias` and give each type its own selected fields. This avoids treating every block as if it shared the same schema.

The example's `hide` rule is likewise an application rule: a Boolean `true` skips the block and, for a grid container, its entire subtree. It does not interpret scheduling, member access, device visibility, or other custom settings. Make the export agree with the meaning of those settings in your site. A block hidden with CSS is not automatically hidden from an export.

Unknown values are intentionally ignored rather than converted with arbitrary `ToString()`. Add explicit handling when a new editor contributes meaningful content. This prevents IDs, JSON metadata, type names, booleans, or layout options from becoming accidental search terms.

## Rich text and embedded blocks

The example handles `IHtmlEncodedString` through HTML parsing and treats ordinary strings as plain text. It uses HtmlAgilityPack to remove script, style, and template elements, collect text nodes with separators, decode HTML entities, and normalize whitespace.

This is **text extraction**, not an HTML rendering or sanitization service. It does not infer CSS visibility or reproduce the browser's full layout. It can place a space between adjacent inline text nodes. If your editor returns HTML as a plain `string`, explicitly pass that known field through your HTML extraction function instead of assuming all strings are HTML.

Modern rich-text editors can have their own value types and embedded blocks. The sample does not claim to render or extract all those models. Inspect the published value returned by your version of the editor, then add an adapter that extracts its text and visits its embedded blocks. Test it separately. Do not call a rich-text object's `ToString()` and assume it contains all child content.

## Links, media, and referenced content

The example does not follow pickers automatically. Decide what each field should mean:

| Value | Useful mapping | Avoid |
| --- | --- | --- |
| Link picker | Link label in text; URL in a separate field if needed | Target metadata or URLs mixed into body prose |
| Media picker | Caption or alt text in prose; resolved image URL separately | Serializing the media object, crop configuration, or picker identifier |
| Content picker | Referenced page's name, or selected published fields | Recursively traversing every referenced page |
| Reusable block/content reference | Explicit content projection | Unbounded traversal or duplicate text |

If you follow references, use a visited-key set or another bounded traversal policy to avoid cycles. Also plan refresh behavior: publishing the referenced item does not necessarily republish every page that includes it. Verify the parent export is refreshed when referenced text changes.

Treat document types containing private or member-only material deliberately. A mapper operates on published content; publication alone does not mean every property belongs in a public search result. Select only fields appropriate for the destination dataset and your search access rules.

## Legacy Grid Layout and custom editors

A legacy Grid Layout property is not a `BlockGridModel`. The example walker ignores it. For older integrations using that editor, write a separate adapter for its published model or documented schema: visit sections, rows, areas, and controls, and extract only known text-bearing controls. Do not paste the Block Grid traversal over raw legacy grid JSON.

Use the same approach for custom editors: first establish their published return type, then define a small projection into your chosen Relewise fields. If the semantics are identical across all pages, a [property converter](custom-content-mapping.md#when-a-property-converter-is-appropriate) may be the appropriate reusable extension point.

## Expected output

For an English page with:

- `title`: “Installation guide”
- a visible list block with heading “Before you start” and text `<p>Check your connection.</p>`
- a hidden list block with text “Internal draft”
- a grid area containing heading “Next steps”

…the example's application-owned data is conceptually:

```json
{
  "title": "Installation guide",
  "searchText": "Before you start\nCheck your connection.\nNext steps"
}
```

This is a readable representation of the field values, not a literal Relewise API request. The actual update uses typed `DataValue` objects and includes integration metadata. Multi-language pages use a multilingual value for `searchText`.

Next: [build and adapt the example](examples/AdvancedContentMapping/README.md), then [validate the export](validation-and-troubleshooting.md).
