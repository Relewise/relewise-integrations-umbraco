using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

/// <summary>Named query providers, independently configured from indexing.</summary>
public class SearchProviderOptions
{
    internal SearchProviderOptions()
    {
    }
    internal Dictionary<string, SearchProviderDefinition> Providers { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Provider selected for ordinary Umbraco published-content queries. Null preserves the existing provider.</summary>
    public string? DefaultPublishedContentProviderAlias { get; set; }
    /// <summary>Register a Content search with optional Document Type selection.</summary>
    public void AddContent(string providerAlias, Action<ContentSearchProviderOptions>? configure = null)
    {
        var options = new ContentSearchProviderOptions();
        configure?.Invoke(options);
        Add(providerAlias, new SearchProviderDefinition(options, options));
    }
    /// <summary>Register a query over the single Product mapping.</summary>
    public void AddProduct(string providerAlias, Action<ProductSearchProviderOptions>? configure = null)
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
    internal void Validate(IReadOnlyDictionary<string, MappingDefinition> mappings)
    {
        if (DefaultPublishedContentProviderAlias != null && !Providers.ContainsKey(DefaultPublishedContentProviderAlias))
        {
            throw new InvalidOperationException($"Default search provider '{DefaultPublishedContentProviderAlias}' is not registered.");
        }

        foreach (var registration in Providers)
        {
            bool found = false;
            foreach (MappingDefinition mapping in mappings.Values)
            {
                if (mapping.Export && mapping.EntityType == registration.Value.EntityType)
                {
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException($"Search provider '{registration.Key}' has no exported {registration.Value.EntityType.Name} mapping.");
            }

            if (registration.Value.ContentOptions?.DocumentTypeAliases is { } aliases)
            {
                if (aliases.Length == 0)
                {
                    throw new InvalidOperationException("DocumentTypeAliases must be omitted or contain at least one Content mapping.");
                }

                for (int i = 0; i < aliases.Length; i++)
                {
                    string alias = aliases[i];
                    if (string.IsNullOrWhiteSpace(alias) || !mappings.TryGetValue(alias, out MappingDefinition? mapping) || mapping.EntityType != typeof(Content) || !mapping.Export)
                    {
                        throw new InvalidOperationException($"Search provider '{registration.Key}' references an unknown or non-Content mapping '{alias}'.");
                    }

                    aliases[i] = mapping.DocumentTypeAlias;
                }
            }
        }
    }
}
