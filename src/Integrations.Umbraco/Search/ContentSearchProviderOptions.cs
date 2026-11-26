using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco.Search;

/// <summary>
/// Query settings for mapped Content documents.
/// </summary>
public class ContentSearchProviderOptions
{
    internal ContentSearchProviderOptions()
    {
    }

    internal Action<ContentSearchRequest, SearchRequestContext>? Configure { get; private set; }

    internal Dictionary<string, SearchFieldDefinition> Fields { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Bind a query field to an existing built-in Content property, independently of export mapping.
    /// </summary>
    /// <typeparam name="TValue">The selected SDK property's type.</typeparam>
    /// <param name="destinationProperty">The direct SDK property to query.</param>
    /// <param name="fieldName">The name used in Umbraco filters, facets and sorters.</param>
    public void Map<TValue>(Expression<Func<Content, TValue>> destinationProperty, string fieldName)
    {
        SearchFieldRegistration.Map(Fields, destinationProperty, fieldName);
    }

    /// <summary>
    /// Bind a query field to existing Data. Supports scalar strings, Booleans and numbers,
    /// and string/numeric arrays. Array sorting and numeric array ranges are not supported.
    /// </summary>
    /// <typeparam name="TValue">The stored scalar type or array type; numeric Data uses SDK doubles.</typeparam>
    /// <param name="fieldName">The name used in Umbraco filters, facets and sorters.</param>
    /// <param name="configure">Optional stored Data key override. This does not export Data.</param>
    public void MapData<TValue>(string fieldName, Action<SearchDataMappingOptions>? configure = null)
    {
        SearchFieldRegistration.MapData<TValue>(Fields, fieldName, configure);
    }

    /// <summary>
    /// Source Content Document Types to include; null includes all exported Content mappings.
    /// </summary>
    public string[]? DocumentTypeAliases { get; set; }

    /// <summary>
    /// Stable analytics/merchandising label. Defaults to Umbraco search: followed by the provider
    /// alias.
    /// </summary>
    public string? DisplayedAtLocation { get; set; }

    /// <summary>
    /// How to handle a requested segment. Defaults to rejecting unsupported segment queries.
    /// </summary>
    public UnsupportedSegmentBehavior UnsupportedSegmentBehavior { get; set; } = UnsupportedSegmentBehavior.Throw();

    /// <summary>
    /// Customize a fresh request for each execution. Mandatory access, mapping, identity, and paging
    /// constraints remain enforced.
    /// </summary>
    public void ConfigureRequest(Action<ContentSearchRequest, SearchRequestContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (Configure != null)
        {
            throw new InvalidOperationException("Request customization has already been configured.");
        }

        Configure = configure;
    }
}
