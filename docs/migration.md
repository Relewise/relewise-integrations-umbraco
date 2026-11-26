# Migration

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Umbraco search](search.md)

## Upgrading to Umbraco 19

`Relewise.Integrations.Umbraco` 19.x requires **Umbraco 19 and .NET 11**.
The package accepts Umbraco versions in `[19.0.0-rc1,20)`; the demo and smoke tests use `19.0.0-rc1`.
Earlier Umbraco versions require a compatible integration release and its matching
documentation.

Back up your database before upgrading and review Umbraco's
[version-specific upgrade notes](https://docs.umbraco.com/umbraco-cms/get-started/upgrading-and-migrating/version-specific).
Update the site's framework and Umbraco dependencies together before updating the
integration. For a prerelease package, specify its published version explicitly.

### Existing mapping and tracking

Existing `AddContentType(...)`, `TrackContentViews()` and `IContentTypeMapping`
implementations remain functional. Numeric node IDs and existing `AutoMap()` output
remain the defaults for existing configurations. Synchronous converters remain
supported, but their registration must change as described below.

The legacy mapper/converter contracts are obsolete with migration guidance.
Projects that treat warnings as errors must migrate or explicitly suppress those
warnings during the transition. Use `IEntityMapper<TEntity>` and asynchronous
converters for new implementations; see [entity mapping](entity-mapping.md) and
[legacy Content mapping](custom-content-mapping.md).

When replacing `AddContentType(...)` with `MapContent(...)`, check whether the
mapping should export data. `AddContentType(...)` without `AutoMap()` or a legacy
mapper enables tracking only. `MapContent(...)` without explicit fields also enables
export. Set `mapping.Export = false` to retain tracking-only behavior. New entity mappers run after automatic or explicit fields and
relationships; legacy mapper instances replace application-field mapping.

Keep the existing ID strategy during the upgrade unless an identity migration is
planned. Changing IDs does not merge entities or transfer behavioral history; see
[content identity](content-identity.md#switching-an-existing-installation).

New mapping contracts and policies use `Relewise.Integrations.Umbraco.Mapping`;
search options, contexts and segment policies use `Relewise.Integrations.Umbraco.Search`.
Add these imports when using those types explicitly. Registration entry points and
legacy mapping/converter contracts retain their existing namespaces.

### Property value converter registration

`AddValueConverter<T>()` now requires `IAsyncRelewisePropertyValueConverter` and
registers the converter in the export operation's service scope. This is a
source-breaking change for existing synchronous converter registrations.

For an existing implementation of `IRelewisePropertyValueConverter`, change only
its registration to keep its synchronous behavior and singleton lifetime:

```csharp
// Previously: builder.Services.AddValueConverter<ExistingConverter>();
builder.Services.AddLegacyValueConverter<ExistingConverter>();
```

Both the legacy interface and `AddLegacyValueConverter<T>()` are obsolete and
will be removed in the next major version. To migrate fully, implement
`IAsyncRelewisePropertyValueConverter`, replace `void Convert(context)` with
`ValueTask ConvertAsync(context, CancellationToken cancellationToken)`, and register
with `AddValueConverter<T>()`. Synchronous work can return `ValueTask.CompletedTask`;
await asynchronous work and propagate cancellation when applicable. Keep
`CanHandle(context)` on the new implementation. Replace the legacy registration;
do not register the same converter through both methods.

Existing field-specific `UseConverter<T>()` configuration is unchanged.

### Asynchronous service method names

Use `IExportContentService.ExportAsync(...)`, `ExportAllAsync(...)` and
`IRelewiseUserLocator.GetUserAsync()`. The old `Export(...)`, `ExportAll(...)` and
`GetUser()` methods remain asynchronous but are obsolete; they will be removed in
the next major version. Awaiting and cancellation behavior are unchanged.

For a new user locator, implement only `GetUserAsync()`. New export services
implement `ExportAsync(...)` and `ExportAllAsync(...)`, alongside the unchanged
`GetTrackerOrNull()`. No obsolete forwarding methods or base class are required.
The [storefront user locator](../samples/Relewise.DemoStorefront/Integration/Tracking/StorefrontUserLocator.cs)
shows an async-only implementation.

Existing custom implementations using the old names continue to work through the
interfaces' default methods. Legacy interface calls also reach async-only
implementations. These default members are available through interface references,
such as services resolved from DI; they are not inherited concrete-class methods.
Each requested operation must implement either its async or its
legacy member; otherwise its first invocation throws an `InvalidOperationException`
identifying the implementation type and the missing operation. Partial export
implementations are checked per operation. HTTP routes are unchanged.

### Indexing and search

The integration uses one selected export system for publication changes,
rebuilds and explicit exports. Core uses notifications. Call `ConfigureSearch()`
to select Umbraco Search indexing, optionally adding named query providers with
`AddContentProvider` and `AddProductProvider` inside its callback.
Queries through Umbraco's default published-content provider retain their existing
search implementation unless `DefaultPublishedContentProviderAlias` is configured. See the
[search guide](search.md) for provider registration and query behavior.

Run a full export after upgrading to populate the source-node and search metadata on
published entities. Reconciliation only removes or disables entities with source-node
metadata and matching configured mappings. Stale entities from earlier releases that are
no longer published and lack that metadata require deliberate cleanup; the integration
does not infer their ownership from a Document Type alias alone.

Entity updates remain partial by default, including full exports. The optional
[`UpdatePolicy`](entity-mapping.md#entity-update-policy) can replace complete
Umbraco-owned entities during delta updates, rebuilds, or both. Existing explicit
Content update kinds and legacy mapper-returned update kinds retain precedence.
Keep the default policy when commerce contributes to the same entity.
