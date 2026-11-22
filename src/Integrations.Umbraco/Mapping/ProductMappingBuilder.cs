#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Mapping;

/// <summary>Maps a Product and its directly nested Variant documents.</summary>
public class ProductMappingBuilder : EntityMappingBuilder<Product>
{
    internal ProductMappingBuilder()
    {
    }
    /// <summary>Resolve a single Brand picker through the referenced Brand mapping.</summary>
    public void BrandFrom(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.BrandPropertyAlias != null)
        {
            throw new InvalidOperationException("A Brand reference is already configured.");
        }

        Definition.BrandPropertyAlias = sourcePropertyAlias;
    }
    /// <summary>Add category paths from a multiple picker, alongside mapped category ancestors.</summary>
    public void AdditionalCategoriesFrom(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.CategoryPropertyAlias != null)
        {
            throw new InvalidOperationException("Additional category references are already configured.");
        }

        Definition.CategoryPropertyAlias = sourcePropertyAlias;
    }
    /// <summary>Map direct child documents as Variants within this Product.</summary>
    public void VariantsFromChildren(string documentTypeAlias, Action<EntityMappingBuilder<ProductVariant>>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTypeAlias);
        if (Definition.Variant != null)
        {
            throw new InvalidOperationException("A Variant mapping is already configured.");
        }

        var builder = new EntityMappingBuilder<ProductVariant>();
        configure?.Invoke(builder);
        builder.Definition.DocumentTypeAlias = documentTypeAlias;
        Definition.Variant = builder.Definition;
    }
}
