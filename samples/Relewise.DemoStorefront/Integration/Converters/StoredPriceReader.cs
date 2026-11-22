using System.Text.Json.Nodes;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Integration.Converters;

// Indexing and seeding read stored Block List payloads; published values use PriceConverter.
internal static class StoredPriceReader
{
    internal static Dictionary<string, decimal> Read(string? json)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return prices;
        }

        foreach (JsonNode? block in JsonNode.Parse(json)?["contentData"]?.AsArray() ?? [])
        {
            JsonArray? values = block?["values"]?.AsArray();
            var currencyJson =
                values?.FirstOrDefault(
                    value => value?["alias"]?.GetValue<string>() == PropertyAliases.Currency)?["value"]
                    ?.GetValue<string>();
            var amount = values?.FirstOrDefault(
                value => value?["alias"]?.GetValue<string>() == PropertyAliases.Amount)?["value"]
                ?.GetValue<decimal>();
            string? currency = currencyJson == null
                ? null
                : JsonNode.Parse(currencyJson)?.AsArray().FirstOrDefault()?.GetValue<string>();
            string? code = currency?.Trim().ToUpperInvariant();

            if (code is not { Length: 3 } || !code.All(char.IsAsciiLetterUpper) || !amount.HasValue)
            {
                throw new InvalidOperationException(
                    "Each indexed price requires a three-letter currency code and an amount.");
            }

            PriceConverter.ValidateAmount(amount.Value);
            prices.Add(code, amount.Value);
        }

        return prices;
    }
}
