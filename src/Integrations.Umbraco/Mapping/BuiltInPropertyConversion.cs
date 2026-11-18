using System;
using System.Collections.Generic;
using System.Globalization;
using Relewise.Client.DataTypes;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco;

internal static class BuiltInPropertyConversion
{
    internal static TValue Convert<TValue>(PropertyMappingContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        string culture = context.CulturesToPublish.Count > 0 ? context.CulturesToPublish[0] : throw new InvalidOperationException("A published culture is required.");
        if (typeof(TValue) == typeof(Multilingual))
        {
            var values = new List<Multilingual.Value>();
            foreach (string publishedCulture in context.CulturesToPublish)
            {
                object? raw = context.Property.GetValue(publishedCulture);
                if (raw != null)
                {
                    values.Add(new Multilingual.Value(publishedCulture, System.Convert.ToString(raw, CultureInfo.InvariantCulture)));
                }
            }
            return (TValue)(object)new Multilingual(values);
        }
        if (typeof(TValue) == typeof(string) && context.Property.PropertyType.VariesByCulture())
        {
            throw new InvalidOperationException($"'{context.DestinationProperty}' is a single string. Configure a typed converter to select the source culture explicitly.");
        }

        object? value = context.Property.GetValue(culture);
        if (value is TValue typed)
        {
            return typed;
        }

        if (value == null && default(TValue) == null)
        {
            return default!;
        }

        if (typeof(TValue) == typeof(List<int>) && value is IEnumerable<int> integers)
        {
            return (TValue)(object)new List<int>(integers);
        }

        throw new InvalidOperationException($"Property '{context.Property.Alias}' on '{context.PublishedContent.Key}' cannot map to {typeof(TValue).Name} ({context.DestinationProperty}). Configure a typed converter. MultiCurrency values require explicit currency codes.");
    }
}
