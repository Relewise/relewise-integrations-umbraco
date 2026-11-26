#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class NestedContentPropertyValueConverter(IServiceProvider serviceProvider)
    : IDefaultPropertyValueConverter, IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.NestedContent");
    }

    /// <inheritdoc/>
    [Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
    public void Convert(RelewisePropertyConverterContext context)
    {
        ConvertAsync(context, context.CancellationToken).GetAwaiter().GetResult();
    }

    public async ValueTask ConvertAsync(
        RelewisePropertyConverterContext context,
        CancellationToken cancellationToken)
    {
        IPublishedElement[]? elementItems =
            context.Property.GetValue<IEnumerable<IPublishedElement>?>(context.Culture)?.ToArray();

        if (elementItems != null)
        {
            // NOTE: This needs to be resolved manually, since it would cause a circular dependency if injected through
            // constructor
            var propertyConverter =
                (context.Services ?? serviceProvider).GetRequiredService<IRelewisePropertyConverter>();

            var properties = new List<DataValue?>();

            foreach (IPublishedElement prop in elementItems)
            {
                IReadOnlyDictionary<string, DataValue?> converted =
                    propertyConverter is RelewisePropertyConverter contextual && context.Services != null
                        ? await contextual.ConvertElementAsync(
                            context.PublishedContent,
                            prop,
                            [context.Culture],
                            context.Services,
                            cancellationToken)
                        : context.PublishedContent != null && context.Services != null
                            ? await propertyConverter.ConvertAsync(
                                context.PublishedContent,
                                prop,
                                [context.Culture],
                                context.Services,
                                cancellationToken)
                            : await propertyConverter.ConvertAsync(
                                prop.Properties,
                                [context.Culture],
                                cancellationToken);

                properties.AddRange(converted.Values);
            }

            if (properties.Count == 0)
            {
                return;
            }

            string[] stringCollection = properties
                .Select(x => x?.Value?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray();

            context.Add(context.Property.Alias, new DataValue(stringCollection));
        }
    }
}
