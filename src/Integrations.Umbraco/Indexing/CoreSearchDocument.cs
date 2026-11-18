using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco;

internal sealed record CoreSearchDocument(Variation[] Variations, IndexField[] Fields, ContentProtection? Protection);
