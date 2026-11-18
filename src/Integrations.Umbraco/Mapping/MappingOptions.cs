using System;

namespace Relewise.Integrations.Umbraco;

/// <summary>Conversion settings for a strongly typed built-in destination property.</summary>
/// <typeparam name="TValue">The value selected by the destination expression.</typeparam>
public class PropertyMappingOptions<TValue>
{
    internal PropertyMappingOptions() { }
    internal Func<PropertyMappingContext, TValue>? Converter { get; private set; }
    /// <summary>Use this converter only for the configured property, resolving dependencies in the export scope.</summary>
    public void UseConverter<TConverter>() where TConverter : class, IPropertyMappingConverter<TValue>
    {
        if (Converter != null) throw new InvalidOperationException("A converter has already been selected for this property.");
        Converter = context => context.Resolve<TConverter>().Convert(context);
    }
}

/// <summary>Destination key and optional converter for an explicitly mapped Data field.</summary>
public class DataMappingOptions
{
    internal Type? ConverterType { get; private set; }
    internal DataMappingOptions(string sourcePropertyAlias) => DataKey = sourcePropertyAlias;
    /// <summary>The destination Data key; defaults to the source property alias.</summary>
    public string DataKey { get; set; }
    /// <summary>Select a single Data converter for this field without adding it to global converter selection.</summary>
    public void UseConverter<TConverter>() where TConverter : class, IRelewisePropertyValueConverter
    {
        if (ConverterType != null) throw new InvalidOperationException("A converter has already been selected for this Data mapping.");
        ConverterType = typeof(TConverter);
    }
}
