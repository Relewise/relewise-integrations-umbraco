#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco;

internal interface IPropertyMapping
{
    string SourcePropertyAlias { get; }
    string DestinationProperty { get; }
    Type? ConverterType { get; }
    ValueTask ApplyAsync(object entity, PropertyMappingContext context);
}
