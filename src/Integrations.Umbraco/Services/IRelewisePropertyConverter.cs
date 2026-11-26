using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

/// <summary>
/// Converts a collection of published properties to a dictionary of data values, supporting multiple cultures.
/// </summary>
public interface IRelewisePropertyConverter
{
    /// <summary>
    /// Convert contextual properties asynchronously, retaining the owning document and service
    /// scope.
    /// </summary>
    Task<IReadOnlyDictionary<string, DataValue?>> ConvertAsync(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        string[] cultures,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Convert(
                publishedContent,
                sourceElement,
                cultures,
                services,
                cancellationToken));
    }

    /// <summary>
    /// Convert properties asynchronously. Existing aggregate implementations retain their synchronous
    /// behavior.
    /// </summary>
    Task<IReadOnlyDictionary<string, DataValue?>> ConvertAsync(
        IEnumerable<IPublishedProperty> properties,
        string[] cultures,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Convert(properties, cultures));
    }

    /// <summary>
    /// Convert an element while retaining its owning document and the current service scope.
    /// </summary>
    /// <remarks>The default implementation preserves compatibility with custom aggregate converters implementing only
    /// the property-based API.</remarks>
    IReadOnlyDictionary<string, DataValue?> Convert(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        string[] cultures,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        return Convert(sourceElement.Properties, cultures);
    }

    /// <summary>
    /// Converts the given collection of properties to a read-only dictionary of data values.
    /// The conversion supports multiple cultures.
    /// </summary>
    /// <param name="properties">A collection of published properties to be converted.</param>
    /// <param name="cultures">An array of culture codes to consider during conversion.</param>
    /// <returns>A read-only dictionary where keys are property names and values are data values.</returns>
    IReadOnlyDictionary<string, DataValue?> Convert(IEnumerable<IPublishedProperty> properties, string[] cultures);

    /// <summary>
    /// Converts the given collection of properties to a read-only dictionary of data values.
    /// The conversion supports multiple cultures.
    /// </summary>
    /// <param name="properties">A collection of published properties to be converted.</param>
    /// <param name="culture">The culture code for which the property should be converted.</param>
    /// <returns>A read-only dictionary where keys are property names and values are data values.</returns>
    public IReadOnlyDictionary<string, DataValue?> Convert(
        IEnumerable<IPublishedProperty> properties,
        string culture)
    {
        return Convert(properties, [culture]);
    }
}
