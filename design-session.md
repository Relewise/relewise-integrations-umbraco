# RFC: Umbraco integration configuration and entity mapping

- Status: **Draft for discussion — not approved for implementation**
- Date: 2026-11-16
- Target: Umbraco 19 / .NET 11

This is an intermediate design document for reviewing the configuration experience,
entity model, and architectural boundaries. The C# below is **proposed API syntax**,
not implemented or compilable examples. Names and public types remain reviewable.
The current branch implementation is not evidence that a proposal here is settled.

## Decisions requested

Review these independently, in order:

1. Compatibility, bootstrap, and the size of the public API.
2. Entity identities and hierarchy: Content, Product, Variant, Category, Brand.
3. Mapping defaults and field/entity ownership.
4. Publication, removal, dependency updates, and rebuilds.
5. Search, tracking, and headless consumption.
6. Migration and delivery gates.

The examples are the proposed customer experience. The rules following them are
equally important: accepting a method name does not implicitly approve its defaults.

## 1. Goals and boundaries

- Retain the ability to map Umbraco pages to ordinary Relewise Content entities.
- Keep working Content registrations and custom mappers usable through adapters.
- Offer a preferred foundation and a gradual obsolete/migrate/remove path.
- Support both an Umbraco-owned catalogue and editorial supplementation of an
  externally managed catalogue, without unintended replacement or deletion.
- Allow indexing, Umbraco search, and tracking to be used independently.
- Give direct Relewise consumers useful entities with readable names, URLs, images,
  and deliberately selected searchable text.
- Use the same entity identities for export, search results, tracking, and lifecycle.
- Keep the public API small and follow existing naming, code style, and XML docs.

Compatibility here concerns capabilities and existing Content integrations on
Umbraco 19. It does not restore support for earlier Umbraco/.NET versions.

Initial exclusions: dual Content/Product mapping of one document type, arbitrary
entity fan-out, arbitrary variant-to-product links, a new backoffice configuration
editor, and automatic migration/merging of existing Relewise entities or history.

## 2. Bootstrap: one configuration foundation

Keep the existing SDK client registration and Umbraco `AddRelewise` entry point.
Use typed mapping registrations within that foundation. Do not add separate
bootstrap systems for products, brands, categories, and Content.

### Example A: Content site using Umbraco search and automatic views

```csharp
// Existing SDK connection setup; credentials remain in server configuration.
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
builder.Services.AddScoped<IRelewiseUserLocator, SiteUserLocator>();

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options =>
    {
        options.Source = "editorial-production";
        options.UseContentKeysAsIds = true; // Explicit choice; existing default remains numeric.

        options.MapContent("article", article =>
        {
            article.TitleFrom("heading");
            article.SummaryFrom("summary");
            article.SearchTextFrom("bodyText");
            article.ImageFrom("heroImage");
        });

        options.MapContent("landingPage", page => page.UseMapper<LandingPageMapper>());

        options.Search.EnableUmbraco = true;
        options.Search.IndexId = "umbraco-published";
        options.Search.Language = "en";
    })
    .Build();

// Existing UseUmbraco middleware/endpoints setup, with this opt-in:
// u.UseWebsite();
// u.TrackRelewiseViews();
```

`SiteUserLocator` and `LandingPageMapper` are application implementations, not
package classes. Middleware placement remains explicit as in the existing package.

Proposed defaults and meaning:

| Choice | Rule |
| --- | --- |
| `Source` | Required for new typed profiles; stable site/environment ownership identifier. It is not a prefix automatically added to business IDs. |
| Destination | Existing integration client by default. An optional root `ClientName` selects an already configured SDK named client; it does not create another connection configuration format. |
| `MapContent` without a custom mapper | Published name, published absolute URL, and integration metadata. Other fields are deliberate selections. |
| Title | Published name by default; `TitleFrom` overrides the readable DisplayName and conventional title field. |
| `SearchTextFrom` | Selects published text for a conventional search field, not DisplayName. Complex blocks use a mapper. |
| `AutoMap()` | Available when desired, retaining existing property-converter behavior. Exported properties are not automatically all made searchable. |
| `UseMapper<T>()` | Takes over application field mapping, as current `UseMapper` does. It does not silently run after AutoMap. |
| Search | Off unless enabled. Default Umbraco index selection is published documents only. |
| Automatic tracking | Off unless the view middleware is registered. Mapped types participate by default; a profile can set `TrackViews = false`. |
| User identity | Existing `IRelewiseUserLocator`; anonymous by default. No generated identifier or implicit consent decision. |

