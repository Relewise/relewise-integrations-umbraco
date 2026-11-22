using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Search.Indexing;
using static Relewise.Integrations.Umbraco.Search.CoreSearchTranslation;

namespace Relewise.Integrations.Umbraco.Indexing;

internal static class CoreSearchMetadata
{
    internal static void Apply(
        MappedEntity entity,
        CoreSearchDocument document,
        string[]? siteCultures = null)
    {
        Dictionary<string, DataValue?> data = EntityMapper.Data(entity.Entity);

        data[CultureKey] = new DataValue(
            document.Variations.Select(v => v.Culture?.ToLowerInvariant() ?? string.Empty)
                .Distinct());
        data[SegmentsKey] = new DataValue(document.Variations.Select(v => v.Segment ?? string.Empty).Distinct());
        data[ProtectedKey] = document.Protection != null;
        data[AccessKey] = new DataValue(document.Protection?.AccessIds.Select(id => id.ToString("D")) ?? []);

        // Retain supported default-segment metadata without blocking business-data export.
        foreach (IGrouping<string, IndexField> group in document.Fields.GroupBy(field => field.FieldName))
        {
            StoreText(
                data,
                group.Key,
                [.. group.Where(field => field.Segment == null)],
                siteCultures ?? [.. document.Variations.Select(value => value.Culture).OfType<string>()]);

            foreach (IGrouping<string?, IndexField> cultureGroup in group
                         .Where(field => field.Segment == null)
                         .GroupBy(field => field.Culture)
                         .OrderBy(fields => fields.Key != null))
            {
                var converted = new Dictionary<string, DataValue?>();

                IndexValue[] values = [.. cultureGroup.Select(field => field.Value)];

                StoreNumbers(
                    converted,
                    group.Key,
                    "integer",
                    values.SelectMany(v => v.Integers ?? []).Select(value => (double)value));
                StoreNumbers(
                    converted,
                    group.Key,
                    "decimal",
                    values.SelectMany(v => v.Decimals ?? []).Select(value => (double)value));
                StoreNumbers(
                    converted,
                    group.Key,
                    "date",
                    values.SelectMany(v => v.DateTimeOffsets ?? []).Select(DateNumber));

                foreach (KeyValuePair<string, DataValue?> field in converted)
                {
                    if (cultureGroup.Key == null)
                    {
                        data[field.Key] = field.Value;

                        foreach (string culture in siteCultures ??
                                 [
                                     .. document.Variations
                                         .Select(value => value.Culture)
                                         .OfType<string>()
                                 ])
                        {
                            data[field.Key + "_" + Uri.EscapeDataString(culture.ToLowerInvariant())] = field.Value;
                        }
                    }
                    else
                    {
                        data[field.Key + "_" + Uri.EscapeDataString(cultureGroup.Key.ToLowerInvariant())] = field.Value;
                    }
                }
            }
        }
    }

    // Shared block text participates in every culture alongside that culture's own values.
    private static void StoreText(
        Dictionary<string, DataValue?> data,
        string name,
        IndexField[] fields,
        string[] siteCultures)
    {
        string[] localizedCultures = fields
            .Select(field => field.Culture)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (localizedCultures.Length == 0)
        {
            StoreStrings(data, name, "keyword", fields.SelectMany(field => field.Value.Keywords ?? []));
            StoreStrings(data, name, "text", fields.SelectMany(field => Texts(field.Value)));

            return;
        }

        var localized = new Dictionary<string, List<(string Culture, DataValue Value)>>();

        foreach (string culture in localizedCultures.Concat(siteCultures).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IndexValue[] values =
            [
                .. fields
                    .Where(field =>
                        field.Culture == null ||
                        string.Equals(field.Culture, culture, StringComparison.OrdinalIgnoreCase))
                    .Select(field => field.Value)
            ];

            var converted = new Dictionary<string, DataValue?>();

            StoreStrings(converted, name, "keyword", values.SelectMany(value => value.Keywords ?? []));
            StoreStrings(converted, name, "text", values.SelectMany(Texts));

            foreach (KeyValuePair<string, DataValue?> field in converted)
            {
                if (!localized.TryGetValue(field.Key, out List<(string Culture, DataValue Value)>? entries))
                {
                    localized[field.Key] = entries = [];
                }

                entries.Add((culture, field.Value!));
            }
        }

        foreach (KeyValuePair<string, List<(string Culture, DataValue Value)>> field in localized)
        {
            data[field.Key] = field.Value[0].Value.Type == DataValue.DataValueTypes.String
                ? new DataValue(
                    new Multilingual(
                        field.Value
                            .Select(value => new Multilingual.Value(value.Culture, value.Value))
                            .ToArray()))
                : new DataValue(
                    new MultilingualCollection(
                        field.Value
                            .Select(value => new MultilingualCollection.Value(value.Culture, value.Value))
                            .ToArray()));
        }
    }
}
