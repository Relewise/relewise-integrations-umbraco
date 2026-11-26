#pragma warning disable CS0618 // Keep built-ins in their original registration order for legacy AutoMap converters.

using System;
using System.Threading;
using System.Threading.Tasks;
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
    [Obsolete("Use ConvertAsync instead. Synchronous conversion will be removed in the next major version.")]
    public void Convert(RelewisePropertyConverterContext context)
    {
        var value = context.Property.GetValue<decimal>(context.Culture);
        context.Add(context.Property.Alias, new DataValue((double)value));
    }
}
