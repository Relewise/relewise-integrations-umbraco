#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.
using System.Threading;
using System.Threading.Tasks;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Handles extracting property values for 'Umbraco.ImageCropper'
/// </summary>
public class ImageCropperValueConverter : IDefaultPropertyValueConverter, IRelewisePropertyValueConverter
{
    private readonly IImageUrlGenerator _imageUrlGenerator;

    /// <inheritdoc cref="ImageCropperValueConverter" />
    public ImageCropperValueConverter(IImageUrlGenerator imageUrlGenerator)
    {
        _imageUrlGenerator = imageUrlGenerator;
    }

    /// <inheritdoc cref="IRelewisePropertyValueConverter.CanHandle" />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.ImageCropper");
    }

    /// <inheritdoc />
    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Convert(context);

        return default;
    }

    /// <inheritdoc/>
    [System.Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
    public void Convert(RelewisePropertyConverterContext context)
    {
        var value = context.Property.GetValue<ImageCropperValue>(context.Culture);

        if (value == null)
        {
            return;
        }

        if (value.Crops != null)
        {
            foreach (ImageCropperValue.ImageCropperCrop crop in value.Crops)
            {
                context.Add($"{context.Property.Alias}_{crop.Alias}",
                    value.Src + value.GetCropUrl(crop.Width, crop.Height, _imageUrlGenerator));
            }
        }
        else
        {
            context.Add($"{context.Property.Alias}", value.Src);
        }
    }
}
