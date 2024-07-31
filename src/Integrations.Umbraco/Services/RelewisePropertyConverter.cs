using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Services;

internal class RelewisePropertyConverter(IEnumerable<IRelewisePropertyValueConverter> converters)
    : IRelewisePropertyConverter
{
    public IReadOnlyDictionary<string, DataValue?> Convert(IEnumerable<IPublishedProperty> properties, string[] cultures)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (cultures == null) throw new ArgumentNullException(nameof(cultures));
        if (cultures.Length == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(cultures));

        var dataKeys = new Dictionary<string, DataValue?>();

        foreach (IPublishedProperty property in properties)
        {
            if (property.PropertyType.VariesByCulture())
            {
                var localizedProperties = new Dictionary<string, Dictionary<string, DataValue?>>();

                foreach (string culture in cultures)
                {
                    var localizedDataKeys = new Dictionary<string, DataValue?>();
                    Convert(property, culture, localizedDataKeys);
                    localizedProperties.Add(culture, localizedDataKeys);
                }

                IEnumerable<(string Lang, string Key, DataValue? Value)> valueTuples = localizedProperties
                    .SelectMany(x => x.Value.Select(y => (Lang: x.Key, Key: y.Key, y.Value)));

                IEnumerable<IGrouping<string, (string Lang, string Key, DataValue? Value)>> groupBy = valueTuples
                    .GroupBy(x => x.Key);

                foreach (var values in groupBy)
                {
                    if (values.Count() > 1)
                    {
                        if (values.First().Value?.Type == DataValue.DataValueTypes.StringList)
                        {
                            dataKeys.Add(values.Key, new MultilingualCollection(values.Select(x => new MultilingualCollection.Value(x.Lang, x.Value)).ToArray()));
                        }
                        else
                        {
                            if (values.First().Value?.Type == DataValue.DataValueTypes.String)
                            {
                                dataKeys.Add(values.Key, new Multilingual(values.Select(x => new Multilingual.Value(x.Lang, x.Value)).ToArray()));
                            }
                            else
                            {
                                foreach ((string Lang, string Key, DataValue? Value) dataValue in values)
                                {
                                    dataKeys.Add($"{dataValue.Key}_{dataValue.Lang}", dataValue.Value);
                                }
                            }
                        }
                    }
                    else
                    {
                        dataKeys.Add(values.Key, values.First().Value);
                    }
                }
            }
            else
            {
                Convert(property, cultures.First(), dataKeys);
            }
        }

        return dataKeys;
    }

    private void Convert(IPublishedProperty property, string culture, Dictionary<string, DataValue?> dataKeys)
    {
        var context = new RelewisePropertyConverterContext(property, culture, dataKeys);

        foreach (IRelewisePropertyValueConverter converter in converters)
        {
            if (converter.CanHandle(context))
                converter.Convert(context);
        }
    }
}