The field conveniences above should cover common needs, not grow into a second
general-purpose mapping language. Use existing converters and a custom mapper for
complex extraction, URL selection, derived fields, or external data.

### Example B: Umbraco owns products and their variants

```csharp
.AddRelewise(options =>
{
    options.Source = "catalogue-production";

    options.MapProductCategory("productCategory", category =>
    {
        category.IdFromProperty("categoryId");
        category.TitleFrom("title");
        category.ImageFrom("image");
    });

    options.MapBrand("brand", brand =>
    {
        brand.IdFromProperty("brandId");
        brand.ImageFrom("logo");
    });

    options.MapProduct("product", product =>
    {
        product.IdFromProperty("sku");
        product.TitleFrom("title");
        product.SearchTextFrom("description");
        product.ImageFrom("image");
        product.BrandFrom("brand");
        product.AdditionalCategoriesFrom("additionalCategories");
        product.UseMapper<ProductMapper>();

        product.VariantsFromChildren("productVariant", variant =>
        {
            variant.IdFromProperty("sku");
            variant.UseMapper<VariantMapper>();
        });
    });
})
```

Here the custom mappers fill application-specific fields such as prices,
availability data, specifications, and selected custom Data keys. Declarative
identity, relationships, and configured conventional fields remain integration
responsibilities; duplicate ownership of a field is a configuration error. A mapper
can call the existing property converter service when ordinary conversion is useful.

`Own` is the default ownership mode for new typed profiles. Product identity must
be explicitly configured; missing/blank IDs are errors rather than a fallback to a
different identifier. Variant ID is unique within its parent Product, not globally.

### Example C: Content plus supplementation of an external product catalogue

```csharp
.AddRelewise(options =>
{
    options.Source = "editorial-production";

    options.MapContent("guide", guide =>
    {
        guide.TitleFrom("title");
        guide.SearchTextFrom("bodyText");
    });

    options.MapProduct("productEditorialPage", product =>
    {
        product.IdFromProperty("commerceProductId");
        product.Ownership = EntityOwnership.Supplement;
        product.DataFrom("editorialSummary", "summary");
        product.DataFrom("editorialBody", "bodyText");
        product.DataFrom("editorialImage", "image");
    });

    // No search registration and no view middleware are required for indexing.
})
```

This profile contributes only its declared fields. It does not send default
DisplayName, URL, category, brand, price, assortment, or variant updates over data
owned by the commerce platform. A supplied field can be deliberately made
searchable through the customer's Relewise index configuration.

`DataFrom(destinationKey, propertyAlias)` is the proposed small escape hatch for
simple field mapping. A custom mapper remains available, subject to the same
ownership validation. Configuration choices should make the payload explainable.

`Supplement` must not silently create partial products. Proposed behavior is to
skip a missing target and report an actionable error. **Open implementation gate:**
verify whether the backend supports an atomic update-if-existing operation. An
existence read followed by an upsert is not an atomic guarantee. Required read
permissions and any remaining race must be settled before offering this promise.

## 3. Hierarchy and entity model

One primary target is allowed per document type. Duplicate registrations across
Content/Product/etc. fail configuration validation. Parent Product assembly of
its child variants is one aggregate, not a second mapping of the Product document.

| Target | Source and identity | Relationship rules | Derived event |
| --- | --- | --- | --- |
| Content | Published page; existing numeric/GUID identity, optionally an explicit property ID in a new profile | Existing Content category paths preserved in compatibility mode | ContentView |
| Product | Product document; explicit Product ID | Closest mapped Product Category ancestor, optional additional category references and one Brand | ProductView |
| Variant | Direct child of a mapped Product; explicit Variant ID plus parent Product ID | No arbitrary parent picker or cross-tree lookup; inherits Product category/brand relationships | ProductView with Product + Variant IDs |
| Product Category | Mapped taxonomy document; explicit ID or configured content-key convention | Category ancestors form Product category paths | ProductCategoryView |
| Content Category | Explicit `MapContentCategory` profile; same configuration pattern as Product Category | Separate taxonomy from Product Categories | ContentCategoryView |
| Brand | Mapped Brand document; explicit ID or configured content-key convention | Products reference it through a single-item Umbraco picker | BrandView |

