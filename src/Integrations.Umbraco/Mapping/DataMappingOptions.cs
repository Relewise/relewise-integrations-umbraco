using System;

namespace Relewise.Integrations.Umbraco;

/// <summary>Destination key and optional converter for an explicitly mapped Data field.</summary>
public class DataMappingOptions
{
    internal Type? ConverterType { get; private set; }
    internal DataMappingOptions(string sourcePropertyAlias) => DataKey = sourcePropertyAlias;
    /// <summary>The destination Data key; defaults to the source property alias.</summary>
    public string DataKey { get; set; }
    /// <summary>Select a single Data converter for this field without adding it to global converter selection.</summary>
    public void UseConverter<TConverter>() where TConverter : class, IAsyncRelewisePropertyValueConverter
    {
        if (ConverterType != null)
        {
            throw new InvalidOperationException("A converter has already been selected for this Data mapping.");
        }

        ConverterType = typeof(TConverter);
    }
}
