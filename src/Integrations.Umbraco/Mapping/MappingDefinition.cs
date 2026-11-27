#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappingDefinition(Type entityType)
{
    public Type EntityType { get; } = entityType;
    public string DocumentTypeAlias { get; set; } = string.Empty;
    public bool Export { get; set; } = true;
    public bool TrackViews { get; set; } = true;
    public bool AutomaticData { get; set; }
    public IContentTypeMapping? LegacyMapper { get; set; }
    public Func<object, IPublishedContent, string[], MappingOperation, Task>? Mapper { get; set; }
    public Type? MapperType { get; set; }
    public string? IdPropertyAlias { get; set; }
    public string? BrandPropertyAlias { get; set; }
    public string? CategoryPropertyAlias { get; set; }
    public MappingDefinition? Variant { get; set; }
    public bool ReplaceExistingVariants => Variant is { Export: true, AdministrativeActions.Enabled: true };
    public AdministrativeActionPolicy AdministrativeActions { get; set; } = AdministrativeActionPolicy.Synchronize();
    public EntityUpdatePolicy UpdatePolicy { get; set; } = EntityUpdatePolicy.ReplaceProvidedProperties();
    public List<IPropertyMapping> Properties { get; } = new();
    public List<DataMapping> DataMappings { get; } = new();

    public void AddProperty(string destinationProperty, IPropertyMapping mapping)
    {
        if (Properties.Any(property => property.DestinationProperty == destinationProperty))
        {
            throw new InvalidOperationException($"Property '{destinationProperty}' is already mapped.");
        }

        Properties.Add(mapping);
    }

    public void Validate(bool parentAdministrativeActions = true)
    {
        bool synchronize = parentAdministrativeActions && AdministrativeActions.Enabled;

        if (UpdatePolicy.CanClear && !synchronize)
        {
            throw new InvalidOperationException(
                $"Mapping '{DocumentTypeAlias}' cannot use ClearAndReplace with AdministrativeActionPolicy.None, " +
                "including on its parent Product.");
        }

        bool explicitFields = DataMappings.Count > 0 || Properties.Count > 0;

        if (AutomaticData && explicitFields)
        {
            throw new InvalidOperationException(
                $"Mapping '{DocumentTypeAlias}' combines AutoMap with explicit fields.");
        }

        if (LegacyMapper != null && (explicitFields || Mapper != null))
        {
            throw new InvalidOperationException(
                $"Mapping '{DocumentTypeAlias}' combines a legacy mapper with new field or entity mappings.");
        }

        var dataKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (DataMapping data in DataMappings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(data.Options.DataKey);

            if (!dataKeys.Add(data.Options.DataKey))
            {
                throw new InvalidOperationException($"Data key '{data.Options.DataKey}' is already mapped.");
            }

            if (data.Options.DataKey is
                RelewiseDataKeys.DocumentTypeAlias or RelewiseDataKeys.Url or
                RelewiseDataKeys.CreatedAt or RelewiseDataKeys.Version ||
                data.Options.DataKey.StartsWith(RelewiseDataKeys.ReservedPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Data key '{data.Options.DataKey}' is reserved for integration metadata.");
            }
        }

        Variant?.Validate(synchronize);
    }
}