Variants initially come from child documents only. Embedded/block variants and
arbitrary source resolvers are deferred. Umbraco personalization segments are not
Product variants. These concepts must not share configuration or identities.

For Product category paths:

- Ignore organisational folders; collect mapped category ancestors.
- The closest mapped category is the primary path's leaf. No mapped category means
  no implicit category assignment.
- `AdditionalCategoriesFrom` reads a multi-item Umbraco document picker whose
  targets must be mapped Product Categories. Each target expands to a full path.
- Remove duplicate paths. Reject incompatible targets and invalid taxonomy cycles.
- References use the category mapping's ID, never a second independent ID selector.

Supplement profiles do not assign relationships unless explicitly granted ownership
of those relationships. Shared categories/brands are not removed when one referring
Product or page is removed. Brand's SDK DisplayName is a string; translated display
fields must be explicitly modelled in Data rather than assuming every entity has
the same multilingual built-in properties.

## 4. Identity, fields, and frontend consumption

Maintain one internal identity resolver used by mapping, tracking, search, and
cleanup. Persist enough source-to-target information to resolve removed or moved
documents after their published values are no longer available.

The logical entity reference includes destination, kind, ID, and parent Product ID
where relevant. Culture/segment and the Umbraco source key are additional context.
Do not expose a new public reference type unless application code genuinely needs
it; prefer SDK ID types and serializable result data where those are sufficient.

For legacy Content, preserve numeric IDs by default and the existing explicit GUID
option. Choosing an external Product/Variant/Category/Brand ID must also govern its
references, views, and administrative actions. Source isolation is achieved through
explicit destination/identity policy and ownership metadata, not silent ID changes.

New owned entities have readable business fields: DisplayName/title, URL, optional
summary, image, and selected search text. Technical indexing data is namespaced.
Do not use the whole page as DisplayName or expose an unnecessary collection of
encoded duplicate business fields to direct consumers.

Preserve separate title/summary/body and relevance-tier information for indexing.
Supply a documented example Search Index configuration and non-mutating validation.
Never rewrite the shared default Relewise index automatically. A separate setup
operation, if needed, needs its own approval and overwrite rules.

Existing Content field names, multilingual values, metadata, and converter behavior
remain available through compatibility mappings. New conventional fields do not
silently replace the payload of an existing custom Content mapper.

## 5. Ownership, updates, and lifecycle

`Own` and `Supplement` describe control, not which system happens to send a request.
An owned profile must not silently adopt a pre-existing entity controlled by another
source; adoption belongs to an explicit migration decision, not ordinary publication.
The manifest records source, destination, source document, target identity,
relationships, owned fields, and last successful generation. Keep that machinery
internal and do not store visitor identity in it.

| Operation | Owned entity | Supplemented entity |
| --- | --- | --- |
| Publish/edit | Update the configured entity and owned fields | Update only approved fields on the target |
| Remove a mapped field | Remove the previously owned value using verified SDK semantics | Remove only the contribution owned by this profile |
| Unpublish/trash | Disable the owned entity/variant, retaining its identity and history | Withdraw the editorial contribution; never disable/delete the external entity |
| Permanent delete | Delete the owned target after checking manifest ownership; legacy Content behavior remains available | Withdraw the contribution without deleting the external target |
| Remove a variant | Remove/disable that owned variant; retain Product and other variants | Withdraw only owned variant fields |
| Change an ID or entity kind | Reconcile the old manifest entry and create/update the new identity | Withdraw the old contribution before adopting the new target |
| Move a variant | Reconcile old and new parent Product aggregates | Subject to the same contribution-only boundary |
| Change category/brand/reference/Library Element | Reindex affected dependants | Refresh approved contributions on affected targets |
| Full export/rebuild | Reconcile this source's owned entities after successful enumeration | Reconcile this source's contributions only |

Never enable `replaceExistingVariants` for a Product whose full variant collection
is externally owned. In owned mode, replacing the collection is permitted only
after assembling and validating the complete current child set.

Separate omitted fields, empty values, explicit removal, and failed extraction.
Do not implement supplementation through read/modify/write of an entire remote
entity snapshot. Verify selective Data-key, language, and relationship removal
semantics against the SDK/backend before deciding the transport implementation.

