# Migration

[Documentation home](README.md) · [Entity mapping](entity-mapping.md) · [Umbraco search](search.md)

## Choosing the matching integration release

Use integration 17.x with Umbraco `[17.0.0,18)`, 18.x with `[18.2.0,19)`, and 19.x with
`[19.0.0-rc1,20)`. The 17.x and 18.x builds use .NET 10; 19.x uses .NET 11.
Mapping, notification-based exports, tracking and the dashboard share the same configuration
API across these releases. Install the same major of the optional Search package.
DemoStorefront uses Umbraco `19.0.0-rc1`.

### Upgrading to Umbraco 19

Back up your database before upgrading and review Umbraco's
[version-specific upgrade notes](https://docs.umbraco.com/umbraco-cms/get-started/upgrading-and-migrating/version-specific).
Update the site's framework and Umbraco dependencies together before updating the
integration. For a prerelease package, specify its published version explicitly.

### Existing mapping and tracking

Existing `AddContentType(...)`, `TrackContentViews()` and `IContentTypeMapping`
implementations remain functional. Numeric node IDs and existing `AutoMap()` output
remain the defaults for existing configurations. Synchronous converters remain
supported through their existing registration method, as described below.

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

Existing implementations of `IRelewisePropertyValueConverter` keep their original
registration and singleton lifetime. New asynchronous converters use
`AddAsyncValueConverter<T>()` and the export operation's service scope:

```csharp
builder.Services.AddValueConverter<ExistingConverter>();
builder.Services.AddAsyncValueConverter<AsyncConverter>();
```

Both the legacy interface and `AddValueConverter<T>()` are obsolete and
will be removed in the next major version. To migrate fully, implement
`IAsyncRelewisePropertyValueConverter`, replace `void Convert(context)` with
`ValueTask ConvertAsync(context, CancellationToken token)`, and register
with `AddAsyncValueConverter<T>()`. Synchronous work can return `ValueTask.CompletedTask`;
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
The [storefront user locator](../samples/Integrations.Umbraco.DemoStorefront/Integration/Tracking/StorefrontUserLocator.cs)
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

Install `Relewise.Integrations.Umbraco.Search` to use Umbraco Search indexing or
Content/Product query providers. Mapping, tracking and notification-based exports
remain in `Relewise.Integrations.Umbraco`.

By default, publication changes trigger exports through Umbraco notifications.
Call `ConfigureSearch()` to use Umbraco Search's indexing flow instead, optionally
adding named query providers with `AddContentProvider` and `AddProductProvider`
inside its callback. Rebuilds and explicit exports use the configured indexing flow too.
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
