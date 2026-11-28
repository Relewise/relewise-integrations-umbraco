using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Dashboard;

internal sealed class DashboardPriceConverter : IPropertyMappingConverter<MultiCurrency?>
{
    public ValueTask<MultiCurrency?> ConvertAsync(PropertyMappingContext context, CancellationToken token)
    {
        throw new InvalidOperationException("Dashboard inspection must not execute property converters.");
    }
}
