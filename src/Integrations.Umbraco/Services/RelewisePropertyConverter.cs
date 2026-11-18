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
        => ConvertProperties(properties, cultures, null, null, null, default, null).Data;

    public IReadOnlyDictionary<string, DataValue?> Convert(IPublishedContent publishedContent, IPublishedElement sourceElement, string[] cultures, IServiceProvider services, System.Threading.CancellationToken cancellationToken = default)
        => ConvertProperties(sourceElement.Properties, cultures, publishedContent, sourceElement, services, cancellationToken, null).Data;

    internal PropertyConversion ConvertProperty(IPublishedContent publishedContent, IPublishedElement sourceElement,
        IPublishedProperty property, string[] cultures, MappingOperation operation, IRelewisePropertyValueConverter? selected)
        => ConvertProperties([property], cultures, publishedContent, sourceElement, operation.Services, operation.CancellationToken, selected);

    private PropertyConversion ConvertProperties(IEnumerable<IPublishedProperty> properties, string[] cultures,
        IPublishedContent? publishedContent, IPublishedElement? sourceElement, IServiceProvider? services,
        System.Threading.CancellationToken cancellationToken, IRelewisePropertyValueConverter? selected)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        if (cultures == null) throw new ArgumentNullException(nameof(cultures));
        if (cultures.Length == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(cultures));

        var dataKeys = new Dictionary<string, DataValue?>();
        bool handled = false;

        foreach (IPublishedProperty property in properties)
        {
            if (property.PropertyType.VariesByCulture())
            {
                var localizedProperties = new Dictionary<string, Dictionary<string, DataValue?>>();

                foreach (string culture in cultures)
                {
                    var localizedDataKeys = new Dictionary<string, DataValue?>();
                    Convert(property, culture, localizedDataKeys, publishedContent, sourceElement, services, cancellationToken, selected, ref handled);
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
                            // We only support having strings as multilingual, so we need to check if the values are strings or not.
                            if (values.All(x => x.Value?.Type == DataValue.DataValueTypes.String))
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
                Convert(property, cultures.First(), dataKeys, publishedContent, sourceElement, services, cancellationToken, selected, ref handled);
            }
        }

        return new PropertyConversion(dataKeys, handled);
    }

    private void Convert(IPublishedProperty property, string culture, Dictionary<string, DataValue?> dataKeys,
        IPublishedContent? publishedContent, IPublishedElement? sourceElement, IServiceProvider? services,
        System.Threading.CancellationToken cancellationToken, IRelewisePropertyValueConverter? selected, ref bool handled)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = new RelewisePropertyConverterContext(property, culture, dataKeys, publishedContent, sourceElement, services, cancellationToken);
        if (selected != null)
        {
            if (!selected.CanHandle(context)) throw new InvalidOperationException($"Selected converter {selected.GetType().Name} cannot handle '{property.Alias}' for culture '{culture}'.");
            handled = true;
            selected.Convert(context);
            return;
        }
        foreach (IRelewisePropertyValueConverter converter in converters)
        {
            if (converter.CanHandle(context))
            {
                handled = true;
                converter.Convert(context);
            }
        }
    }
}
internal sealed record PropertyConversion(IReadOnlyDictionary<string, DataValue?> Data, bool Handled);
