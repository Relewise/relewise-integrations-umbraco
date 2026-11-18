using Relewise.Integrations.Umbraco;
using Relewise.MappingExamples;

/// <summary>Extracts selected editorial text from Block List/Grid values, including nested elements.</summary>
internal sealed class EditorialBlocksConverter : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
        => context.Property.PropertyType.EditorAlias is "Umbraco.BlockList" or "Umbraco.BlockGrid";

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        context.Add(context.Property.Alias, EditorialBlockText.Extract(context.Property.GetValue(context.Property.PropertyType.VariesByCulture() ? context.Culture : null), context.Culture, cancellationToken));
        return default;
    }
}
