# Migration

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Umbraco search](search.md)

## Upgrading to integration major 19

Integration major 19 requires **Umbraco 19 and .NET 11**. The package accepts
Umbraco versions in `[19.0.0-rc1,20)`; the demo and smoke tests use `19.0.0-rc1`.
Earlier Umbraco versions require a compatible integration release and its matching
documentation.

Back up your database before upgrading and review Umbraco's
[version-specific upgrade notes](https://docs.umbraco.com/umbraco-cms/get-started/upgrading-and-migrating/version-specific).
Update the site's framework and Umbraco dependencies together before updating the
integration. For a prerelease package, specify its published version explicitly.

### Existing mapping and tracking

Existing `AddContentType(...)`, `TrackContentViews()`, `IContentTypeMapping` and
synchronous converter registrations remain functional. Numeric node IDs and
existing `AutoMap()` output remain the defaults for existing configurations.

The legacy mapper/converter contracts are obsolete with migration guidance.
Projects that treat warnings as errors must migrate or explicitly suppress those
warnings during the transition. Use `IEntityMapper<TEntity>` and asynchronous
converters for new implementations; see [entity mapping](entity-mapping.md) and
[legacy Content mapping](custom-content-mapping.md).

When replacing `AddContentType(...)` with `MapContent(...)`, check export intent:
a bare legacy registration is tracking-only, while a bare `MapContent(...)`
registration enables export. Set `mapping.Export = false` to retain tracking-only
behavior. New entity mappers run after automatic or explicit fields and
relationships; legacy mapper instances replace application-field mapping.

Keep the existing ID strategy during the upgrade unless an identity migration is
planned. Changing IDs does not merge entities or transfer behavioral history; see
[content identity](content-identity.md#switching-an-existing-installation).

### Indexing and search

The integration uses one shared indexing pipeline for publication changes,
rebuilds and explicit exports. Relewise search-provider registration is optional.
Ordinary Umbraco searches retain their existing provider unless
`DefaultPublishedContentProviderAlias` is configured. See the
[search guide](search.md) for provider registration and query behavior.
