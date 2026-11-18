#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;

namespace Relewise.Integrations.Umbraco.PropertyValueConverters;

internal class DecimalPropertyValueConverter : IDefaultPropertyValueConverter, IRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.PropertyType.EditorAlias.Equals("Umbraco.Decimal");
    }

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Convert(context);
        return default;
    }

    /// <inheritdoc/>
    [System.Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
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