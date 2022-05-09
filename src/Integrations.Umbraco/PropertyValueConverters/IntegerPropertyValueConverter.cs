using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class IntegerPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.Integer");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        int value = context.Property.GetValue<int>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value));
    }
}