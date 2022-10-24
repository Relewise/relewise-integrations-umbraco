using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines the context of a <see cref="IPublishedProperty"/> that is to be mapped
/// </summary>
public class RelewisePropertyConverterContext
{
    private readonly Dictionary<string, DataValue?> _dataKeys;

    internal RelewisePropertyConverterContext(IPublishedProperty property, string culture, Dictionary<string, DataValue?> dataKeys)
    {
        _dataKeys = dataKeys;
        Property = property;
        Culture = culture;
    }

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
    public void Add(string key, DataValue? value) => _dataKeys.Add(key, value);
}