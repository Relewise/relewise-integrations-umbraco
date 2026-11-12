using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Extracts media URLs from Umbraco 19's single and multiple media pickers.
/// </summary>
public class MediaPickerValueConverter : IRelewisePropertyValueConverter
{
    /// <inheritdoc />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias is "Umbraco.MediaPicker3" or "Umbraco.MediaPicker.Single" or "Umbraco.MultipleMediaPicker";
    }

    /// <inheritdoc />
    public void Convert(RelewisePropertyConverterContext context)
    {
        bool isMultiple = context.Property.PropertyType.EditorAlias != "Umbraco.MediaPicker.Single";

        if (isMultiple)
        {
            var value = new List<string>();
            IEnumerable<IPublishedContent> items = context.Property.GetValue<IEnumerable<IPublishedContent>>(context.Culture) ?? Array.Empty<IPublishedContent>();
            foreach (var item in items)
            {
                string? mediaObject = item.Url();
                if (mediaObject != null)
                {
                    value.Add(mediaObject);
                }
            }
            context.Add(context.Property.Alias, new DataValue(value));
        }
        else
        {
            IPublishedContent? item = context.Property.GetValue<IPublishedContent>(context.Culture);
            string? mediaObject = item?.Url();
            if (mediaObject != null)
            {
                context.Add(context.Property.Alias, new DataValue(mediaObject));
            }
        }
    }
}
