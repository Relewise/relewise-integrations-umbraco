using Relewise.Integrations.Umbraco;

/// <summary>Extracts selected editorial text from Block List/Grid values, including nested elements.</summary>
internal sealed class EditorialBlocksConverter : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
        => context.Property.PropertyType.EditorAlias is "Umbraco.BlockList" or "Umbraco.BlockGrid";

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? value = context.Property.GetValue(context.Property.PropertyType.VariesByCulture() ? context.Culture : null);
        context.Add(context.Property.Alias, EditorialBlockText.Extract(value, context.Culture, cancellationToken));
        return default;
    }
}
