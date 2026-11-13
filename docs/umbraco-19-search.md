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

The provider credential needs ContentUpdate, ContentAdministrativeAction, batched
tracking, and ContentSearchRequest permissions. Automatic page-view tracking also
needs ContentView permission on its tracker's credential. Store credentials in
server configuration or a secret store. Never expose the indexing credential in
browser code.

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

## User identity and behavioral tracking

Core searches and automatic content views both resolve `IRelewiseUserLocator` from
the current HTTP request's services. Register your site's locator before building
Umbraco, for example `builder.Services.AddScoped<IRelewiseUserLocator, MyUserLocator>();`.
Return the same temporary/authenticated identifiers and other User data that your
behavioral tracking uses. The default locator remains anonymous; the integration
does not generate a tracking ID or override your locator's consent decisions.

The provider resolves the user once per search and reuses it for the main query
and its facet requests. Index metadata and searches outside an HTTP request remain
anonymous. A scoped locator is supported by both search and content-view middleware,
so one visitor's identity is not captured in a singleton middleware or resolver.

Umbraco's `AccessContext` remains independent authorization data: it controls access
to protected content, rather than defining a Relewise user ID. An empty principal
is anonymous even if group IDs are supplied; only the explicit protection bypass
can override that rule. Identity alignment does not change the scoped provider
entity IDs or convert behavioral events for legacy exported content into events for
those separate provider entities.

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

## Sample configuration

The regular sample supports enabling the provider through ignored local settings:
set `Relewise:Umbraco:Search:Enabled` to `true`, set a unique
`Relewise:Umbraco:Search:Scope`, and optionally set `Language` (default `en`).

For contributor test setup, see the [disposable smoke host](../tests/Umbraco19.Smoke/README.md).
