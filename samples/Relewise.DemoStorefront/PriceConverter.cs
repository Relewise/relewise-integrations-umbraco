using System.Globalization;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core.Models.Blocks;

internal sealed class PriceConverter : IPropertyMappingConverter<MultiCurrency?>, IDisposable
{
    private bool _disposed;

    public ValueTask<MultiCurrency?> ConvertAsync(PropertyMappingContext context, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(context.Property.GetValue(), cancellationToken));
    }

    internal static MultiCurrency? Read(object? source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source == null)
        {
            return null;
        }

        if (source is not BlockListModel entries)
        {
            throw new InvalidOperationException("Prices require a Block List of currency/amount entries.");
        }

        var values = new List<Money>();
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? code = entry.Content.GetProperty("currency")?.GetValue()?.ToString()?.Trim().ToUpperInvariant();
            object? amount = entry.Content.GetProperty("amount")?.GetValue();
            if (code is not { Length: 3 } || !code.All(char.IsAsciiLetterUpper) || amount == null)
            {
                throw new InvalidOperationException("Each price requires a three-letter currency code and an amount.");
            }

            if (!currencies.Add(code))
            {
                throw new InvalidOperationException($"Currency '{code}' is entered more than once for this price field.");
            }

            values.Add(new Money(code, System.Convert.ToDecimal(amount, CultureInfo.InvariantCulture)));
        }

        return new MultiCurrency(values);
    }

    public void Dispose() => _disposed = true;
}
