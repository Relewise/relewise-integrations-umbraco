using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

internal sealed class PriceConverter : IPropertyMappingConverter<MultiCurrency?>, IDisposable
{
    private bool _disposed;
    public ValueTask<MultiCurrency?> ConvertAsync(PropertyMappingContext context, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<MultiCurrency?>(new MultiCurrency(new Money("DKK", System.Convert.ToDecimal(context.Property.GetValue(), System.Globalization.CultureInfo.InvariantCulture))));
    }
    public void Dispose() => _disposed = true;
}
