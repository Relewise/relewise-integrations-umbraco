using System;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

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
