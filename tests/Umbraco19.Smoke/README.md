# Disposable Umbraco 19 smoke host

Build/test the solution, then pack the integration and restore the
`tests/Umbraco19.Smoke` test host from that local NuGet feed. This host intentionally
uses a PackageReference. Run it from a disposable directory, with a fresh SQLite
database and a `local.settings.json`; bind it to loopback only. Its `/smoke/*` routes
are test operations and **must never be deployed**.

`Initialize-SmokeSite.ps1` creates that disposable directory.
Pass `-CredentialFile` with an existing local settings file containing a `Relewise`
section to run against a dedicated live Dataset. Without it, the host uses a dummy
credential and the optional local simulation. The generated local settings also
contain the unattended backoffice user's password; keep them outside source control.

`/smoke/seed` creates and publishes a real page. `/smoke/provider` exercises the core
resolver, indexing, searching and metadata. `/smoke/verify` runs live checks for
protection, cultures, segments, ranges, sorting, facets, paging and scoped cleanup.
Use a dedicated disposable Dataset: the verification performs real writes/deletes.
Revoke its temporary credential when testing is complete.

For a browser lifecycle smoke test, call `/smoke/setup-body` after `/smoke/seed`
to add a Textarea property to the fixture's document type. Create and edit another
Smoke page through `/umbraco`, then use `/smoke/search` as an anonymous visitor.
The setup also creates a Razor template and enables automatic content-view tracking.
Open the page's normal Umbraco URL to exercise routing, rendering and tracking; the
test credential needs ContentView permission in addition to indexing/search access.
The form queries the configured published-content searcher and links results to
their normal Umbraco URLs. `/smoke/page/{key}` also renders the published cache for
diagnostics. The search page displays the
raw remote match count and every returned ID, including stale IDs whose content
is no longer published, so cache filtering cannot hide an indexing failure.
Verify draft exclusion, publication, body search, unpublished edits, republication,
unpublication, recycle-bin deletion and a native published-index rebuild. Wait for
remote indexing between checks; a manual Relewise Search Index refresh may shorten
the dedicated Dataset's refresh delay without manually invoking the provider.
The host enables DI scope/build validation. To test visitor identity, register a
request-scoped `IRelewiseUserLocator` in the disposable copy, then compare the IDs
in view and search requests while sending overlapping requests for different
visitors. Test anonymous, temporary and authenticated users, and check that scoped
services are disposed after successful and failed requests. Keep credentials,
captures and run-specific Dataset details outside the repository; record execution
results in the pull request.


The optional Postman simulation in `postman/simulations/umbraco19.sim.yaml` checks
SDK HTTP wiring against a local fixture, using `Smoke:UseJson=true` and a dummy key.
It is not evidence for live ranking or backend indexing behavior. Its context file
records the boundaries.
