#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappedEntity(Guid nodeKey, string documentTypeAlias, string id, object entity, bool synchronize)
{
    internal Guid NodeKey { get; } = nodeKey;
    internal string DocumentTypeAlias { get; } = documentTypeAlias;
    internal string Id { get; } = id;
    internal object Entity { get; } = entity;
    internal bool Synchronize { get; set; } = synchronize;
    internal List<MappedEntity> Variants { get; } = new();
    internal Trackable Update(ContentUpdate.UpdateKind? contentUpdateKind = null)
    {
        contentUpdateKind ??= ContentUpdateKind;
        if (!Synchronize && contentUpdateKind == ContentUpdate.UpdateKind.ClearAndReplace)
        {
            throw new InvalidOperationException("ClearAndReplace is incompatible with AdministrativeActionPolicy.None.");
        }

        return Entity switch
        {
            Content content => new ContentUpdate(content, contentUpdateKind ?? ContentUpdate.UpdateKind.ReplaceProvidedProperties),
            Product product => new ProductUpdate(product, Variants.Select(variant => (ProductVariant)variant.Entity), ProductUpdate.UpdateKind.ReplaceProvidedProperties,
                ProductUpdate.UpdateKind.ReplaceProvidedProperties, replaceExistingVariants: Synchronize && VariantsConfigured)
            {
                BrandUpdateKind = BrandUpdate.UpdateKind.None
            },
            Brand brand => new BrandUpdate(brand, BrandUpdate.UpdateKind.ReplaceProvidedProperties),
            ProductCategory category => new ProductCategoryUpdate(category, CategoryUpdate.UpdateKind.ReplaceProvidedProperties),
            ContentCategory category => new ContentCategoryUpdate(category, CategoryUpdate.UpdateKind.ReplaceProvidedProperties),
            _ => throw new InvalidOperationException("Variants must be exported through their Product.")
        };
    }
    internal ContentUpdate.UpdateKind? ContentUpdateKind { get; set; }
    internal bool VariantsConfigured { get; set; }
}
