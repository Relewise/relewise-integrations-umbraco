using System;
using System.Globalization;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class DateTimePropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.DateTime");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        DateTime date = context.Property.GetValue<DateTime>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(date.ToString(CultureInfo.InvariantCulture)));
    }
}