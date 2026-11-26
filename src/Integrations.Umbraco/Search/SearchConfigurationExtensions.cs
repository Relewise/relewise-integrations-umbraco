using System;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>
/// Enables Relewise's integration with Umbraco Search.
/// </summary>
public static class SearchConfigurationExtensions
{
    private static readonly SearchProviderOptions Unconfigured = new();

    /// <summary>
    /// Replaces notification-based export with Umbraco Search indexing. Named query providers are optional.
    /// This method may be called once across all Relewise configuration callbacks.
    /// </summary>
    /// <param name="options">The integration configuration.</param>
    /// <param name="configure">Optional configuration of Content and Product query providers.</param>
    /// <returns>The integration configuration.</returns>
    public static RelewiseUmbracoOptionsBuilder ConfigureSearch(
        this RelewiseUmbracoOptionsBuilder options,
        Action<SearchProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ExportSystemConfiguration != null)
        {
            throw new InvalidOperationException("ConfigureSearch can only be called once.");
        }

        var configuration = new SearchExportConfiguration();
        options.ExportSystemConfiguration = configuration;
        configure?.Invoke(configuration.Options);
        return options;
    }

    internal static SearchProviderOptions GetSearchOptions(this RelewiseUmbracoConfiguration configuration)
    {
        return (configuration.ExportSystemConfiguration as SearchExportConfiguration)?.Options ?? Unconfigured;
    }

    internal static bool UsesUmbracoSearch(this RelewiseUmbracoConfiguration configuration)
    {
        return configuration.ExportSystemConfiguration is SearchExportConfiguration;
    }
}
