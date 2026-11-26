#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed record DataMapping(string SourcePropertyAlias, DataMappingOptions Options);
