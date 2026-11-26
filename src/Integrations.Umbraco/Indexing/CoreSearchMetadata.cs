using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Indexing;

internal static class CoreSearchMetadata
{
    internal static void Apply(MappedEntity entity, ExportSourceDocument document)
    {
        // These fields establish membership before paging and facets; application values remain in their mapped fields.
        Dictionary<string, DataValue?> data = EntityMapper.Data(entity.Entity);
        data[RelewiseDataKeys.Search.Culture] = new DataValue(document.SearchCultures);
        data[RelewiseDataKeys.Search.Protected] = document.AccessIds != null;
        data[RelewiseDataKeys.Search.Access] = new DataValue(
            document.AccessIds?.Select(id => id.ToString("D")) ?? []);
    }
}
