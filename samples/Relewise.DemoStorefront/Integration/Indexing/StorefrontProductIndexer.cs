using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;
using static Relewise.DemoStorefront.DemoConstants;

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

        if (source is not IContent product || product.ContentType.Alias != DocumentTypeAliases.Product)
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

        var additional = product.GetValue<string>(PropertyAliases.ExtraCategories, published: published);

        if (!string.IsNullOrWhiteSpace(additional))
        {
            categories.AddRange(
                (JsonSerializer.Deserialize<Guid[]>(additional) ?? [])
                .Select(key => key.ToString("D")));
        }

        fields.Add(
            new IndexField(
                SearchFields.Category,
                new IndexValue { Keywords = categories.Distinct().ToArray() },
                null,
                null
            ));

        var brandReference = product.GetValue<string>(PropertyAliases.Brand, published: published);
        string[] brands = [];

        if (!string.IsNullOrWhiteSpace(brandReference) && UdiParser.TryParse(brandReference, out Udi? brand) &&
            brand is GuidUdi brandId)
        {
            brands = [brandId.Guid.ToString("D")];
        }

        // Emit an empty field too, so clearing the picker removes its earlier search projection.
        fields.Add(new IndexField(SearchFields.Brand, new IndexValue { Keywords = brands }, null, null));

        return Task.FromResult<IEnumerable<IndexField>>(fields);
    }
}
