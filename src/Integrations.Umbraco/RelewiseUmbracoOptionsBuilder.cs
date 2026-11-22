#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using System.Collections.Generic;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Search;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Represents the root configuration of the Relewise Umbraco Integration.
/// </summary>
public class RelewiseUmbracoOptionsBuilder
{
    /// <summary>
    /// Use stable Umbraco content keys (GUIDs) as Relewise IDs. Defaults to false.
    /// Applies to export, category paths, tracking, and publication lifecycle actions.
    /// </summary>
    public bool UseContentKeysAsIds { get; set; }

    internal Dictionary<string, ContentTypeBuilder?> ContentBuilders { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, MappingDefinition> EntityMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Named Content and Product query providers; registration does not add indexing pipelines.
    /// </summary>
    public SearchProviderOptions SearchProviders { get; } = new();

    /// <summary>
    /// Map a distinct Document Type to Relewise Content.
    /// </summary>
    public RelewiseUmbracoOptionsBuilder MapContent(
        string documentTypeAlias,
        Action<ContentTypeBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTypeAlias);

        if (ContentBuilders.ContainsKey(documentTypeAlias) || EntityMappings.ContainsKey(documentTypeAlias))
        {
            throw new InvalidOperationException($"Document Type '{documentTypeAlias}' is already registered.");
        }

        var builder = new ContentTypeBuilder();

        configure?.Invoke(builder);
        builder.Definition.DocumentTypeAlias = documentTypeAlias;
        builder.Definition.Validate();
        ContentBuilders.Add(documentTypeAlias, builder);

        return this;
    }

    /// <summary>
    /// Register the single Product Document Type.
    /// </summary>
    public RelewiseUmbracoOptionsBuilder MapProduct(
        string documentTypeAlias,
        Action<ProductMappingBuilder>? configure = null)
    {
        var builder = new ProductMappingBuilder();

        configure?.Invoke(builder);
        Register(documentTypeAlias, builder.Definition);

        return this;
    }

    /// <summary>
    /// Register the single Brand Document Type.
    /// </summary>
    public RelewiseUmbracoOptionsBuilder MapBrand(
        string documentTypeAlias,
        Action<EntityMappingBuilder<Brand>>? configure = null)
    {
        return RegisterEntity(documentTypeAlias, configure);
    }

    /// <summary>
    /// Register the single Product Category Document Type.
    /// </summary>
    public RelewiseUmbracoOptionsBuilder MapProductCategory(
        string documentTypeAlias,
        Action<EntityMappingBuilder<ProductCategory>>? configure = null)
    {
        return RegisterEntity(documentTypeAlias, configure);
    }

    /// <summary>
    /// Register the single Content Category Document Type.
    /// </summary>
    public RelewiseUmbracoOptionsBuilder MapContentCategory(
        string documentTypeAlias,
        Action<EntityMappingBuilder<ContentCategory>>? configure = null)
    {
        return RegisterEntity(documentTypeAlias, configure);
    }

    private RelewiseUmbracoOptionsBuilder RegisterEntity<TEntity>(
        string documentTypeAlias,
        Action<EntityMappingBuilder<TEntity>>? configure) where TEntity : class
    {
        var builder = new EntityMappingBuilder<TEntity>();

        configure?.Invoke(builder);
        Register(documentTypeAlias, builder.Definition);

        return this;
    }

    private void Register(
        string documentTypeAlias,
        MappingDefinition mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTypeAlias);

        if (ContentBuilders.ContainsKey(documentTypeAlias) || EntityMappings.ContainsKey(documentTypeAlias))
        {
            throw new InvalidOperationException($"Document Type '{documentTypeAlias}' is already registered.");
        }

        foreach (MappingDefinition existing in EntityMappings.Values)
        {
            if (existing.EntityType == mapping.EntityType)
            {
                throw new InvalidOperationException(
                    $"{mapping.EntityType.Name} mapping '{existing.DocumentTypeAlias}" +
                    $"' is already registered; cannot register '{documentTypeAlias}'.");
            }
        }

        mapping.DocumentTypeAlias = documentTypeAlias;
        mapping.Validate();
        EntityMappings.Add(documentTypeAlias, mapping);
    }

    /// <summary>
    /// Registers a Document Type for existing Content mapping and view-tracking configuration.
    /// </summary>
    /// <param name="contentType">The Document Type alias.</param>
    /// <param name="options">Configuration for this Document Type.</param>
    /// <param name="throwIfExists">Whether repeated registration should throw instead of merging configuration.</param>
    public RelewiseUmbracoOptionsBuilder AddContentType(
        string contentType,
        Action<ContentTypeBuilder>? options = null,
        bool throwIfExists = false)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));
        }

        if (EntityMappings.ContainsKey(contentType))
        {
            throw new InvalidOperationException(
                $"Document Type '{contentType}' is already registered for another entity type.");
        }

        if (ContentBuilders.TryGetValue(contentType, out ContentTypeBuilder? builder))
        {
            if (throwIfExists)
            {
                throw new ArgumentException("A contentType with that name was already registered", nameof(contentType));
            }
        }
        else
        {
            builder = new ContentTypeBuilder();
            builder.Definition.Export = false;
            ContentBuilders.Add(contentType, builder);
        }

        if (builder != null)
        {
            options?.Invoke(builder);
            builder.Definition.DocumentTypeAlias = contentType;

            if (builder.AutoMapContent || builder.Mapper != null)
            {
                builder.Definition.Export = true;
            }
        }

        return this;
    }
}
