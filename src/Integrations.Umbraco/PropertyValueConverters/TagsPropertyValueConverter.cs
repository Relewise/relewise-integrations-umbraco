using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

public class TagsPropertyValueConverter : IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.Tags");
    }

    public void Convert(RelewisePropertyConverterContext context)
    {
        IEnumerable<string> value = context.Property.GetValue<IEnumerable<string>>(context.Culture);
        context.Add(context.Property.Alias, new DataValue(value));
    }
}