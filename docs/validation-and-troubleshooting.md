# Validation and troubleshooting

[Documentation home](README.md) · [Custom mappers](custom-content-mapping.md) · [Block editors](block-editors.md)

Validate the mapped data before diagnosing search results. A successful export request does not establish that the intended text was extracted or that the search configuration uses it.

## Validate in three stages

1. **Build and inspect locally.** Compile the mapper against the integration and Umbraco versions used by your application. Test the extraction helper with representative published models. Inspect `context.Entity.Data` in a new mapper, or `ContentUpdate.Content.Data` in a legacy mapper. Let the integration supply mapping contexts; do not construct them via reflection in application code.
2. **Exercise an actual export.** Register the mapper for the page's document type, restart the application, and publish a test page or use the Relewise dashboard's export action with a test dataset. Check the exported page ID, metadata, languages, field types, and text. Exercise both publish-triggered export and a full export.
3. **Exercise search.** Search for a distinctive phrase appearing only inside a nested block. Confirm the search configuration includes your derived field, the request language matches the exported language, and the intended page appears. Check that deliberately excluded text does not contribute through another exported field.

The [demo storefront](../samples/Relewise.DemoStorefront/README.md) provides a seeded
CMS content preview and buildable mapping, converter, search and tracking examples.
Building the sample and browsing its CMS preview require no Dataset credentials.
For connected checks, follow the [sample connection setup](../samples/Relewise.DemoStorefront/README.md#connect-to-relewise)
with a dedicated test Dataset.

## Repository regression tests

The [backend test project](../src/Integrations.Umbraco.Tests) contains tests grouped
under `Mapping`, `Indexing`, `Search`, `Dashboard`, `Tracking` and `Compatibility`.
[DemoStorefront tests](../src/Integrations.Umbraco.Tests/DemoStorefront) cover the sample's
mapping, shopping/search rules and Razor encoding. Run them from the repository root:

```powershell
dotnet test Relewise.Integrations.Umbraco.slnx
```

These isolated and in-process tests require no running Umbraco site or Relewise API.
They do not replace verification of installation, live ingestion, search and tracking
in your application's test environment with a test Dataset.

## Connection settings

The Relewise dashboard's **Connection details** section lists default and named
connections in separate tables, with one client per row. Named connections inherit
default settings unless overridden.
The default connection is used when application code requests a client without a name.
Expand **Connections used by the Umbraco integration** to see its resolved clients,
including integration-specific overrides. Credentials are omitted.
Connection names do not select Umbraco's default published-content search provider.

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

By default, publication and full exports use partial updates. A field that your mapper no longer provides can therefore remain on previously exported Content. Assigning a new dictionary locally does not itself clear every old remote field. Configure [UpdatePolicy](entity-mapping.md#entity-update-policy) when Umbraco owns the complete entity and omitted fields should be removed.

The example always supplies `searchText`, including an empty string when all relevant text disappears, so removal does not merely omit that field. Verify the resulting stored value and search behavior in your dataset.

When changing field names or data types, plan an explicit migration. With the default
update policy, a full export replaces only provided properties; **permanently delete** does not
clear omitted fields from retained entities. After successful ingestion, it deletes
missing entities identified by a different export version, restricted to configured entity types
and Document Types with source-node metadata and permitted administrative actions. A full
export without permanent deletion disables those entities instead.
Mappings with `AdministrativeActionPolicy.None()` retain their entities and business
fields. Content/Product page-availability metadata is still updated.

Review the mapped entity types, Document Types and administrative-action policies before
permanent deletion, especially in a shared Dataset.
Use a test Dataset for schema experiments. See the [export service](../src/Integrations.Umbraco/Services/ExportContentService.cs)
and [notification export](../src/Integrations.Umbraco/Indexing/NotificationExportOperations.cs)
and [Umbraco Search export](../src/Integrations.Umbraco.Search/Indexing/SearchExportOperations.cs).

## Diagnose common symptoms

| Symptom | Check |
| --- | --- |
| Mapper never runs | The published page's document type alias is registered with `UseMapper(...)`; the running app has restarted; export is actually occurring. |
| Ordinary fields disappear | New entity mappers run after declarative/automatic mapping; preserve or extend the supplied Data. Legacy `IContentTypeMapping` replaces field mapping and must explicitly convert fields it wants to retain. |
| Text Area or rich text is absent under AutoMap | Inspect the actual editor alias and registered converters. Not every converter class in the package is registered automatically. |
| Block Grid body is empty | There is no default Block Grid converter. Check the selected property alias, published value type, and traversal of areas. |
| Nested values show type names or unusable strings | Avoid generic `ToString()` on collections or editor objects; extract supported models explicitly. |
| Hidden content is exported | Default Block List conversion does not implement your settings policy. Check your mapper's own visibility rule. |
| Duplicate-key exception | A converter emits a key twice, explicit Data destinations collide, or a mapped alias uses an integration-owned metadata key. |
| Only one language is present | Inspect `CulturesToPublish`, publication status, variant settings, and whether the mapper always reads the first culture. |
| Old text remains after deploying a mapper | Deployment does not rewrite stored content. Export it again and check omitted-field/update semantics. |
| Data looks correct but search misses it | Check searchable field configuration, request language, filters, and the specific dataset/server used by the request. |
| Export fails or data does not appear | Check the integration's effective Tracker connection, mapped Document Type aliases, published state and application logs. Missing client configuration fails the export; background failures are logged. A dashboard completion message confirms export processing, not that the expected field values or search relevance are correct. |
| Referenced text becomes stale | Export the containing page after reference changes; do not assume dependent pages refresh automatically. |

## When asking for help

Include integration and Umbraco package versions, document type/property/editor aliases, the published value's CLR type, relevant mapper registration, expected output, and a small sanitized actual output. Mention whether the issue occurs on publish, full export, or search. Exclude API keys and private content from shared logs.
