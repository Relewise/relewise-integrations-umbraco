# Integration documentation

Use this integration to export published Umbraco pages to Relewise and track content views. Start with the [installation and connection setup](../README.md#installing-relewiseintegrationsumbraco), then choose how to map your site's content.

## Guides

| I want to… | Read |
| --- | --- |
| Understand automatic mapping and choose an extension point | [Custom content mapping](custom-content-mapping.md) |
| Export readable text from Block List, Block Grid, or nested blocks | [Mapping block editors](block-editors.md) |
| Copy and build a complete mapper | [Advanced content mapping example](examples/AdvancedContentMapping/README.md) |
| Verify exported data and diagnose missing or stale fields | [Validation and troubleshooting](validation-and-troubleshooting.md) |

## Version scope

These guides describe the public APIs and implementation in this repository. The complete example targets .NET 10, Relewise.Integrations.Umbraco 17.0.0, and Umbraco 17.6.2. The repository's current source accepts Umbraco 17–18, but that does not mean every historical integration release or editor uses the same value types. Check the dependencies of your installed package and the documentation on its corresponding branch or tag before copying examples into older projects.

“Grid” in these guides means **Block Grid**, unless explicitly described as the legacy Grid Layout editor. They have different data models.

## Maintaining these docs

Documentation lives beside the code so changes can be reviewed together in pull requests. Use relative Markdown links, keep examples consistent with the implementation, and build the example when changing mapping guidance. GitHub renders these pages and their heading navigation directly; no documentation site deployment is required. See [GitHub's guidance on relative links](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#relative-links).
