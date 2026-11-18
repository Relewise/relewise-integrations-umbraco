using System;
using System.Linq;
using Relewise.Client.DataTypes;
using static Relewise.Integrations.Umbraco.Search.CoreSearchTranslation;

namespace Relewise.Integrations.Umbraco;

internal static class CoreSearchMetadata
{
    internal static void Apply(MappedEntity entity, CoreSearchDocument document, string[]? siteCultures = null)
    {
        var data = EntityMapper.Data(entity.Entity);
        data[CultureKey] = new DataValue(document.Variations.Select(v => v.Culture?.ToLowerInvariant() ?? string.Empty).Distinct());
        data[SegmentsKey] = new DataValue(document.Variations.Select(v => v.Segment ?? string.Empty).Distinct());
        data[ProtectedKey] = document.Protection != null;
        data[AccessKey] = new DataValue(document.Protection?.AccessIds.Select(id => id.ToString("D")) ?? []);
        // Retain supported default-segment metadata without blocking business-data export.
        foreach (var group in document.Fields.GroupBy(field => field.FieldName))
        {
            foreach (var cultureGroup in group.Where(field => field.Segment == null).GroupBy(field => field.Culture).OrderBy(group => group.Key != null))
            {
                var converted = new System.Collections.Generic.Dictionary<string, DataValue?>();
                var values = cultureGroup.Select(field => field.Value).ToArray();
                StoreStrings(converted, group.Key, "keyword", values.SelectMany(v => v.Keywords ?? []));
                StoreStrings(converted, group.Key, "text", values.SelectMany(Texts));
                StoreNumbers(converted, group.Key, "integer", values.SelectMany(v => v.Integers ?? []).Select(value => (double)value));
                StoreNumbers(converted, group.Key, "decimal", values.SelectMany(v => v.Decimals ?? []).Select(value => (double)value));
                StoreNumbers(converted, group.Key, "date", values.SelectMany(v => v.DateTimeOffsets ?? []).Select(DateNumber));
                foreach (var field in converted)
                {
                    bool numeric = field.Key.Contains("_integer", StringComparison.Ordinal) || field.Key.Contains("_decimal", StringComparison.Ordinal) || field.Key.Contains("_date", StringComparison.Ordinal);
                    if (numeric)
                    {
                        if (cultureGroup.Key == null)
                        {
                            data[field.Key] = field.Value;
                            foreach (string culture in siteCultures ?? document.Variations.Select(value => value.Culture).OfType<string>().ToArray())
                            {
                                data[field.Key + "_" + Uri.EscapeDataString(culture.ToLowerInvariant())] = field.Value;
                            }
                        }
                        else
                        {
                            data[field.Key + "_" + Uri.EscapeDataString(cultureGroup.Key.ToLowerInvariant())] = field.Value;
                        }
                    }
                    else if (cultureGroup.Key == null)
                    {
                        data[field.Key] = field.Value;
                    }
                    else
                    {
                        if (field.Value?.Type == DataValue.DataValueTypes.String)
                        {
                            if (!data.TryGetValue(field.Key, out DataValue? existing))
                            {
                                data[field.Key] = existing = new Multilingual();
                            }

                            existing!.ValueAs<Multilingual>().Add(new Multilingual.Value(cultureGroup.Key, field.Value));
                        }
                        else if (field.Value?.Type == DataValue.DataValueTypes.StringList)
                        {
                            if (!data.TryGetValue(field.Key, out DataValue? existing))
                            {
                                data[field.Key] = existing = new MultilingualCollection();
                            }

                            existing!.ValueAs<MultilingualCollection>().Add(new MultilingualCollection.Value(cultureGroup.Key, field.Value));
                        }
                    }
                }
            }
        }
    }
}
