using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>
/// Named query providers, independently configured from indexing.
/// </summary>
public class SearchProviderOptions
{
    internal SearchProviderOptions()
    {
    }

    internal Dictionary<string, SearchProviderDefinition> Providers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Provider selected for ordinary Umbraco published-content queries. Null preserves the existing
    /// provider.
    /// </summary>
    public string? DefaultPublishedContentProviderAlias { get; set; }

    /// <summary>
    /// Register a Content search with optional Document Type selection.
    /// </summary>
    public void AddContentProvider(string providerAlias, Action<ContentSearchProviderOptions>? configure = null)
    {
        var options = new ContentSearchProviderOptions();

        configure?.Invoke(options);
        Add(providerAlias, new SearchProviderDefinition(options, options));
    }

    /// <summary>
    /// Register a query over the single Product mapping.
    /// </summary>
    public void AddProductProvider(string providerAlias, Action<ProductSearchProviderOptions>? configure = null)
    {
        var options = new ProductSearchProviderOptions();

        configure?.Invoke(options);
        Add(providerAlias, new SearchProviderDefinition(options, null));
    }

    private void Add(string providerAlias, SearchProviderDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerAlias);

        if (!Providers.TryAdd(providerAlias, definition))
        {
            throw new InvalidOperationException($"Search provider '{providerAlias}' is already registered.");
        }
    }

    internal void Validate(MappingCollection mappings)
    {
        if (DefaultPublishedContentProviderAlias != null &&
            !Providers.ContainsKey(DefaultPublishedContentProviderAlias))
        {
            throw new InvalidOperationException(
                $"Default search provider '{DefaultPublishedContentProviderAlias}' is not registered.");
        }

        foreach (KeyValuePair<string, SearchProviderDefinition> registration in Providers)
        {
            UnsupportedSegmentBehavior? behavior = registration.Value.Options switch
            {
                ContentSearchProviderOptions content => content.UnsupportedSegmentBehavior,
                ProductSearchProviderOptions product => product.UnsupportedSegmentBehavior,
                _ => null
            };

            if (behavior == null)
            {
                throw new InvalidOperationException(
                    $"Search provider '{registration.Key}' requires an UnsupportedSegmentBehavior policy.");
            }

            if (!mappings.Exports(registration.Value.EntityType))
            {
                throw new InvalidOperationException(
                    $"Search provider '{registration.Key}' has no exported " +
                    $"{registration.Value.EntityType.Name} mapping.");
            }

            if (registration.Value.ContentOptions?.DocumentTypeAliases is { } aliases)
            {
                if (aliases.Length == 0)
                {
                    throw new InvalidOperationException(
                        "DocumentTypeAliases must be omitted or contain at least one Content mapping.");
                }

                for (var i = 0; i < aliases.Length; i++)
                {
                    string alias = aliases[i];

                    if (string.IsNullOrWhiteSpace(alias) ||
                        !mappings.TryGetMapping(alias, out MappingDefinition? mapping) ||
                        mapping.EntityType != typeof(Content) || !mapping.Export)
                    {
                        throw new InvalidOperationException(
                            $"Search provider '{registration.Key}' references an unknown " +
                            $"or non-Content mapping '{alias}'.");
                    }

                    aliases[i] = mapping.DocumentTypeAlias;
                }
            }
        }
    }
}
