using System;
using System.Collections.Generic;

namespace Relewise.Integrations.Umbraco.Indexing;

// Keys includes removals; Documents contains the latest available metadata for each collected document.
internal sealed class ExportSourceBatch
{
    public HashSet<Guid> Keys { get; } = [];
    public Dictionary<Guid, ExportSourceDocument> Documents { get; } = new();

    public void Include(ExportSourceBatch batch)
    {
        foreach (Guid key in batch.Keys)
        {
            Keys.Add(key);

            // A later source callback may remove a document collected earlier in the same export.
            if (batch.Documents.TryGetValue(key, out ExportSourceDocument? document))
            {
                Documents[key] = document;
            }
            else
            {
                Documents.Remove(key);
            }
        }
    }
}
