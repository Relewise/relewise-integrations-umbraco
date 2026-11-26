using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Indexing;

internal static class CoreSearchMetadata
{
    internal static void Apply(MappedEntity entity, CoreSearchDocument document)
    {
        // These fields establish membership before paging and facets; application values remain in their mapped fields.
        Dictionary<string, DataValue?> data = EntityMapper.Data(entity.Entity);
        data[RelewiseDataKeys.Search.Culture] = new DataValue(
            document.Variations.Where(variation => variation.Segment == null)
                .Select(variation => variation.Culture?.ToLowerInvariant() ?? string.Empty)
                .Distinct());
        data[RelewiseDataKeys.Search.Protected] = document.Protection != null;
        data[RelewiseDataKeys.Search.Access] = new DataValue(
            document.Protection?.AccessIds.Select(id => id.ToString("D")) ?? []);
    }
}
