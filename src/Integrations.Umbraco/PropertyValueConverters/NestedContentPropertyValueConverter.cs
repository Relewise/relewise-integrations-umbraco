using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class NestedContentPropertyValueConverter(IServiceProvider serviceProvider) : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.NestedContent");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        var elementItems = context.Property.GetValue<IEnumerable<IPublishedElement>?>(context.Culture)?.ToArray();

        if (elementItems != null)
        {
            // NOTE: This needs to be resolved manually, since it would cause a circular dependency if injected through constructor
            var propertyConverter = serviceProvider.GetRequiredService<IRelewisePropertyConverter>();

            var properties = new List<DataValue?>();

            foreach (IPublishedElement prop in elementItems)
            {
                IReadOnlyDictionary<string, DataValue?> converted = propertyConverter.Convert(prop.Properties, context.Culture);

                properties.AddRange(converted.Values);
            }

            if (properties.Count == 0)
                return;

            string[] stringCollection = properties
                .Select(x => x?.Value?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray();

            context.Add(context.Property.Alias, new DataValue(stringCollection));
        }
    }
}