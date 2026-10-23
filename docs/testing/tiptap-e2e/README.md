# Tiptap end-to-end verification

Executed on 23 October 2026 against integration commit `d65ccb0c333eddd5957f1a8ef07d4a6c283a5b27` for [the Tiptap support card](https://trello.com/c/lC2Y6WTe).

## Environment and installation

- Fresh site from the official `Umbraco.Templates` 18.2.0 template, .NET 10, SQLite, Development.
- Installed `Relewise.Integrations.Umbraco.0.0.0-tiptap-smoke-d65ccb0.nupkg` from a local package feed. The site uses a NuGet package reference, not a project reference.
- Installed integration DLL SHA-256: `0C3799A7A1253E39D3FB5810C4D284157834D6A1CCC430C4346059024F142561`, identical to the branch build.
- Default `AddRelewise(options => options.AddContentType("smokePage", type => type.AutoMap()))`, with no explicit rich-text converter registration.
- The test site's startup bootstrap creates a root document type with one `description` property using Umbraco's installed Rich Text datatype. All document content is authored, saved, and published in Chrome through the real backoffice.
- Fresh API-only Dataset `8855c822-4b7d-41bf-bd30-81b5e70aed04`; content ID `1057`; document key `acdabbae-248b-430d-9cbc-36857c098292`.
- Umbraco: `http://localhost:5187/`; isolated API: `https://localhost:5002/`. The pre-existing API on port 5000 was left running.
- API rebuilt from local source commit `8222bffc5a395c2bcb109a79ca7c6e3f2e13b0b7`, in Development. That checkout was one commit behind the locally recorded remote branch; no remote fetch or source update was performed. Existing NU1902 audit errors blocked the standard build, so this local-only build used `-p:NuGetAudit=false`. No API source or dependency files were changed.
- Dataset and local administrator credentials were generated locally and are excluded from this evidence.

## Verification method

1. Install a fresh Umbraco site and the packed integration package; configure its Relewise Dataset ID, API key, and server URL locally.
2. Confirm an authenticated `POST /{datasetId}/v1/ContentQuery` returns no content before the first publish.
3. Author a description in the real Tiptap editor. This run used its built-in source dialog for the formatted fixture, then verified the rendered heading, bold, italics, list, and link in the editor.
4. Use **Save and publish**. The package's normal `ContentPublishedNotification` handler exports the document. There is no mocked tracker and no direct ContentUpdate fixture import.
5. Independently read the published `description` through Umbraco's published cache and query stored content with an authenticated Relewise `ContentQuery`. The local inspection endpoint only reads published values; it does not map or export them.
6. Compare `IHtmlEncodedString.ToHtmlString()` with Relewise's `data.description.value` using case-sensitive, exact string equality.
7. Repeat after a draft save, publishing an edit, clearing the editor, and restoring the formatted description. Query both systems afresh after each step.

The actual published property was `Umbraco.RichText`, editor UI `Umb.PropertyEditorUi.Tiptap`, and runtime value type `Umbraco.Cms.Core.Strings.HtmlEncodedString`.

## Results

| Scenario | Expected and observed | Result |
| --- | --- | --- |
| Fresh Dataset | Authenticated query returns zero content records | Pass |
| First publish | Exactly one enabled record; description HTML exactly matches the published HTML, including heading, bold, italics, list, link, `&amp;`, and `æøå` | Pass |
| Save draft | Previously published HTML remains in both the published cache and Relewise | Pass |
| Publish changed description | Relewise description changes to the newly published value | Pass |
| Clear field and publish | Published and stored HTML both become `<p></p>`; previous description is removed | Pass |
| Restore rich text and publish | Exact original formatted HTML is exported again | Pass |

The empty-field case used editor keyboard selection and deletion after verifying that an initial automated empty `fill` had left the editor unchanged. The result above is from the verified empty editor and a subsequent publish.

## Evidence

The [Umbraco response](umbraco-first-publish.json) and [Relewise response](relewise-first-publish.json) contain the actual captured first-publish values. [summary.json](summary.json) records the scenario results and installed DLL hash. The API screenshot is a local report generated from captured responses and assertions; it is not the My Relewise interface.

### Real Tiptap editor

![Formatted Tiptap description before publishing](https://github.com/user-attachments/assets/d73abcf1-2c67-4395-a918-bdd72445a304)

### Successful publication

![Umbraco publish history and Published status](https://github.com/user-attachments/assets/7af1bfd1-c51c-4ffb-acea-b39e13cd10a9)

### API verification

![Scenario results and exact comparison of published and stored HTML](https://github.com/user-attachments/assets/28babe3e-4584-47e7-b23f-58486b39c419)

## Scope

This verifies a fresh Umbraco 18.2.0 installation, real Tiptap editing and publishing, the installed integration package's default conversion and export path, and persisted content in a real local Relewise API. Older Umbraco majors, embedded RTE blocks, publishing multiple cultures, My Relewise registration/UI, and production infrastructure were not exercised.

The local site and isolated API remain available for inspection. The detailed local manifest, runtime setup, verification script, response captures, and additional screenshots are under `C:\Users\mail\.codex\artifacts\umbraco-tiptap-e2e-20261023`. Runtime configuration in that directory contains local-only credentials and must not be uploaded.
