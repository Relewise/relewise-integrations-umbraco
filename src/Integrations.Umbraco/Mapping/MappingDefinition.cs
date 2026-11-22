#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappingDefinition(Type entityType)
{
    internal Type EntityType { get; } = entityType;
    internal string DocumentTypeAlias { get; set; } = string.Empty;
    internal bool Export { get; set; } = true;
    internal bool TrackViews { get; set; } = true;
    internal bool AutomaticData { get; set; }
    internal bool Legacy { get; set; }
    internal IContentTypeMapping? LegacyMapper { get; set; }
    internal Func<object, IPublishedContent, string[], MappingOperation, Task>? Mapper { get; set; }
    internal Type? MapperType { get; set; }
    internal string? IdPropertyAlias { get; set; }
    internal string? BrandPropertyAlias { get; set; }
    internal string? CategoryPropertyAlias { get; set; }
    internal MappingDefinition? Variant { get; set; }
    internal AdministrativeActionPolicy AdministrativeActions { get; set; } = AdministrativeActionPolicy.Synchronize();
    internal List<IPropertyMapping> Properties { get; } = new();
    internal HashSet<string> PropertyNames { get; } = new(StringComparer.Ordinal);
    internal List<DataMapping> DataMappings { get; } = new();
    internal HashSet<string> DataKeys { get; } = new(StringComparer.Ordinal);
    internal void AddProperty(string destinationProperty, IPropertyMapping mapping)
    {
        if (!PropertyNames.Add(destinationProperty))
        {
            throw new InvalidOperationException($"Property '{destinationProperty}' is already mapped.");
        }

        Properties.Add(mapping);
    }
    internal void Validate()
    {
        if ((AutomaticData && (DataMappings.Count > 0 || Properties.Count > 0)) || (LegacyMapper != null && (DataMappings.Count > 0 || Properties.Count > 0 || Mapper != null)))
        {
            throw new InvalidOperationException($"Mapping '{DocumentTypeAlias}' combines automatic/custom and explicit field mappings.");
        }

        var dataKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DataMapping data in DataMappings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(data.Options.DataKey);
            if (!dataKeys.Add(data.Options.DataKey))
            {
                throw new InvalidOperationException($"Data key '{data.Options.DataKey}' is already mapped.");
            }

            if (data.Options.DataKey is "contentTypeAlias" or "url" or "createdAt" or "UmbracoVersionId" || data.Options.DataKey.StartsWith("rwUmbraco", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Data key '{data.Options.DataKey}' is reserved for integration metadata.");
            }
        }

        Variant?.Validate();
    }
}
