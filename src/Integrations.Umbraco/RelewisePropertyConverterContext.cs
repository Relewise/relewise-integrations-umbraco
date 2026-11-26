using System;
using System.Collections.Generic;
using System.Threading;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines the context of a <see cref="IPublishedProperty"/> that is to be mapped
/// </summary>
public class RelewisePropertyConverterContext
{
    private readonly Dictionary<string, DataValue?> _dataKeys;

    internal RelewisePropertyConverterContext(
        IPublishedProperty property,
        string culture,
        Dictionary<string, DataValue?> dataKeys)
    {
        _dataKeys = dataKeys;
        Property = property;
        Culture = culture;
    }

    internal RelewisePropertyConverterContext(
        IPublishedProperty property,
        string culture,
        Dictionary<string, DataValue?> dataKeys,
        IPublishedContent? publishedContent,
        IPublishedElement? sourceElement,
        IServiceProvider? services,
        CancellationToken cancellationToken)
        : this(property, culture, dataKeys)
    {
        PublishedContent = publishedContent;
        SourceElement = sourceElement;
        Services = services;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The owning document, when supplied by contextual export. Legacy property-only calls have no
    /// owner.
    /// </summary>
    public IPublishedContent? PublishedContent { get; }

    /// <summary>
    /// The document or nested element containing the source property.
    /// </summary>
    public IPublishedElement? SourceElement { get; }

    /// <summary>
    /// Services for this conversion operation, when contextual conversion is used.
    /// </summary>
    public IServiceProvider? Services { get; }

    /// <summary>
    /// Cancellation for the export operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Content that needs to be mapped
    /// </summary>
    public IPublishedProperty Property { get; }

    /// <summary>
    /// The languages that content was published in.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Adds a property to the Data-field on the content
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Add(string key, DataValue? value)
    {
        _dataKeys.Add(key, value);
    }
}
