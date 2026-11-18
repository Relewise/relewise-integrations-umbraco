using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco;

/// <summary>Named query providers, independently configured from indexing.</summary>
public class SearchProviderOptions
{
    internal SearchProviderOptions() { }
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
        if (!Providers.TryAdd(providerAlias, definition)) throw new InvalidOperationException($"Search provider '{providerAlias}' is already registered.");
    }
    internal void Validate(IReadOnlyDictionary<string, MappingDefinition> mappings)
    {
        if (DefaultPublishedContentProviderAlias != null && !Providers.ContainsKey(DefaultPublishedContentProviderAlias))
            throw new InvalidOperationException($"Default search provider '{DefaultPublishedContentProviderAlias}' is not registered.");
        foreach (var registration in Providers)
        {
            bool found = false;
            foreach (MappingDefinition mapping in mappings.Values)
                if (mapping.Export && mapping.EntityType == registration.Value.EntityType) found = true;
            if (!found) throw new InvalidOperationException($"Search provider '{registration.Key}' has no exported {registration.Value.EntityType.Name} mapping.");
            if (registration.Value.ContentOptions?.DocumentTypeAliases is { } aliases)
            {
                if (aliases.Length == 0) throw new InvalidOperationException("DocumentTypeAliases must be omitted or contain at least one Content mapping.");
                for (int i = 0; i < aliases.Length; i++)
                {
                    string alias = aliases[i];
                    if (string.IsNullOrWhiteSpace(alias) || !mappings.TryGetValue(alias, out MappingDefinition? mapping) || mapping.EntityType != typeof(Content) || !mapping.Export)
                        throw new InvalidOperationException($"Search provider '{registration.Key}' references an unknown or non-Content mapping '{alias}'.");
                    aliases[i] = mapping.DocumentTypeAlias;
                }
            }
        }
    }
}

/// <summary>Runtime information for request customization; services belong to the current operation.</summary>
public class SearchRequestContext
{
    internal SearchRequestContext(IServiceProvider services, User user, string culture)
    {
        Services = services;
        User = user;
        Culture = culture;
    }
    /// <summary>Services in the current request or background-operation scope.</summary>
    public IServiceProvider Services { get; }
    /// <summary>The user resolved by the same locator used for behavioral tracking.</summary>
    public User User { get; }
    /// <summary>The effective Relewise request language.</summary>
    public string Culture { get; }
}

/// <summary>Query settings for mapped Content documents.</summary>
public class ContentSearchProviderOptions
{
    internal ContentSearchProviderOptions() { }
    internal Action<ContentSearchRequest, SearchRequestContext>? Configure { get; private set; }
    /// <summary>Source Content Document Types to include; null includes all exported Content mappings.</summary>
    public string[]? DocumentTypeAliases { get; set; }
    /// <summary>Stable analytics/merchandising label. Defaults to Umbraco search: followed by the provider alias.</summary>
    public string? DisplayedAtLocation { get; set; }
    /// <summary>How to handle a requested segment. Defaults to rejecting unsupported segment queries.</summary>
    public UnsupportedSegmentBehavior UnsupportedSegmentBehavior { get; set; } = UnsupportedSegmentBehavior.Throw();
    /// <summary>Customize a fresh request for each execution. Mandatory access, mapping, identity, and paging constraints remain enforced.</summary>
    public void ConfigureRequest(Action<ContentSearchRequest, SearchRequestContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (Configure != null) throw new InvalidOperationException("Request customization has already been configured.");
        Configure = configure;
    }
}

/// <summary>Query settings for the single Product mapping.</summary>
public class ProductSearchProviderOptions
{
    internal ProductSearchProviderOptions() { }
    internal Action<ProductSearchRequest, SearchRequestContext>? Configure { get; private set; }
    /// <summary>Require an available published Umbraco page. Defaults to true; disabling does not change Product administrative state.</summary>
    /// <remarks>Callers disabling this requirement must handle results without a published page.</remarks>
    public bool RequirePublishedPage { get; set; } = true;
    /// <summary>Stable analytics/merchandising label. Defaults to Umbraco search: followed by the provider alias.</summary>
    public string? DisplayedAtLocation { get; set; }
    /// <summary>How to handle a requested segment. Defaults to rejecting unsupported segment queries.</summary>
    public UnsupportedSegmentBehavior UnsupportedSegmentBehavior { get; set; } = UnsupportedSegmentBehavior.Throw();
    /// <summary>Customize a fresh request for each execution. Mandatory access, mapping, identity, and paging constraints remain enforced.</summary>
    public void ConfigureRequest(Action<ProductSearchRequest, SearchRequestContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (Configure != null) throw new InvalidOperationException("Request customization has already been configured.");
        Configure = configure;
    }
}

internal sealed class SearchProviderDefinition
{
    internal object Options { get; }
    internal Type EntityType { get; }
    internal ContentSearchProviderOptions? ContentOptions { get; }
    internal SearchProviderDefinition(object options, ContentSearchProviderOptions? contentOptions)
    {
        Options = options;
        ContentOptions = contentOptions;
        EntityType = contentOptions == null ? typeof(Product) : typeof(Content);
    }
    internal string Location(string providerAlias) => Options switch
    {
        ContentSearchProviderOptions content when !string.IsNullOrWhiteSpace(content.DisplayedAtLocation) => content.DisplayedAtLocation,
        ProductSearchProviderOptions product when !string.IsNullOrWhiteSpace(product.DisplayedAtLocation) => product.DisplayedAtLocation,
        _ => "Umbraco search: " + providerAlias
    };
}
