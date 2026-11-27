using System;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Controllers.Messages;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class SearchDashboardContributor : IDashboardConfigurationContributor
{
    public void Apply(RelewiseUmbracoConfiguration configuration, ConfigurationViewModel snapshot)
    {
        if (!configuration.UsesUmbracoSearch())
            return;
        SearchProviderOptions options = configuration.GetSearchOptions();
        snapshot.SearchConfigured = true;
        snapshot.DefaultPublishedContentProviderAlias = options.Providers.Keys.FirstOrDefault(alias =>
            string.Equals(alias, options.DefaultPublishedContentProviderAlias, StringComparison.OrdinalIgnoreCase));
        snapshot.SearchProviders = options.Providers
            .OrderBy(pair => string.Equals(
                pair.Key,
                options.DefaultPublishedContentProviderAlias,
                StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1)
            .ThenBy(pair => pair.Value.EntityType == typeof(Content) ? 0 : 1)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => DescribeProvider(pair.Key, pair.Value, configuration))
            .ToArray();
    }

    private static DashboardSearchProviderViewModel DescribeProvider(
        string alias,
        SearchProviderDefinition provider,
        RelewiseUmbracoConfiguration configuration)
    {
        string[] restrictions = provider.ContentOptions?.DocumentTypeAliases ?? [];
        string? configuredLocation = provider.Options switch
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
            ResolvedDocumentTypeAliases = configuration.Mappings
                .Where(mapping => mapping.Export && mapping.EntityType == provider.EntityType
                                                 && (restrictions.Length == 0 ||
                                                     restrictions.Contains(
                                                         mapping.DocumentTypeAlias,
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
}
