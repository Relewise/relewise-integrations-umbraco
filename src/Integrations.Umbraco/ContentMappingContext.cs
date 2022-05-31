using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

public class ContentMappingContext : IServiceProvider
{
    private readonly IServiceProvider _serviceProvider;

    internal ContentMappingContext(
        IPublishedContent publishedContent,
        ContentUpdate contentUpdate,
        List<string> culturesToPublish, 
        IServiceProvider serviceProvider)
    {
        PublishedContent = publishedContent;
        ContentUpdate = contentUpdate;
        CulturesToPublish = culturesToPublish;

        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Content that needs to be mapped
    /// </summary>
    public IPublishedContent PublishedContent { get; }

    /// <summary>
    /// Holds the basic mapping to ensure basic information is handled uniform during the content export
    /// </summary>
    public ContentUpdate ContentUpdate { get; }

    public List<string> CulturesToPublish { get; }

    public object? GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }
}