Two profiles/sources claiming the same target field is a conflict to report, not
an implicit last-writer-wins policy. New mixed-catalogue cleanup is source-scoped.
Local configuration conflicts can be validated at startup. Detecting claims from
other installations requires shared ownership information; this is not a promise
of global conflict detection against arbitrary external writers. Decide that
coordination boundary alongside the manifest before implementation.
The old Content-only cleanup capability remains explicitly available through the
compatibility path; selecting new profiles never grants a global purge capability.

Mapping, publication handling, and dependency invalidation operate without an HTTP
request. Indexing-only/headless use must still react to published reusable content.
Retries, idempotency, partial failures, and cross-instance coordination need design
coverage; a process-local lock alone is not a distributed rebuild strategy.

## 6. Search and tracking configuration

### Tracking

Automatic event type is derived from entity kind, as in the table above. There is
no independent `ViewEventType` setting. Customers control participation, consent,
and user identity, not an inconsistent Content-to-ProductView combination.

`TrackRelewiseViews()` is the candidate replacement for `TrackContentViews()`.
The old method becomes an obsolete forwarding wrapper. Registration is idempotent:
using both names must not track two views. A profile can disable automatic views
while remaining indexed. An existing tracking-only Content registration still works.
Variant views occur on an actual variant page or an explicit frontend interaction;
rendering a Product with a list of variants does not emit a view for every variant.

Resolve `IRelewiseUserLocator` from request services, retaining temporary and
authenticated IDs, identifiers, classifications, and consent decisions. Do not
derive behavioral identity from Umbraco's authorization principal/group context.
Keep background index/metadata work visitor-independent.

Headless consumers must receive/document the exact target entity IDs, including
Product + Variant IDs. They can send ordinary SDK events themselves. No server
endpoint that trusts arbitrary client-supplied member/group access is proposed.

### Umbraco search adapter

The simple example enables the published document index. Advanced selection of
draft/media/member/element indexes must remain explicit and isolated from public
catalogue data. Core query capabilities currently supported by the PR must be
listed and verified individually, including access checks before paging/facets.
Explicitly decide how unmapped document types participate. Replacing the whole
published index must not silently hide pages that Umbraco would otherwise index.
An opt-in fallback Content profile is a candidate; its precedence, field selection,
and export/tracking behavior need agreement before adding another public option.

Use Content search for Content and Product search for Products. Product results
must resolve to their originating Umbraco document where used through the adapter;
externally supplied products without an Umbraco document remain headless results.
Variants need an explicit collapse-to-product or variant-page result policy so
counts and paging do not accidentally return duplicate Umbraco documents.

**Open design gate:** one canonical multilingual entity must first be proven able
to satisfy the required culture/segment, typed-filter, facet, sort, and permission
semantics. Do not silently reintroduce a separate search entity family to pass
these tests. Translating a result back to a page does not attach behavioral history
to a different entity. If a projection is unavoidable, resolve that trade-off and
its tracking/personalization implications explicitly before approval.

**Open design gate:** mixed Content/Product results need a policy for ranking,
totals, paging, facets, and duplicate source documents. Batched requests do not by
themselves establish a comparable global relevance score. Recommended first
experience for headless callers is separate result groups. Whether a mixed native
Umbraco index is supported initially must be decided, not silently approximated.

Readable entities do not make protected/draft content safe for public direct API
access. Decide destination and credential boundaries and server-side access checks
for headless use before exposing those records outside Umbraco.

## 7. Compatibility and public API budget

| Existing surface/behavior | Proposed treatment |
| --- | --- |
| SDK `AddRelewise` and `ReadFromConfiguration` | Reuse unchanged |
| Umbraco `AddRelewise` | Keep as the single integration entry point |
| `AddContentType`, `AutoMap`, instance `UseMapper`, `IContentTypeMapping`, `ContentMappingContext` | Internal compatibility adapters; obsolete only after equivalent replacements and migration examples exist |
| Numeric Content IDs / explicit GUID setting | Preserve; no automatic rewrite of identities or remote history |
| Property converters and service | Reuse; do not require rewriting converters to opt into new entity types |
| Tracking-only Content registration | Preserve; new configuration needs an equivalent without forcing export |
| `TrackContentViews` | Obsolete wrapper over generalized derived-event tracking |
| `AddRelewiseSearchProvider` introduced in this unreleased PR | Consolidate into root search options; no need to preserve every unreleased design decision |
| Current separate scoped search entities | Redesign subject to the identity/search proof; clean up test entities explicitly, not through an automatic production migration |

