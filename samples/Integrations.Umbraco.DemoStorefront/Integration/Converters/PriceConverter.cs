using System.Globalization;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.Blocks;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Integration.Converters;

internal sealed class PriceConverter : IPropertyMappingConverter<MultiCurrency?>, IDisposable
{
    private bool _disposed;

    public ValueTask<MultiCurrency?> ConvertAsync(PropertyMappingContext context, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();

        return ValueTask.FromResult(Read(context.Property.GetValue(), token));
    }

    public static MultiCurrency? Read(
        object? source,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

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

        foreach (BlockListItem entry in entries)
        {
            token.ThrowIfCancellationRequested();

            string? code = entry.Content.GetProperty(
                    PropertyAliases.Currency)
                ?.GetValue()
                ?.ToString()
                ?.Trim()
                .ToUpperInvariant();
            object? amount = entry.Content.GetProperty(PropertyAliases.Amount)?.GetValue();

            if (code is not { Length: 3 } || !code.All(char.IsAsciiLetterUpper) || amount == null)
            {
                throw new InvalidOperationException("Each price requires a three-letter currency code and an amount.");
            }

            if (!currencies.Add(code))
            {
                throw new InvalidOperationException(
                    $"Currency '{code}' is entered more than once for this price field.");
            }

            var value = Convert.ToDecimal(amount, CultureInfo.InvariantCulture);

            ValidateAmount(value);
            values.Add(new Money(code, value));
        }

        return new MultiCurrency(values);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    public static void ValidateAmount(decimal amount)
    {
        if (amount < 0 || decimal.Round(amount, 2) != amount)
        {
            throw new InvalidOperationException(
                "Demo prices require a nonnegative amount with at most two decimal places.");
        }
    }
}
