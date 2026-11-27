#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Services;

internal class RelewisePropertyConverter(
    IEnumerable<IRelewisePropertyValueConverter> converters,
    IServiceProvider? provider = null)
    : IRelewisePropertyConverter
{
    public IReadOnlyDictionary<string, DataValue?> Convert(
        IEnumerable<IPublishedProperty> properties,
        string[] cultures)
    {
        return ConvertAsync(properties, cultures).GetAwaiter().GetResult();
    }

    public IReadOnlyDictionary<string, DataValue?> Convert(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        string[] cultures,
        IServiceProvider services,
        CancellationToken token = default)
    {
        return ConvertAsync(publishedContent, sourceElement, cultures, services, token)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<IReadOnlyDictionary<string, DataValue?>> ConvertAsync(
        IEnumerable<IPublishedProperty> properties,
        string[] cultures,
        CancellationToken token = default)
    {
        if (provider == null)
        {
            return (await ConvertPropertiesAsync(properties, cultures, null, null, null, null, token)).Data;
        }

        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        return (await ConvertPropertiesAsync(
            properties,
            cultures,
            null,
            null,
            scope.ServiceProvider,
            null,
            token)).Data;
    }

    public async Task<IReadOnlyDictionary<string, DataValue?>> ConvertAsync(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        string[] cultures,
        IServiceProvider services,
        CancellationToken token = default)
    {
        return (await ConvertPropertiesAsync(
            sourceElement.Properties,
            cultures,
            publishedContent,
            sourceElement,
            services,
            null,
            token)).Data;
    }

    internal async Task<IReadOnlyDictionary<string, DataValue?>> ConvertElementAsync(
        IPublishedContent? publishedContent,
        IPublishedElement sourceElement,
        string[] cultures,
        IServiceProvider services,
        CancellationToken token)
    {
        return (await ConvertPropertiesAsync(
            sourceElement.Properties,
            cultures,
            publishedContent,
            sourceElement,
            services,
            null,
            token)).Data;
    }

    internal Task<PropertyConversion> ConvertPropertyAsync(
        IPublishedContent publishedContent,
        IPublishedElement sourceElement,
        IPublishedProperty property,
        string[] cultures,
        MappingOperation operation,
        IAsyncRelewisePropertyValueConverter? selected)
    {
        return ConvertPropertiesAsync(
            [property],
            cultures,
            publishedContent,
            sourceElement,
            operation.Services,
            selected,
            operation.CancellationToken);
    }

    private async Task<PropertyConversion> ConvertPropertiesAsync(
        IEnumerable<IPublishedProperty> properties,
        string[] cultures,
        IPublishedContent? publishedContent,
        IPublishedElement? sourceElement,
        IServiceProvider? services,
        IAsyncRelewisePropertyValueConverter? selected,
        CancellationToken token)
    {
        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        if (cultures == null)
        {
            throw new ArgumentNullException(nameof(cultures));
        }

        if (cultures.Length == 0)
        {
            throw new ArgumentException("Value cannot be an empty collection.", nameof(cultures));
        }

        var dataKeys = new Dictionary<string, DataValue?>();
        var handled = false;

        foreach (IPublishedProperty property in properties)
        {
            if (property.PropertyType.VariesByCulture())
            {
                var localizedProperties = new Dictionary<string, Dictionary<string, DataValue?>>();

                foreach (string culture in cultures)
                {
                    var localizedDataKeys = new Dictionary<string, DataValue?>();

                    handled |= await ConvertPropertyValueAsync(
                        property,
                        culture,
                        localizedDataKeys,
                        publishedContent,
                        sourceElement,
                        services,
                        selected,
                        token);
                    localizedProperties.Add(culture, localizedDataKeys);
                }

                IEnumerable<(string Lang, string Key, DataValue? Value)> valueTuples = localizedProperties
                    .SelectMany(x => x.Value.Select(y => (Lang: x.Key, Key: y.Key, y.Value)));

                IEnumerable<IGrouping<string, (string Lang, string Key, DataValue? Value)>> groupBy = valueTuples
                    .GroupBy(x => x.Key);

                foreach (IGrouping<string, (string Lang, string Key, DataValue? Value)> values in groupBy)
                {
                    if (values.Count() > 1)
                    {
                        if (values.First().Value?.Type == DataValue.DataValueTypes.StringList)
                        {
                            dataKeys.Add(
                                values.Key,
                                new MultilingualCollection(
                                    values
                                        .Select(x => new MultilingualCollection.Value(x.Lang, x.Value))
                                        .ToArray()));
                        }
                        else
                        {
                            // We only support having strings as multilingual, so we need to check if the values are
                            // strings or not.
                            if (values.All(x => x.Value?.Type == DataValue.DataValueTypes.String))
                            {
                                dataKeys.Add(
                                    values.Key,
                                    new Multilingual(
                                        values.Select(x => new Multilingual.Value(x.Lang, x.Value))
                                            .ToArray()));
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
                handled |= await ConvertPropertyValueAsync(
                    property,
                    cultures.First(),
                    dataKeys,
                    publishedContent,
                    sourceElement,
                    services,
                    selected,
                    token);
            }
        }

        return new PropertyConversion(dataKeys, handled);
    }

    private async ValueTask<bool> ConvertPropertyValueAsync(
        IPublishedProperty property,
        string culture,
        Dictionary<string, DataValue?> dataKeys,
        IPublishedContent? publishedContent,
        IPublishedElement? sourceElement,
        IServiceProvider? services,
        IAsyncRelewisePropertyValueConverter? selected,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var context = new RelewisePropertyConverterContext(
            property,
            culture,
            dataKeys,
            publishedContent,
            sourceElement,
            services,
            token);

        if (selected != null)
        {
            if (!selected.CanHandle(context))
            {
                throw new InvalidOperationException(
                    $"Selected converter {selected.GetType().Name} cannot handle '{property.Alias}" +
                    $"' for culture '{culture}'.");
            }

            await selected.ConvertAsync(context, token);

            return true;
        }

        var handled = false;
        var customApplied = false;
        IAsyncRelewisePropertyValueConverter? custom =
            (services?.GetServices<IAsyncRelewisePropertyValueConverter>() ?? [])
            .Reverse()
            .OrderBy(converter => converter is IDefaultPropertyValueConverter)
            .FirstOrDefault(converter => converter.CanHandle(context));

        foreach (IRelewisePropertyValueConverter converter in converters)
        {
            if (!converter.CanHandle(context))
            {
                continue;
            }

            handled = true;

            if (converter is IDefaultPropertyValueConverter && custom != null)
            {
                if (!customApplied)
                {
                    await custom.ConvertAsync(context, token);
                }

                customApplied = true;
            }
            else if (converter is IAsyncRelewisePropertyValueConverter asynchronous)
            {
                await asynchronous.ConvertAsync(context, token);
            }
            else
            {
                converter.Convert(context);
            }
        }

        if (custom != null && !customApplied)
        {
            await custom.ConvertAsync(context, token);
            handled = true;
        }

        token.ThrowIfCancellationRequested();

        return handled;
    }
}
