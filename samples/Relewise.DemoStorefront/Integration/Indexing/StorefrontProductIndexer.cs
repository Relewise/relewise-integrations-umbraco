using System.Text.Json;
using Relewise.DemoStorefront.Integration.Converters;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;

namespace Relewise.DemoStorefront.Integration.Indexing;

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
        Attempt<Guid> parent = identities.GetKeyForId(product.ParentId, UmbracoObjectTypes.Document);
        var categories = new List<string>();

        if (parent.Success)
        {
            categories.Add(parent.Result.ToString("D"));
        }

        var additional = product.GetValue<string>("extraCategories", published: published);

        if (!string.IsNullOrWhiteSpace(additional))
        {
            categories.AddRange(
                (JsonSerializer.Deserialize<Guid[]>(additional) ?? [])
                .Select(key => key.ToString("D")));
        }

        fields.Add(
            new IndexField(
                "shopCategory",
                new IndexValue { Keywords = categories.Distinct().ToArray() },
                null,
                null
            ));

        var brandReference = product.GetValue<string>("brand", published: published);

        if (!string.IsNullOrWhiteSpace(brandReference) && UdiParser.TryParse(brandReference, out Udi? brand) &&
            brand is GuidUdi brandId)
        {
            fields.Add(
                new IndexField(
                    "shopBrand",
                    new IndexValue { Keywords = [brandId.Guid.ToString("D")] },
                    null,
                    null
                ));
        }

        Dictionary<string, decimal> sales =
            StoredPriceReader.Read(product.GetValue<string>("salesPrice", published: published));

        foreach (var (currency, amount) in sales)
        {
            fields.Add(
                new IndexField(
                    "shopPrice" + currency,
                    new IndexValue { Decimals = [amount] },
                    null,
                    null
                ));
        }

        return Task.FromResult<IEnumerable<IndexField>>(fields);
    }
}
