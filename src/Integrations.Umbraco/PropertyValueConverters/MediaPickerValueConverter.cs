#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Extracts media URLs from Umbraco 19's single and multiple media pickers.
/// </summary>
public class MediaPickerValueConverter : IDefaultPropertyValueConverter, IRelewisePropertyValueConverter
{
    /// <inheritdoc cref="IRelewisePropertyValueConverter.CanHandle" />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias is "Umbraco.MediaPicker3" or "Umbraco.MediaPicker.Single"
            or "Umbraco.MultipleMediaPicker";
    }

    /// <inheritdoc />
    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Convert(context);

        return default;
    }

    /// <inheritdoc/>
    [Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
    public void Convert(RelewisePropertyConverterContext context)
    {
        var isMultiple = context.Property.PropertyType.EditorAlias != "Umbraco.MediaPicker.Single";

        if (isMultiple)
        {
            var value = new List<string>();
            IEnumerable<IPublishedContent> items =
                context.Property.GetValue<IEnumerable<IPublishedContent>>(context.Culture) ??
                Array.Empty<IPublishedContent>();

            foreach (IPublishedContent item in items)
            {
                value.Add(item.Url());
            }

            context.Add(context.Property.Alias, new DataValue(value));
        }
        else
        {
            var item = context.Property.GetValue<IPublishedContent>(context.Culture);
            var mediaObject = item?.Url();

            if (mediaObject != null)
            {
                context.Add(context.Property.Alias, new DataValue(mediaObject));
            }
        }
    }
}
