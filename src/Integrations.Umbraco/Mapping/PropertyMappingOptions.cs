using System;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>Conversion settings for a strongly typed built-in destination property.</summary>
/// <typeparam name="TValue">The value selected by the destination expression.</typeparam>
public class PropertyMappingOptions<TValue>
{
    internal PropertyMappingOptions()
    {
    }

    internal Func<PropertyMappingContext, ValueTask<TValue>>? Converter { get; private set; }
    internal Type? ConverterType { get; private set; }

    /// <summary>Use this converter only for the configured property, resolving dependencies in the export
    /// scope.</summary>
    public void UseConverter<TConverter>() where TConverter : class, IPropertyMappingConverter<TValue>
    {
        if (Converter != null)
        {
            throw new InvalidOperationException("A converter has already been selected for this property.");
        }

        ConverterType = typeof(TConverter);
        Converter = context => context.Resolve<TConverter>().ConvertAsync(context, context.CancellationToken);
    }
}
