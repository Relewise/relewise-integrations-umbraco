using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

public class MediaPickerValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.MediaPicker3");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        bool isMultiple = context.Property.PropertyType.DataType.ConfigurationAs<MediaPicker3Configuration>()?.Multiple ?? false;

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