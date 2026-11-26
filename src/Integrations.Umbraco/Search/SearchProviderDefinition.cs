using System;
using System.Collections.Concurrent;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class SearchProviderDefinition
{
    internal ConcurrentDictionary<string, SearchFieldDefinition> ResolvedFields { get; } = new(StringComparer.Ordinal);

    internal object Options { get; }
    internal Type EntityType { get; }
    internal ContentSearchProviderOptions? ContentOptions { get; }

    internal SearchProviderDefinition(object options, ContentSearchProviderOptions? contentOptions)
    {
        Options = options;
        ContentOptions = contentOptions;
        EntityType = contentOptions == null ? typeof(Product) : typeof(Content);
    }

    internal string Location(string providerAlias)
    {
        return Options switch
        {
            ContentSearchProviderOptions content when !string.IsNullOrWhiteSpace(content.DisplayedAtLocation) => content
                .DisplayedAtLocation,
            ProductSearchProviderOptions product when !string.IsNullOrWhiteSpace(product.DisplayedAtLocation) => product
                .DisplayedAtLocation,
            _ => "Umbraco search: " + providerAlias
        };
    }
}
