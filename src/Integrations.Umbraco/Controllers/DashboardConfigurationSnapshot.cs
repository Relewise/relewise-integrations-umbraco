using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Controllers.Messages;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Controllers;

internal static class DashboardConfigurationSnapshot
{
    public static ConfigurationViewModel Create(
        RelewiseUmbracoConfiguration configuration,
        List<NamedOptionsViewObject> clients,
        bool middlewareEnabled,
        IEnumerable<IDashboardConfigurationContributor>? contributors = null)
    {
        var snapshot = new ConfigurationViewModel(
            configuration.TrackedContentTypes,
            configuration.ExportedContentTypes,
            clients,
            middlewareEnabled)
        {
            Mappings = configuration.Mappings.OrderBy(mapping => MappingOrder(mapping.EntityType))
                .ThenBy(mapping => mapping.EntityType == typeof(ProductVariant) ? 1 : 0)
                .ThenBy(mapping => mapping.DocumentTypeAlias, StringComparer.OrdinalIgnoreCase)
                .Select(mapping => Describe(mapping, configuration))
                .ToArray()
        };
        foreach (IDashboardConfigurationContributor contributor in contributors ?? [])
        {
            contributor.Apply(configuration, snapshot);
        }

        return snapshot;
    }

    private static int MappingOrder(Type entityType)
    {
        return entityType.Name switch
        {
            nameof(Content) => 0,
            nameof(Product) or nameof(ProductVariant) => 1,
            nameof(Brand) => 2,
            nameof(ProductCategory) => 3,
            nameof(ContentCategory) => 4,
            _ => 5
        };
    }

    private static DashboardMappingViewModel Describe(
        MappingDefinition mapping,
        RelewiseUmbracoConfiguration configuration)
    {
        List<DashboardFieldMappingViewModel> fields =
        [
            .. mapping.Properties.Select(property =>
                new DashboardFieldMappingViewModel
                {
                    SourcePropertyAlias = property.SourcePropertyAlias,
                    Destination = property.DestinationProperty,
                    Conversion = property.ConverterType?.Name ?? "Default"
                })
        ];

        fields.AddRange(
            mapping.DataMappings.Select(data => new DashboardFieldMappingViewModel
            {
                SourcePropertyAlias = data.SourcePropertyAlias,
                Destination = $"Data[\"{data.Options.DataKey}\"]",
                Conversion = data.Options.ConverterType?.Name ?? "Automatic selection"
            }));

        MappingDefinition? parent =
            configuration.Mappings.FirstOrDefault(parent => ReferenceEquals(parent.Variant, mapping));
        var relationships = new List<string>();

        if (mapping.AdministrativeActions.Enabled && (mapping.EntityType == typeof(Content) ||
                                                      mapping.EntityType == typeof(Product) ||
                                                      typeof(Category).IsAssignableFrom(mapping.EntityType)))
        {
            relationships.Add("Category paths are derived from the published Umbraco hierarchy.");
        }

        if (mapping.BrandPropertyAlias != null)
        {
            relationships.Add(
                $"{mapping.BrandPropertyAlias} → Brand, using the referenced Brand's configured ID source.");
        }

        if (mapping.CategoryPropertyAlias != null)
        {
            relationships.Add(
                $"{mapping.CategoryPropertyAlias}" +
                $" → Additional categories, using each referenced Category's configured ID source.");
        }

        if (mapping.Variant != null)
        {
            relationships.Add(
                $"Child documents of type '{mapping.Variant.DocumentTypeAlias}" +
                $"' supply Product Variants when their export is configured.");
        }

        if (parent != null)
        {
            relationships.Add($"Exported with its parent Product document of type '{parent.DocumentTypeAlias}'.");
        }

        return new DashboardMappingViewModel
        {
            DocumentTypeAlias = mapping.DocumentTypeAlias,
            EntityType = mapping.EntityType.Name,
            ParentDocumentTypeAlias = parent?.DocumentTypeAlias,
            IdSource = mapping.IdPropertyAlias == null
                ? configuration.UseContentKeysAsIds ? "Node key" : "Node ID"
                : $"Property: {mapping.IdPropertyAlias}",
            Export = mapping.Export,
            TrackViews = mapping.TrackViews,
            SynchronizeLifecycle = mapping.AdministrativeActions.Enabled &&
                                   (parent?.AdministrativeActions.Enabled ?? true),
            ClearOnDeltaUpdates = mapping.UpdatePolicy.Clears(fullRebuild: false),
            ClearOnFullRebuilds = mapping.UpdatePolicy.Clears(fullRebuild: true),
            AutoMap = mapping.AutomaticData,
            CustomMapper = mapping.Mapper != null || mapping.LegacyMapper != null,
            CustomMapperTypeName = (mapping.MapperType ?? mapping.LegacyMapper?.GetType())?.Name,
            LegacyMapper = mapping.LegacyMapper != null,
            Fields = fields,
            Relationships = relationships
        };
    }
}
