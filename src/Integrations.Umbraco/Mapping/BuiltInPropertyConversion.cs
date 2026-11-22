using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Mapping;

internal static class BuiltInPropertyConversion
{
    internal static async ValueTask<TValue> ConvertAsync<TValue>(PropertyMappingContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var culture = context.CulturesToPublish.Count > 0
            ? context.CulturesToPublish[0]
            : throw new InvalidOperationException("A published culture is required.");

        if (typeof(TValue) == typeof(Multilingual))
        {
            var values = new List<Multilingual.Value>();

            foreach (var publishedCulture in context.CulturesToPublish)
            {
                var raw =
                    context.Property.GetValue(context.Property.PropertyType.VariesByCulture()
                        ? publishedCulture
                        : null);

                if (raw != null)
                {
                    values.Add(new Multilingual.Value(publishedCulture,
                        Convert.ToString(raw, CultureInfo.InvariantCulture)));
                }
            }

            return (TValue)(object)new Multilingual(values);
        }

        if (typeof(TValue) == typeof(string) && context.Property.PropertyType.VariesByCulture())
        {
            culture = await context.DefaultCultureAsync();
            context.CancellationToken.ThrowIfCancellationRequested();
        }

        var value = context.Property.GetValue(context.Property.PropertyType.VariesByCulture() ? culture : null);

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

        throw new InvalidOperationException(
            $"Property '{context.Property.Alias}' on '{context.PublishedContent.Key}' cannot map to {typeof(TValue).Name} ({context.DestinationProperty}). Configure a typed converter. MultiCurrency values require explicit currency codes.");
    }
}
