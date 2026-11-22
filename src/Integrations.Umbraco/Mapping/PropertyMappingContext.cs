using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>
/// Read-only source information for conversion of a built-in destination property.
/// </summary>
public class PropertyMappingContext
{
    private readonly MappingOperation _operation;

    internal PropertyMappingContext(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        IPublishedProperty property,
        IReadOnlyList<string> culturesToPublish,
        string destinationProperty,
        MappingOperation operation)
    {
        PublishedContent = publishedContent;
        SourceElement = sourceElement;
        Property = property;
        CulturesToPublish = culturesToPublish;
        DestinationProperty = destinationProperty;
        _operation = operation;
    }

    /// <summary>
    /// The owning published document, retained when converting nested elements.
    /// </summary>
    public IPublishedContent PublishedContent { get; }

    /// <summary>
    /// The element containing the source property.
    /// </summary>
    public IPublishedElement SourceElement { get; }

    /// <summary>
    /// The source property, including its editor and localized values.
    /// </summary>
    public IPublishedProperty Property { get; }

    /// <summary>
    /// The published cultures to include in the destination value, or the site's languages for invariant
    /// content.
    /// </summary>
    public IReadOnlyList<string> CulturesToPublish { get; }

    /// <summary>
    /// The selected built-in destination property.
    /// </summary>
    public string DestinationProperty { get; }

    /// <summary>
    /// Services belonging to the current export operation.
    /// </summary>
    public IServiceProvider Services => _operation.Services;

    /// <summary>
    /// Cancellation of the current export operation.
    /// </summary>
    public CancellationToken CancellationToken => _operation.CancellationToken;

    internal T Resolve<T>() where T : class
    {
        return _operation.Resolve<T>();
    }

    internal Task<string> DefaultCultureAsync()
    {
        return _operation.DefaultCultureAsync([.. CulturesToPublish]);
    }
}
