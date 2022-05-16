using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco;

public class RelewisePropertyConverterContext
{
    private readonly Dictionary<string, DataValue> _dataKeys;

    internal RelewisePropertyConverterContext(IPublishedProperty property, string culture, Dictionary<string, DataValue> dataKeys)
    {
        _dataKeys = dataKeys;
        Property = property;
        Culture = culture;
    }

    public IPublishedProperty Property { get; }
    public string Culture { get; }

    public void Add(string key, DataValue value) => _dataKeys.Add(key, value);
}