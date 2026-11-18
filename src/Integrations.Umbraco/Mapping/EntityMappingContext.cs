using System;
using System.Collections.Generic;
using System.Threading;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>Context for a scoped custom entity mapper.</summary>
/// <typeparam name="TEntity">The destination SDK entity.</typeparam>
public class EntityMappingContext<TEntity> where TEntity : class
{
    internal EntityMappingContext(IPublishedContent publishedContent, TEntity entity, IReadOnlyList<string> culturesToPublish, MappingOperation operation)
    {
        PublishedContent = publishedContent;
        Entity = entity;
        CulturesToPublish = culturesToPublish;
        Services = operation.Services;
        CancellationToken = operation.CancellationToken;
    }
    /// <summary>The published source document.</summary>
    public IPublishedContent PublishedContent { get; }
    /// <summary>The typed destination. Its identity and integration metadata remain controlled by the integration.</summary>
    public TEntity Entity { get; }
    /// <summary>Published cultures included in this export.</summary>
    public IReadOnlyList<string> CulturesToPublish { get; }
    /// <summary>Services for the current export operation.</summary>
    public IServiceProvider Services { get; }
    /// <summary>Cancellation for the current operation.</summary>
    public CancellationToken CancellationToken { get; }
}
