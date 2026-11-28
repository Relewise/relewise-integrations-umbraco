# Integration documentation

Use this integration to export published Umbraco content and catalogue entities to Relewise for search, recommendations and behavioral tracking. Start with [installation](../README.md#installation) and [connection setup](../README.md#getting-started), then choose how to map your site's content.

## Guides

| I want to… | Read |
| --- | --- |
| Upgrade an existing integration | [Migration](migration.md) |
| Choose numeric IDs, stable content keys or plan an ID migration | [Content identity across environments](content-identity.md) |
| Map Products, Variants, Categories and Brands | [Entity mapping](entity-mapping.md) |
| Use Relewise through Umbraco's core search API | [Umbraco search](search.md) |
| Understand automatic mapping and choose an extension point | [Custom content mapping](custom-content-mapping.md) |
| Export readable text from Block List, Block Grid, or nested blocks | [Mapping block editors](block-editors.md) |
| Run mapping, converters, search and tracking together | [Demo storefront and mapping examples](../samples/Integrations.Umbraco.DemoStorefront/README.md) |
| Verify exported data and diagnose missing or stale fields | [Validation and troubleshooting](validation-and-troubleshooting.md) |

## Version scope

These guides describe the shared integration capabilities on Umbraco 17, 18 and 19. Install the integration major matching your CMS: 17.x uses Umbraco `[17.0.0,18)` and .NET 10; 18.x uses `[18.2.0,19)` and .NET 10; 19.x uses `[19.0.0-rc1,20)` and .NET 11. The optional Search package supports the same CMS versions; install its matching major. DemoStorefront uses Umbraco `19.0.0-rc1`. See [migration](migration.md) for upgrade requirements and [Umbraco search](search.md) for provider configuration.

“Grid” in these guides means **Block Grid**, unless explicitly described as the legacy Grid Layout editor. They have different data models.
