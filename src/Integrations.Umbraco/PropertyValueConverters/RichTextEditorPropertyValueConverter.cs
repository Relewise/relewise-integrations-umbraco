using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Strings;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

/// <summary>
/// Handles extracting published HTML for 'Umbraco.RichText' (Tiptap) and 'Umbraco.TinyMCE'.
/// </summary>
public class RichTextEditorPropertyValueConverter : IRelewisePropertyValueConverter
{
    /// <inheritdoc />
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias is "Umbraco.RichText" or "Umbraco.TinyMCE";
    }

    /// <inheritdoc />
    public void Convert(RelewisePropertyConverterContext context)
    {
        IHtmlEncodedString? value = context.Property.GetValue<IHtmlEncodedString>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value?.ToHtmlString()));
    }
}
