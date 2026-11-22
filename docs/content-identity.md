# Content IDs across environments

[Documentation home](README.md) · [Entity mapping](entity-mapping.md)

Numeric Umbraco node IDs remain the default. To export stable Umbraco content keys
(GUIDs) instead, add this environment setting alongside your Relewise client settings:

```json
{
  "Relewise": {
    "DatasetId": "insert-dataset-id-here",
    "ApiKey": "insert-api-key-here",
    "ServerUrl": "insert-server-url-here",
    "Umbraco": {
      "UseContentKeysAsIds": true
    }
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
An explicit `IdFromProperty(...)` mapping takes precedence for that entity type.
Umbraco's internal database and published-cache lookups still use local identities.
Custom mappers receive an entity initialized with the selected ID and should preserve
that ID. Legacy Content mappers receive it inside their update. For references
that you export yourself, resolve `RelewiseUmbracoConfiguration` from the mapping
context and call `GetContentId(referencedContent)`; use the same convention in
custom tracking, searches, filters, and code that resolves Relewise results back to
Umbraco. For example, inside a custom mapper:

```csharp
var configuration = context.Services.GetRequiredService<RelewiseUmbracoConfiguration>();
var referenceId = configuration.GetContentId(referencedContent);
```

This example uses `IEntityMapper<TEntity>`. Legacy `ContentMappingContext` implements
`IServiceProvider` directly, so use `context.GetRequiredService<RelewiseUmbracoConfiguration>()` there.

Here `referencedContent` is a resolved `IPublishedContent`; add
`using Microsoft.Extensions.DependencyInjection;` for `GetRequiredService`.
The setting does not rewrite arbitrary IDs inside property data, rich text,
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

## Switching an existing installation

Changing the setting is an identity change, not a migration. Configuration alone
does not rename or merge entities, transfer history or perform cleanup. Subsequent
publishes create GUID-based entities while existing numeric entities and their
history remain separate until a lifecycle or reconciliation action affects them.
Changing back has the corresponding effect. Category IDs,
custom references, saved filters, and tracking/history can also be affected.

Before switching, plan the transition for every writer and consumer, preferably
validate it in a separate Dataset, and explicitly decide how to re-export content
and handle the old entities/history. Do not assume that old numeric history will
move to the GUID entity. The dashboard's **explicit full export** retains its existing
stale-entity cleanup: within the configured source scope, it disables entities not
carrying the completed export version, or deletes them if permanent deletion is
selected. Review that operation before using it on an existing or shared Dataset;
it is not a GUID migration tool.
