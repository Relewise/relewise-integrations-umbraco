using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines the Relewise Umbraco Integration configuration
/// </summary>
public class RelewiseUmbracoConfiguration
{
    private readonly HashSet<string> _trackableDocTypes;
    private readonly HashSet<string> _autoMappedContentTypes;
    private readonly Dictionary<string, IContentTypeMapping?> _customMappers;

#pragma warning disable 1591
    public RelewiseUmbracoConfiguration(IServiceProvider provider)
#pragma warning restore 1591
    {
        var options = new RelewiseUmbracoOptionsBuilder();

        foreach (Configure configure in provider.GetServices<Configure>())
            configure(options, provider);

        _trackableDocTypes = options.ContentBuilders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _autoMappedContentTypes = options
            .ContentBuilders
            .Where(x => x.Value == null || x.Value.AutoMapContent)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _customMappers = options
            .ContentBuilders
            .Where(x => x.Value is { Mapper: { } })
            .ToDictionary(x => x.Key, x => x.Value?.Mapper, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contains all contentType aliases that are being tracked
    /// </summary>
    public IReadOnlyCollection<string> TrackedContentTypes => _trackableDocTypes;

    /// <summary>
    /// Contains all contentType aliases that are being exported
    /// </summary>
    public IReadOnlyCollection<string> ExportedContentTypes => _autoMappedContentTypes.Concat(_customMappers.Keys).ToArray();

    /// <summary>
    /// If the contents type is registered, this will tell that it can be mapped by the automapping or by a mapper
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool CanMap(IContent content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        return CanMap(content.ContentType.Alias);
    }

    /// <summary>
    /// If contentType is registered, this will tell that it can be mapped by the automapping or by a mapper
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool CanMap(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));
        
        return _autoMappedContentTypes.Contains(contentType) || _customMappers.ContainsKey(contentType);
    }

    /// <summary>
    /// Check if the content is registered to be tracked
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool IsTrackable(IPublishedElement content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        return IsTrackable(content.ContentType.Alias);
    }

    private bool IsTrackable(string docAlias)
    {
        return _trackableDocTypes.Contains(docAlias);
    }

    /// <summary>
    /// Find a mapper for a ContentType Alias, if one exists
    /// </summary>
    /// <param name="contentType"></param>
    /// <param name="mapping"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool TryGetMapper(string contentType, out IContentTypeMapping? mapping)
    {
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));
        
        return _customMappers.TryGetValue(contentType, out mapping) && mapping != null;
    }

    internal delegate void Configure(RelewiseUmbracoOptionsBuilder builder, IServiceProvider services);
}