Obsolete APIs produce actionable warnings, not errors, with a documented removal
major. Warnings-as-errors consumers must have a clear migration path. Do not
obsolete functionality before the new foundation can express it.

Candidate public additions are limited to typed mapping builders, necessary root
options, ownership choices, a generalized view-middleware entry point, and custom
mapping contracts. Avoid public dispatcher, manifest, transport, registry,
dependency graph, generic plugin framework, or public interfaces for every class.

For custom mapping, review **one generic pair** rather than six parallel APIs:
`IEntityMapper<TUpdate>` and `EntityMappingContext<TUpdate>`, with SDK update types
such as `ContentUpdate` and `ProductUpdate`. Existing Content mappers adapt to it.
This is a proposal, not an instruction to add both old and new abstractions forever.
Variant mappers may require a Product/Variant context instead; settle that small
exception before finalizing the generic contract.

Resolve new mapper types per mapping operation in a DI scope. Legacy instance
mappers retain their documented shared/stateless requirements. Never resolve a
scoped mapper into singleton configuration or share its page/request state.
Mapping runs on published data, accepts cancellation, and does not depend on the
current visitor. No mutable SDK-update identity/lifecycle fields may bypass the
configured identity and ownership rules.

Use existing code style and XML documentation conventions. Each public addition
needs a concrete usage example and justification; internals do not become public
merely to simplify tests.

## 8. Validation and delivery pieces

1. Agree compatibility matrix, bootstrap examples, and public API budget.
2. Prove canonical identity + core search semantics, including mixed-result and
   variant-collapse decisions. Validate selective updates and missing-target policy.
3. Build the internal foundation/manifest and adapters; snapshot existing Content
   payloads, IDs, converter outputs, tracking, and lifecycle before refactoring.
4. Deliver owned Content plus readable field/index setup and headless consumption.
5. Deliver owned Product/child Variant/Category/Brand relationships and lifecycle.
6. Deliver supplementation after field-removal, conflict, and variant-ownership
   rules are proven. No destructive catalogue cleanup as a shortcut.
7. Validate fresh SQLite sites, existing Content configuration, shared catalogues,
   headless SDK consumers, cultures/segments, protected content, reference changes,
   moves/removal, retries/rebuilds, concurrent requests and multiple app instances.

Phase boundaries do not permit declaring full compatibility while silently
dropping an existing capability. Smaller prereleases must state their supported
matrix and keep unfinished typed features unavailable rather than misleading.

User documentation belongs in README-linked guides: bootstrap, mapping examples,
identity, ownership, search-index setup, lifecycle, and migration. Repeatable test
instructions belong beside tests. Dataset details, credentials, captures, and
one-off execution evidence stay outside the repository and in review work items.
Remove or replace this intermediate RFC once decisions are reflected in durable
documentation; it is not a second set of supported product instructions.

## Review questions

- Is the proposed typed configuration clearer than extending `AddContentType` with
  a target selector? Which conveniences should be removed from the public API?
- Do we agree on adapters plus staged obsolescence, including tracking-only use?
- Are direct-child variants and ancestor categories sufficient, with an optional
  additional-category picker and single Brand picker?
- Should Supplement be exposed in the first typed-mapping release, or follow the
  owned-entity release after its backend guarantees have been validated?
- What native Umbraco behavior is required for Product variants and mixed targets?
  Is separate headless result grouping an acceptable initial experience?
- How should the native index cover document types without an explicit profile,
  while keeping ordinary business exports and tracking opt-in?
- Can we prove shared entity identity across multilingual/segmented core search,
  ordinary SDK search, and tracking without separate behavioral histories?

## References

- [Current integration setup](README.md)
- [Existing custom mapping contract and examples](docs/custom-content-mapping.md)
- [Existing block/converter behavior and extension points](docs/block-editors.md)
- [Current branch's search provider guide](docs/umbraco-19-search.md)
- [Relewise .NET entity integration and variant update semantics](https://docs.relewise.com/docs/developer/sdks/dotnet/product-update/entity-integration.html)
- [Relewise behavioral user identity](https://docs.relewise.com/docs/tracking/user.html)

Current guides describe existing behavior. They are references for compatibility,
not documentation of the proposed methods in this RFC.
