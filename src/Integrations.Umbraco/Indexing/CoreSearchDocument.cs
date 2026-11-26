using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed record CoreSearchDocument(Variation[] Variations, ContentProtection? Protection);
