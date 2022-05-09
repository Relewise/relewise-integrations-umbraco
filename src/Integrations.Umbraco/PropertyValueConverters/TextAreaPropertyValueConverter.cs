using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class TextAreaPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.TextArea");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        string value = context.Property.GetValue<string>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value));
    }
}