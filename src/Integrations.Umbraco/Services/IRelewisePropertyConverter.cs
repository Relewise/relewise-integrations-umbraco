using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

/// <summary>
/// Converts a collection of published properties to a dictionary of data values, supporting multiple cultures.
/// </summary>
public interface IRelewisePropertyConverter
{
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
    public IReadOnlyDictionary<string, DataValue?> Convert(IEnumerable<IPublishedProperty> properties, string culture) => Convert(properties, [culture]);
}