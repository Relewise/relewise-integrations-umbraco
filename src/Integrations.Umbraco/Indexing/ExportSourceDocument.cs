using System;

namespace Relewise.Integrations.Umbraco.Indexing;

// All variations determine exported languages; only default-segment variations establish search membership.
// Null access IDs mean public content; an empty array means protected content with no granted identities.
internal sealed record ExportSourceDocument(string?[] Cultures, string[] SearchCultures, Guid[]? AccessIds);
