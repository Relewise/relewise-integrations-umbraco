﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

public class RelewiseUmbracoConfiguration
{
    private readonly HashSet<string> _trackableDocTypes;
    private readonly HashSet<string> _autoMappedContentTypes;
    private readonly Dictionary<string, IContentTypeMapping?> _customMappers;

    public RelewiseUmbracoConfiguration(IServiceProvider provider)
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

    public bool CanMap(string contentType)
    {
        return _autoMappedContentTypes.Contains(contentType) || _customMappers.ContainsKey(contentType);
    }

    public bool IsTrackable(IPublishedElement content)
    {
        return IsTrackable(content.ContentType.Alias);
    }

    public bool IsTrackable(IContent content)
    {
        return IsTrackable(content.ContentType.Alias);
    }

    private bool IsTrackable(string docAlias)
    {
        return _trackableDocTypes.Contains(docAlias);
    }

    public bool TryGetMapper(string contentType, out IContentTypeMapping? mapping)
    {
        return _customMappers.TryGetValue(contentType, out mapping) && mapping != null;
    }

    public delegate void Configure(RelewiseUmbracoOptionsBuilder builder, IServiceProvider services);
}