using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Integration.Converters;

/// <summary>Extracts selected editorial text from Block List/Grid values, including nested elements.</summary>
internal sealed class EditorialBlocksConverter(IVariationContextAccessor variations)
    : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias is "Umbraco.BlockList" or "Umbraco.BlockGrid";
    }

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Add(context.Property.Alias,
            EditorialBlockText.ExtractProperty(
                context.Property,
                context.Culture,
                variations,
                cancellationToken
            )
        );

        return default;
    }
}
