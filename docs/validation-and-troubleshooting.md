# Validation and troubleshooting

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Block editors](block-editors.md)

Start by checking what was exported to Relewise, then check how it is searched or tracked.
An export can complete successfully even when the mapping does not produce the values you intended.

## Verify your integration

1. **Check configuration.** Confirm that the intended Document Types are mapped, export is enabled, and the application has restarted after configuration changes. Review the dashboard's mapping details and effective connection.
2. **Publish and export test data.** Use a dedicated test Dataset. Publish a representative page or Product and inspect its ID, languages, built-in properties and custom Data in My Relewise. Run a full export from the dashboard as well as testing publication changes.
3. **Check the user experience.** Search for a distinctive mapped value and verify the request language, currency and filters. For view tracking, confirm that the tracked entity ID matches the exported entity and that search and tracking use the same visitor identity. See [search and identity](search.md#identity-access-and-tracking).

Include multiple published cultures, empty values, removed fields and publish/unpublish changes
in your checks. If you use custom block extraction, verify nested content and your visibility
rules using the [block-editor guide](block-editors.md).

The [DemoStorefront](../samples/Integrations.Umbraco.DemoStorefront/README.md) provides a seeded CMS preview
and mapping, converter, search and tracking examples. Follow its
[connection setup](../samples/Integrations.Umbraco.DemoStorefront/README.md#connect-to-relewise) to exercise
Relewise features with a dedicated test Dataset.

## Connection settings

The dashboard's **Connection details** section lists default and named connections.
Application code uses the default connection unless it requests a client by name.
Named connections inherit default settings unless overridden.

Expand **Connections used by the Umbraco integration** to check the effective settings
used for exports, views and search. Credentials are omitted. Connection names do not
select Umbraco's default published-content search provider; see [provider configuration](search.md#registration).

## Removed fields and update semantics

By default, publication changes and full exports use partial updates. Values omitted by
a mapper can therefore remain on previously exported entities. Exporting again does not
remove those values. Configure [UpdatePolicy](entity-mapping.md#entity-update-policy) when
Umbraco owns the complete entity and omitted fields should be removed.

For a derived text field, explicitly supplying an empty value can clear old text when
its source is removed. Verify the stored value and search behavior after the update.
Changing a field's name or data type may require a data migration; test it in a dedicated Dataset.

A full export disables previously exported entities that are no longer published when
the mapping permits Umbraco to manage their availability. The dashboard's permanent-deletion
option deletes missing entities instead; it does not clear omitted fields from retained entities.
Review the configured entity types, Document Types and
[administrative-action policies](entity-mapping.md#contributing-to-an-existing-catalogue) before using it,
especially in a shared Dataset.

Mappings using `AdministrativeActionPolicy.None()` preserve the entity's enabled/disabled state
and existing business fields omitted from updates. Content/Product page-availability metadata
is still maintained. See [entity mapping](entity-mapping.md) for combined commerce configurations.

## Diagnose common symptoms

| Symptom | Check |
| --- | --- |
| Mapper never runs | Check the Document Type alias, `UseMapper(...)` registration, export setting and published state. Restart after changing registration. |
| Ordinary fields disappear | New entity mappers run after automatic or explicit mapping. Preserve fields you want to retain. Legacy `IContentTypeMapping` replaces field mapping and must supply those fields itself. |
| A property is absent under AutoMap | Check its editor alias and registered converters. Register a suitable converter if the editor is not handled. |
| Block Grid text is absent | There is no default Block Grid converter. Follow the [block-editor guide](block-editors.md) to extract its content. |
| Nested values contain type names or unusable text | Extract supported block models explicitly rather than using `ToString()` on editor objects or collections. |
| Hidden block text is exported | Default Block List conversion does not apply custom visibility settings. Apply your visibility rule in the mapper or converter. |
| Duplicate-key exception | Check for converters emitting the same key, conflicting Data destinations, or use of reserved metadata keys. |
| A language is missing | Check the node's and its ancestors' published cultures, Variant configuration, and the cultures read by custom mapping. |
| Old values remain after deploying a mapper | Run an export and check [update semantics](#removed-fields-and-update-semantics). Deployment alone does not update stored data. |
| Data looks correct but search misses it | Check searchable fields, request language, filters and the Dataset/server used by the search provider. |
| Export fails or data does not appear | Check the effective Tracker connection, credentials, mapped Document Types, published state and application logs. Background export failures are logged. |
| Views are missing or attributed to a different entity | Check tracking middleware, mapping/view settings, canonical entity IDs and the visitor identity. See [entity mapping and tracking](entity-mapping.md). |
| Referenced text becomes stale | Export the containing page after reference changes; dependent pages are not always refreshed automatically. |

## When asking for help

Include the integration and Umbraco versions, relevant registration, Document Type and
property/editor aliases, expected output and a small sanitized example of the actual output.
For custom converters, include the published value's CLR type. State whether the issue
occurs on publish, full export, search or tracking. Exclude API keys and private content from logs.
