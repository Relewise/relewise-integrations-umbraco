#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using System.Collections.Generic;
using System.Linq;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class MappedEntity(
    Guid nodeKey,
    string documentTypeAlias,
    string id,
    object entity,
    bool synchronize)
{
    public Guid NodeKey { get; } = nodeKey;
    public string DocumentTypeAlias { get; } = documentTypeAlias;
    public string Id { get; } = id;
    public object Entity { get; } = entity;
    public bool Synchronize { get; set; } = synchronize;
    public List<MappedEntity> Variants { get; } = new();
    public bool ClearAndReplace { get; init; }

    public Trackable Update(ContentUpdate.UpdateKind? contentUpdateKind = null)
    {
        contentUpdateKind ??= ContentUpdateKind;

        if ((!Synchronize && (ClearAndReplace || contentUpdateKind == ContentUpdate.UpdateKind.ClearAndReplace)) ||
            Variants.Any(variant => variant.ClearAndReplace && !variant.Synchronize))
        {
            throw new InvalidOperationException(
                "ClearAndReplace is incompatible with AdministrativeActionPolicy.None.");
        }

        return Entity switch
        {
            Content content => new ContentUpdate(
                content,
                contentUpdateKind ?? (ClearAndReplace
                    ? ContentUpdate.UpdateKind.ClearAndReplace
                    : ContentUpdate.UpdateKind.ReplaceProvidedProperties)),
            Product product => new ProductUpdate(
                product,
                Variants.Select(variant => (ProductVariant)variant.Entity),
                ClearAndReplace
                    ? ProductUpdate.UpdateKind.ClearAndReplace
                    : ProductUpdate.UpdateKind.ReplaceProvidedProperties,
                Variants.Any(variant => variant.ClearAndReplace)
                    ? ProductUpdate.UpdateKind.ClearAndReplace
                    : ProductUpdate.UpdateKind.ReplaceProvidedProperties,
                Synchronize && ReplaceExistingVariants)
            {
                BrandUpdateKind = BrandUpdate.UpdateKind.None
            },
            Brand brand => new BrandUpdate(brand, ClearAndReplace
                ? BrandUpdate.UpdateKind.ClearAndReplace
                : BrandUpdate.UpdateKind.ReplaceProvidedProperties),
            ProductCategory category => new ProductCategoryUpdate(
                category,
                ClearAndReplace
                    ? CategoryUpdate.UpdateKind.ClearAndReplace
                    : CategoryUpdate.UpdateKind.ReplaceProvidedProperties),
            ContentCategory category => new ContentCategoryUpdate(
                category,
                ClearAndReplace
                    ? CategoryUpdate.UpdateKind.ClearAndReplace
                    : CategoryUpdate.UpdateKind.ReplaceProvidedProperties),
            _ => throw new InvalidOperationException("Variants must be exported through their Product.")
        };
    }

    public ContentUpdate.UpdateKind? ContentUpdateKind { get; set; }
    public bool ReplaceExistingVariants { get; init; }
}
