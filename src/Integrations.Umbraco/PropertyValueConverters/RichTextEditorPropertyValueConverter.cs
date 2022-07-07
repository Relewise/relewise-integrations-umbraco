using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Umbraco.Cms.Core.Strings;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

public class RichTextEditorPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.TinyMCE");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        HtmlEncodedString? value = context.Property.GetValue<HtmlEncodedString>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value?.ToString()));
    }
}