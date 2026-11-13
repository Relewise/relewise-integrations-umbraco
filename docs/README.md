# Integration documentation

Use this integration to export published Umbraco pages to Relewise and track content views. Start with the [installation and connection setup](../README.md#installing-relewiseintegrationsumbraco), then choose how to map your site's content.

## Guides

| I want to… | Read |
| --- | --- |
| Choose numeric IDs or stable content keys across environments | [Content IDs across environments](../README.md#content-ids-across-environments) |
| Use Relewise through Umbraco's core search API | [Umbraco 19 and core search](umbraco-19-search.md) |
| Understand automatic mapping and choose an extension point | [Custom content mapping](custom-content-mapping.md) |
| Export readable text from Block List, Block Grid, or nested blocks | [Mapping block editors](block-editors.md) |
| Copy and build a complete mapper | [Advanced content mapping example](examples/AdvancedContentMapping/README.md) |
| Verify exported data and diagnose missing or stale fields | [Validation and troubleshooting](validation-and-troubleshooting.md) |

## Version scope

These guides describe the public APIs and implementation in this repository. The current source and complete example target .NET 11 and Umbraco 19.0.0-rc1 exclusively. For older Umbraco versions, use an earlier integration major and the documentation on its corresponding branch or tag. See [Umbraco 19 and core search](umbraco-19-search.md) for the new provider and upgrade requirements.

“Grid” in these guides means **Block Grid**, unless explicitly described as the legacy Grid Layout editor. They have different data models.

## Maintaining these docs

Documentation lives beside the code so changes can be reviewed together in pull requests. Use relative Markdown links, keep examples consistent with the implementation, and build the example when changing mapping guidance. GitHub renders these pages and their heading navigation directly; no documentation site deployment is required. See [GitHub's guidance on relative links](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#relative-links).
