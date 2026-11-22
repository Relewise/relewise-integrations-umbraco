#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class PropertyMapping<TEntity, TValue>(string sourcePropertyAlias, string destinationProperty,
    Action<TEntity, TValue> assign, Func<PropertyMappingContext, ValueTask<TValue>>? converter, Type? converterType) : IPropertyMapping where TEntity : class
{
    public string SourcePropertyAlias { get; } = sourcePropertyAlias;
    public string DestinationProperty { get; } = destinationProperty;
    public Type? ConverterType { get; } = converterType;
    public async ValueTask ApplyAsync(object entity, PropertyMappingContext context)
    {
        if (entity is not TEntity destination)
        {
            throw new InvalidOperationException("The mapped entity type does not match its configured property.");
        }

        try
        {
            TValue value;
            if (converter != null)
            {
                value = await converter(context);
            }
            else
            {
                value = await BuiltInPropertyConversion.ConvertAsync<TValue>(context);
            }

            assign(destination, value);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Cannot map '{context.Property.Alias}' on document '{context.PublishedContent.Key}' to '{context.DestinationProperty}' for cultures '{string.Join(", ", context.CulturesToPublish)}'.", error);
        }
    }
}
