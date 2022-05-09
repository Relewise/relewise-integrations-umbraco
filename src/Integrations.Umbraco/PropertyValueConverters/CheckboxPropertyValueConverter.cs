using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class CheckboxPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.TrueFalse");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        bool value = context.Property.GetValue<bool>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value));
    }
}