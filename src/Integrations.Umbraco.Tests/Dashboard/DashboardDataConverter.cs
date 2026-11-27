namespace Relewise.Integrations.Umbraco.Tests.Dashboard;

internal sealed class DashboardDataConverter : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        throw new InvalidOperationException("Dashboard inspection must not execute property converters.");
    }

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken token)
    {
        throw new InvalidOperationException("Dashboard inspection must not execute property converters.");
    }
}
