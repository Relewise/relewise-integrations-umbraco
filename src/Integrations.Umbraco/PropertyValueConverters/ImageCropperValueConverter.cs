using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Handles extracting property values for 'Umbraco.ImageCropper'
/// </summary>
public class ImageCropperValueConverter : IRelewisePropertyValueConverter
{
    private readonly IImageUrlGenerator _imageUrlGenerator;

    /// <inheritdoc cref="ImageCropperValueConverter" />
    public ImageCropperValueConverter(IImageUrlGenerator imageUrlGenerator)
    {
        _imageUrlGenerator = imageUrlGenerator;
    }

    /// <inheritdoc />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.ImageCropper");
    }

    /// <inheritdoc />
    public void Convert(RelewisePropertyConverterContext context)
    {
        ImageCropperValue? value = context.Property.GetValue<ImageCropperValue>(context.Culture);

        if (value == null)
            return;

        if (value.Crops != null)
        {
            foreach (ImageCropperValue.ImageCropperCrop crop in value.Crops)
            {
                context.Add($"{context.Property.Alias}_{crop.Alias}", value.Src+value.GetCropUrl(crop.Width, crop.Height, _imageUrlGenerator));
            }
        }
        else
        {
            context.Add($"{context.Property.Alias}", value.Src);
        }
    }
}