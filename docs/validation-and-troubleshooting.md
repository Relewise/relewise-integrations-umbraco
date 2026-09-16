# Validation and troubleshooting

[Documentation home](README.md) · [Custom mappers](custom-content-mapping.md) · [Block editors](block-editors.md)

Validate the mapped data before diagnosing search results. A successful export request does not establish that the intended text was extracted or that the search configuration uses it.

## Validate in three stages

1. **Build and inspect locally.** Compile the mapper against the integration and Umbraco versions used by your application. Test the extraction helper with representative published models. Inspect the resulting `ContentUpdate.Content.Data` before transport; do not construct the internal `ContentMappingContext` via reflection in application code.
2. **Exercise an actual export.** Register the mapper for the page's document type, restart the application, and publish a test page or use the Relewise dashboard's export action with a test dataset. Check the exported page ID, metadata, languages, field types, and text. Exercise both publish-triggered export and a full export.
3. **Exercise search.** Search for a distinctive phrase appearing only inside a nested block. Confirm the search configuration includes your derived field, the request language matches the exported language, and the intended page appears. Check that deliberately excluded text does not contribute through another exported field.

The complete [example project](examples/AdvancedContentMapping/README.md) builds without starting Umbraco or contacting a Relewise dataset. It verifies code compatibility; you still need application-level export checks against your own document types.

## Useful test cases

| Fixture | Expected result |
| --- | --- |
| Ordinary title, summary, and tags | Preserved using registered standard converters |
| Empty or absent block property | No exception; owned aggregate text field is empty if there is no other text |
| Block List with plain and rich text | Readable text in the chosen field order |
| Single-block Block List value | Same extraction policy as an item in a list |
| Block Grid child inside an area | Child text included |
| Nested list inside a grid item, and nested grid inside a list | Selected nested content included |
| Hidden block | Block text excluded |
| Hidden grid parent with visible children | Entire subtree excluded under the example's visibility policy |
| Two published cultures | Separate text values; no accidental language mixing |
| Missing translation | Your chosen fallback or empty-value policy is followed |
| Rich text with entities, adjacent paragraphs, and script/style nodes | Readable spacing; no script/style text |
| Unknown editor or settings-only block | No raw object names, settings JSON, IDs, or unintended values |
| Removed last block | Old text no longer searchable after the corresponding update |
| Repeated execution | Stable text ordering and field types |
| Cancellation | Mapping exits when cancellation is requested |

For pure extraction tests, construct Block List / Block Grid items with fake `IPublishedElement` and `IPublishedProperty` values. For mapper integration tests, run through the integration's mapping/export services in an Umbraco test host so the real context, culture selection, and post-mapping metadata are exercised.

## Removed fields and update semantics

Publishing uses `ContentUpdate.UpdateKind.ReplaceProvidedProperties`. A regular full export also uses this update kind. A field that your new mapper no longer provides can therefore remain on previously exported content. Assigning a new dictionary locally does not itself clear every old remote field.

The example always supplies `searchText`, including an empty string when all relevant text disappears, so removal does not merely omit that field. Verify the resulting stored value and search behavior in your dataset.

When changing field names or data types, plan an explicit migration. The current full-export implementation's **permanently delete** option does two things:

- Exports retained content with `ClearAndReplace`.
- Deletes content outside that export's feed version; the ordinary option disables it instead.

Do not select this option merely as a harmless refresh. Review the dataset's contents and the export scope first, especially if the dataset is shared. Use a test dataset for schema experiments. See [export service](../src/Integrations.Umbraco/Services/ExportContentService.cs) and [publish handler](../src/Integrations.Umbraco/NotificationHandlers/RelewiseContentPublishedNotificationHandler.cs).

## Diagnose common symptoms

| Symptom | Check |
| --- | --- |
| Mapper never runs | The published page's document type alias is registered with `UseMapper(...)`; the running app has restarted; export is actually occurring. |
| Ordinary fields disappear | A mapper replaces automatic property mapping. Call `IRelewisePropertyConverter` for the fields you want to retain. |
| Text Area or rich text is absent under AutoMap | Inspect the actual editor alias and registered converters. Not every converter class in the package is registered automatically. |
| Block Grid body is empty | There is no default Block Grid converter. Check the selected property alias, published value type, and traversal of areas. |
| Nested values show type names or unusable strings | Avoid generic `ToString()` on collections or editor objects; extract supported models explicitly. |
| Hidden content is exported | Default Block List conversion does not implement your settings policy. Check your mapper's own visibility rule. |
| Duplicate-key exception | Two matching converters write the same key, two custom fields collide, or a mapped alias matches an integration-owned metadata key. |
| Only one language is present | Inspect `CulturesToPublish`, publication status, variant settings, and whether the mapper always reads the first culture. |
| Old text remains after deploying a mapper | Deployment does not rewrite stored content. Export it again and check omitted-field/update semantics. |
| Data looks correct but search misses it | Check searchable field configuration, request language, filters, and the specific dataset/server used by the request. |
| Export appears to do nothing | Check Relewise client configuration and logs. The current export service returns without tracking when it cannot obtain a tracker. |
| Referenced text becomes stale | Export the containing page after reference changes; do not assume dependent pages refresh automatically. |

## When asking for help

Include integration and Umbraco package versions, document type/property/editor aliases, the published value's CLR type, relevant mapper registration, expected output, and a small sanitized actual output. Mention whether the issue occurs on publish, full export, or search. Exclude API keys and private content from shared logs.
