# Umbraco 19 and core search

Version 19 of this package requires **Umbraco 19** and **.NET 11**. It does not support
earlier Umbraco majors. The initial target is `19.0.0-rc1`; use this prerelease in a
test environment until Umbraco 19 and this package are released.

Back up your database before upgrading. Umbraco's database upgrade cannot be undone
by installing an older package. Resolve obsolete API usage and review Umbraco's
[19 upgrade notes](https://docs.umbraco.com/umbraco-cms/fundamentals/setup/upgrading/version-specific).
Backoffice API requests now use the session cookie. The generated dashboard client
uses `credentials: 'include'` and no longer requests bearer tokens.

## Register the core search provider

The existing `.AddRelewise(...)` content-export API remains available. Core search
is a separate, opt-in indexing pipeline, driven by Umbraco's own index fields and
publication lifecycle. It does not depend on `AutoMap()` or custom export mappers.

Configure your Relewise clients as described in the main README, then add:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddRelewise()
    .AddRelewiseSearchProvider(options =>
    {
        options.Scope = "my-site-production";
        options.Language = "en";
    })
    .Build();
```

`Scope` is required and must be unique per site/environment sharing a Dataset.
Keep it stable: changing it creates a new set of remote entities. Reset/delete
operations are restricted to the configured scope and Umbraco index alias.

Prefer a **dedicated Dataset** and a separately configured named client. Set
`options.ClientName` to that named client. Provider entities have scoped IDs and
contain one document per culture/segment; they are distinct from the ordinary
content exports used by your own Relewise searches and tracking. Ordinary Relewise
queries against a shared Dataset must exclude provider entities using the
`rwUmbracoSearchOwner` Data Key. The dashboard full-export cleanup excludes them.

The Dataset must have an enabled default Search Index, with `options.Language`
enabled and the Content `DisplayName` field enabled. An entirely empty Dataset may
not yet have an index. The provider stores its combined searchable text in that
field. `Language` controls Relewise language processing; Umbraco culture filtering
is independent. Choose the language appropriate to your site's text. A multilingual
site needing different linguistic processing per culture should use separate
configured provider instances/Datasets rather than treating the language setting
as automatic culture-to-language mapping.

The credential needs ContentUpdate, ContentAdministrativeAction, batched tracking,
and ContentSearchRequest permissions. Store it in server configuration or a secret
store. Never expose the indexing credential in browser code.

## Index selection and querying

Published content is replaced by default. Draft content, media, members and Library
Elements continue to use the built-in provider until explicitly selected:

```csharp
options.IndexAliases.Add(Umbraco.Cms.Core.Constants.Search.IndexAliases.DraftContent);
options.IndexAliases.Add(Umbraco.Cms.Core.Constants.Search.IndexAliases.DraftMedia);
options.IndexAliases.Add(Umbraco.Cms.Core.Constants.Search.IndexAliases.DraftMembers);
options.IndexAliases.Add(Umbraco.Cms.Core.Constants.Search.IndexAliases.DraftElements);
```

Enable those only when the remote Dataset is appropriate for draft/member data.
Registration runs after the default index registrations, so composer ordering does
not silently restore Examine for the selected aliases. Use Umbraco's resolver:

```csharp
var alias = Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;
var searcher = searcherResolver.GetSearcher(alias)
    ?? throw new InvalidOperationException("Search index is not registered");
var results = await searcher.SearchAsync(alias, "solar panels", culture: "en-us");
```

`searcherResolver` is an injected `Umbraco.Cms.Core.Search.ISearcherResolver`.
Results contain Umbraco GUIDs, regardless of the ordinary export's numeric/GUID ID
setting. Rebuild the selected indexes using Umbraco's Search management screen after
enabling the provider. Existing Examine entries are not imported automatically.

## Behavior and limits

- Culture and segment selection, invariant content, member/group access and explicit
  protection bypass are applied before pagination and facet counting.
- Keyword/text filters, integer/decimal/date exact and range filters, sorting,
  exact facets and range facets are translated. Unsupported custom types throw an
  exception rather than dropping a constraint.
- Relewise supplies relevance/NLP, so full-text behavior differs from Lucene. All
  textual relevance tiers are combined; the provider does not preserve Examine's
  separate R1/R2/R3 weights. Text filters use Relewise substring conditions.
- Numeric comparisons use Relewise doubles; very high precision decimal values can
  lose precision. Dates use UTC Unix milliseconds; sub-millisecond distinctions are
  not retained. Do not use those fields for requirements needing exact precision
  beyond that representation.
- A range includes its lower bound and excludes its upper bound. Numeric collections
  are stored as Data Objects, ensuring a single value must satisfy both bounds.
- Facets omit filters on their own field. Range facets make one count request per
  bucket; exact facets use Relewise string facets. Consider request volume for many
  facets/buckets. Query suggestions are currently empty (optional in the core API).
- Relewise indexes asynchronously. A successful indexing request does not imply the
  updated text is immediately searchable. Index management reports remote searchable
  variation counts, which can exceed the number of Umbraco documents.
- Rebuild currently clears the scoped remote index before repopulating it. This
  provider does not implement Examine's shadow-index/zero-downtime rebuild feature.
- Writes for the same scope/index are serialized within a process. Avoid multiple
  independent sites writing the same scope; this is not a distributed transaction.

Umbraco's own core indexing handles referenced Library Element changes for the
provider. The legacy custom content-export pipeline still follows document
publication notifications; it does not independently synchronize referencing pages
when only a Library Element is published. Use core search for that scenario or
explicitly republish/export the affected documents.

## Local validation

The regular sample also supports enabling the provider through ignored local settings:
set `Relewise:Umbraco:Search:Enabled` to `true`, set a unique
`Relewise:Umbraco:Search:Scope`, and optionally set `Language` (default `en`).

Build/test the solution, then pack the integration and restore the
`tests/Umbraco19.Smoke` test host from that local NuGet feed. This host intentionally
uses a PackageReference. Run it from a disposable directory, with a fresh SQLite
database and a `local.settings.json`; bind it to loopback only. Its `/smoke/*` routes
are test operations and **must never be deployed**.

`tests/Umbraco19.Smoke/Initialize-SmokeSite.ps1` creates that disposable directory.
Pass `-CredentialFile` with an existing local settings file containing a `Relewise`
section to run against a dedicated live Dataset. Without it, the host uses a dummy
credential and the optional local simulation. The generated local settings also
contain the unattended backoffice user's password; keep them outside source control.

`/smoke/seed` creates and publishes a real page. `/smoke/provider` exercises the core
resolver, indexing, searching and metadata. `/smoke/verify` runs live checks for
protection, cultures, segments, ranges, sorting, facets, paging and scoped cleanup.
Use a dedicated disposable Dataset: the verification performs real writes/deletes.
Revoke its temporary credential when testing is complete.

The optional Postman simulation in `postman/simulations/umbraco19.sim.yaml` checks
SDK HTTP wiring against a local fixture, using `Smoke:UseJson=true` and a dummy key.
It is not evidence for live ranking or backend indexing behavior. Its context file
records the boundaries.
