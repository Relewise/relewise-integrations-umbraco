using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;

// Typed search projection for Umbraco queries; business enrichment lives in DemoProductMapper.
internal sealed class StorefrontProductIndexer(IIdKeyMap identities) : IContentIndexer
{
    public Task<IEnumerable<IndexField>> GetIndexFieldsAsync(
        IContentBase source,
        string?[] cultures,
        bool published,
        CancellationToken cancellationToken = default)
    {
        var fields = new List<IndexField>();
        if (source is not IContent product || product.ContentType.Alias != "product")
        {
            return Task.FromResult<IEnumerable<IndexField>>(fields);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Resolve the primary parent through Umbraco's ID cache, not a content-service fetch per Product.
        var parent = identities.GetKeyForId(product.ParentId, UmbracoObjectTypes.Document);
        var categories = new List<string>();
        if (parent.Success)
        {
            categories.Add(parent.Result.ToString("D"));
        }
        string? additional = product.GetValue<string>("extraCategories", published: published);
        if (!string.IsNullOrWhiteSpace(additional))
        {
            categories.AddRange((System.Text.Json.JsonSerializer.Deserialize<Guid[]>(additional) ?? []).Select(key => key.ToString("D")));
        }
        fields.Add(new IndexField("shopCategory", new IndexValue { Keywords = categories.Distinct().ToArray() }, Culture: null, Segment: null));

        string? brandReference = product.GetValue<string>("brand", published: published);
        if (!string.IsNullOrWhiteSpace(brandReference) && global::Umbraco.Cms.Core.UdiParser.TryParse(brandReference, out var brand) &&
            brand is global::Umbraco.Cms.Core.GuidUdi brandId)
        {
            fields.Add(new IndexField("shopBrand", new IndexValue { Keywords = [brandId.Guid.ToString("D")] }, Culture: null, Segment: null));
        }

        var sales = ReadPrices(product.GetValue<string>("salesPrice", published: published));
        foreach (var (currency, amount) in sales)
        {
            fields.Add(new IndexField("shopPrice" + currency, new IndexValue { Decimals = [amount] }, Culture: null, Segment: null));
        }

        return Task.FromResult<IEnumerable<IndexField>>(fields);
    }

    internal static Dictionary<string, decimal> ReadPrices(string? json)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return prices;
        }

        foreach (JsonNode? block in JsonNode.Parse(json)?["contentData"]?.AsArray() ?? [])
        {
            var values = block?["values"]?.AsArray();
            string? currencyJson = values?.FirstOrDefault(value => value?["alias"]?.GetValue<string>() == "currency")?["value"]?.GetValue<string>();
            decimal? amount = values?.FirstOrDefault(value => value?["alias"]?.GetValue<string>() == "amount")?["value"]?.GetValue<decimal>();
            string? currency = currencyJson == null ? null : JsonNode.Parse(currencyJson)?.AsArray().FirstOrDefault()?.GetValue<string>();
            string? code = currency?.Trim().ToUpperInvariant();
            if (code is not { Length: 3 } || !code.All(char.IsAsciiLetterUpper) || !amount.HasValue)
            {
                throw new InvalidOperationException("Each indexed price requires a three-letter currency code and an amount.");
            }
            PriceConverter.ValidateAmount(amount.Value);
            prices.Add(code, amount.Value);
        }

        return prices;
    }
}
