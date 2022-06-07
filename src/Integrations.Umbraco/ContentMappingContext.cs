using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines the context of a PublishedContent that is to be mapped
/// </summary>
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

    /// <summary>
    /// These are the languages that content was published in.
    /// </summary>
    public List<string> CulturesToPublish { get; }

    /// <summary>
    /// Gets the service object of the specified type.
    /// </summary>
    public object? GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }
}


