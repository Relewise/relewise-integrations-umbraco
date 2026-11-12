# Umbraco 19 browser lifecycle smoke

Run on 12 November 2026 against Umbraco 19.0.0-rc1 / .NET 11 and the dedicated
`BHK - Umbraco v19` Dataset (`9d03c867-c7f7-46f8-acbb-8be8a5ab1007`).

The disposable SQLite host consumed the packed integration through a NuGet
PackageReference, not a ProjectReference. This run installed the current provider
code from commit `10af1a8` as local package `19.0.0-rc1.local7`. The earlier fresh-site
installation, cookie-authenticated dashboard and full-export checks remain recorded
in PR #60. The visitor routes and body-property setup are fixture-only additions.

Content was created, saved, published, edited, unpublished, republished and trashed
through Chrome's Umbraco backoffice. The fixture scaffold supplied the document
type and Textarea property. No `/smoke/provider` call or direct provider indexing
call was used for this document. Its key was
`3e16bc47-db4a-46f0-abda-d4cca658586b`.

| Check | Observed result |
| --- | --- |
| Save new draft `E2eorchard original` | Published search returned 0 matches. |
| Publish from the backoffice | Automatic ingestion produced 1 visitor-search result; clicking it opened the published page. |
| Publish body `E2eblossom body content from the Umbraco backoffice.` | Published page rendered the body; body search returned 1 result. The native collected index fields and remote stored document both contained the body. |
| Save unpublished title/body changes | Published page retained its original title/body. Draft-title search returned 0. The remote document retained the published values. |
| Republish as `E2eharvest revised` with body `E2enectar revised body content awaiting publication.` | Published page rendered the new values. Queries `harvest`, `revised`, and `nectar` each returned 1; `orchard`, `original`, and `blossom` each returned 0. |
| Unpublish through backoffice | Visitor page returned HTTP 404; remote search returned 0. |
| Republish the same document | Remote search returned 1 again. |
| Trash the published document | Visitor page returned HTTP 404; remote search returned 0. The separate `Alpha smoke` control page remained searchable. |
| Rebuild `Umb_PublishedContent` from Settings → Search | Umbraco queued and started the rebuild; management reported Healthy with 1 document afterwards. Control search returned 1, and the trashed document remained absent. |

Visitor search requests were also repeated using HTTP without a backoffice cookie.
The page displays raw remote totals and returned IDs instead of silently removing
results missing from the published cache, avoiding a false pass on removal checks.

The Dataset's asynchronous search refresh delayed text changes. Initial publication
was observed without a manual refresh. Later steps used the My Relewise default
Search Index rebuild action to shorten the refresh wait, after automatic ingestion
had occurred. Those actions refresh Relewise search; they do not invoke the Umbraco
provider or manufacture its records. This run does not establish immediate search
consistency or a production indexing-latency guarantee.

No integration code defect was found in this browser run. It supplements the 39
unit tests and 15 live provider checks. Permissions, cultures/segments, numeric
filters/facets and sort/paging were exercised in the separate live provider fixture;
they were not all recreated as browser-authored content in this run. Rich text,
media and Library Element editor journeys were not covered here.

The temporary credential remains outside the repository. Revocation/rotation and
removal of local settings copies remain open on the Trello cleanup checklist.
