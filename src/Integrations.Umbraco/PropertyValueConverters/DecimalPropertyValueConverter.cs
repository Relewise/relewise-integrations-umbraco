using System.Globalization;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class DecimalPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.Decimal");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        decimal value = context.Property.GetValue<decimal>(context.Culture);
        var number = 0d;

        if (double.TryParse(value.ToString(CultureInfo.InvariantCulture), out double n))
        {
            number = n;
        }

        context.Add(context.Property.Alias, new DataValue(number));
    }
}