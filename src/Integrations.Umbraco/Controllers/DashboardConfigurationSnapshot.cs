using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Controllers.Messages;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Search;

namespace Relewise.Integrations.Umbraco.Controllers;

internal static class DashboardConfigurationSnapshot
{
    internal static ConfigurationViewModel Create(
        RelewiseUmbracoConfiguration configuration,
        List<NamedOptionsViewObject> clients,
        bool middlewareEnabled)
    {
        return new ConfigurationViewModel(configuration.TrackedContentTypes,
            configuration.ExportedContentTypes,
            clients,
            middlewareEnabled)
        {
            Mappings = configuration.Mappings.Values.OrderBy(mapping => MappingOrder(mapping.EntityType))
                .ThenBy(mapping => mapping.EntityType == typeof(ProductVariant) ? 1 : 0)
                .ThenBy(mapping => mapping.DocumentTypeAlias, StringComparer.OrdinalIgnoreCase)
                .Select(mapping => Describe(mapping, configuration))
                .ToArray(),
            DefaultPublishedContentProviderAlias = configuration.SearchProviders.Providers.Keys.FirstOrDefault(alias =>
                string.Equals(alias,
                    configuration.SearchProviders.DefaultPublishedContentProviderAlias,
                    StringComparison.OrdinalIgnoreCase)),
            SearchProviders = configuration.SearchProviders.Providers
                .OrderBy(pair => string.Equals(pair.Key,
                    configuration.SearchProviders.DefaultPublishedContentProviderAlias,
                    StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
                .ThenBy(pair => pair.Value.EntityType == typeof(Content) ? 0 : 1)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => DescribeProvider(pair.Key, pair.Value, configuration))
                .ToArray()
        };
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

    private static DashboardSearchProviderViewModel DescribeProvider(
        string alias,
        SearchProviderDefinition provider,
        RelewiseUmbracoConfiguration configuration)
    {
        var restrictions = provider.ContentOptions?.DocumentTypeAliases ?? [];
        var configuredLocation = provider.Options switch
        {
            ContentSearchProviderOptions content => content.DisplayedAtLocation,
            ProductSearchProviderOptions productLocation => productLocation.DisplayedAtLocation,
            _ => null
        };

        return new DashboardSearchProviderViewModel
        {
            Alias = alias,
            EntityType = provider.EntityType.Name,
            DocumentTypeAliases = restrictions,
            ResolvedDocumentTypeAliases = configuration.Mappings.Values
                .Where(mapping => mapping.Export && mapping.EntityType == provider.EntityType
                                                 && (restrictions.Length == 0 ||
                                                     restrictions.Contains(mapping.DocumentTypeAlias,
                                                         StringComparer.OrdinalIgnoreCase)))
                .Select(mapping => mapping.DocumentTypeAlias)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DisplayedAtLocation = provider.Location(alias),
            UsesFallbackLocation = string.IsNullOrWhiteSpace(configuredLocation),
            RequirePublishedPage = provider.Options is not ProductSearchProviderOptions product ||
                                   product.RequirePublishedPage,
            CustomRequest = provider.Options switch
            {
                ContentSearchProviderOptions content => content.Configure != null,
                ProductSearchProviderOptions productOptions => productOptions.Configure != null,
                _ => false
            },
            IgnoreSegments = provider.Options switch
            {
                ContentSearchProviderOptions content => content.UnsupportedSegmentBehavior.IgnoreSegment,
                ProductSearchProviderOptions productOptions => productOptions.UnsupportedSegmentBehavior.IgnoreSegment,
                _ => false
            }
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

        fields.AddRange(mapping.DataMappings.Select(data => new DashboardFieldMappingViewModel
        {
            SourcePropertyAlias = data.SourcePropertyAlias,
            Destination = $"Data[\"{data.Options.DataKey}\"]",
            Conversion = data.Options.ConverterType?.Name ?? "Automatic selection"
        }));

        MappingDefinition? parent =
            configuration.Mappings.Values.FirstOrDefault(parent => ReferenceEquals(parent.Variant, mapping));
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
                $"{mapping.CategoryPropertyAlias} → Additional categories, using each referenced Category's configured ID source.");
        }

        if (mapping.Variant != null)
        {
            relationships.Add(
                $"Child documents of type '{mapping.Variant.DocumentTypeAlias}' supply Product Variants when their export is configured.");
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
            SynchronizeLifecycle = mapping.AdministrativeActions.Enabled,
            AutoMap = mapping.AutomaticData,
            CustomMapper = mapping.Mapper != null || mapping.LegacyMapper != null,
            CustomMapperTypeName = (mapping.MapperType ?? mapping.LegacyMapper?.GetType())?.Name,
            LegacyMapper = mapping.LegacyMapper != null,
            Fields = fields,
            Relationships = relationships
        };
    }
}
