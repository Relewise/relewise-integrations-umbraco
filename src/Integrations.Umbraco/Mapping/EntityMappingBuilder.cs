using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

/// <summary>Defines source identity, built-in properties, and Data fields for an entity mapping.</summary>
/// <typeparam name="TEntity">The Relewise entity type.</typeparam>
public class EntityMappingBuilder<TEntity> where TEntity : class
{
    internal EntityMappingBuilder() { }
    internal MappingDefinition Definition { get; } = new(typeof(TEntity));
    /// <summary>Whether this mapping exports data. View-only mappings can set this to false.</summary>
    public bool Export { get => Definition.Export; set => Definition.Export = value; }
    /// <summary>Whether automatic view tracking applies to this mapping.</summary>
    public bool TrackViews { get => Definition.TrackViews; set => Definition.TrackViews = value; }
    /// <summary>Administrative actions allowed for this mapping. Defaults to synchronization.</summary>
    public AdministrativeActionPolicy AdministrativeActions
    {
        get => Definition.AdministrativeActions;
        set => Definition.AdministrativeActions = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// <summary>Use a scoped mapper for application fields, instead of declarative field mapping.</summary>
    public void UseMapper<TMapper>() where TMapper : class, IEntityMapper<TEntity>
    {
        if (Definition.Mapper != null) throw new InvalidOperationException("An entity mapper is already configured.");
        Definition.Mapper = (entity, source, cultures, operation) =>
        {
            if (entity is not TEntity destination) throw new InvalidOperationException("The configured mapper has an incompatible destination.");
            return operation.Resolve<TMapper>().MapAsync(new EntityMappingContext<TEntity>(source, destination, cultures, operation), operation.CancellationToken);
        };
    }
    /// <summary>Use a source property as the canonical ID instead of the configured Umbraco node ID/key.</summary>
    public void IdFromProperty(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.IdPropertyAlias != null) throw new InvalidOperationException("An ID mapping has already been configured.");
        Definition.IdPropertyAlias = sourcePropertyAlias;
    }
    /// <summary>Map a supported built-in value property, optionally selecting a strongly typed converter.</summary>
    public void Map<TValue>(Expression<Func<TEntity, TValue>> destinationProperty, string sourcePropertyAlias, Action<PropertyMappingOptions<TValue>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(destinationProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (destinationProperty.Body is not MemberExpression { Member: PropertyInfo property, Expression: ParameterExpression parameter }
            || parameter != destinationProperty.Parameters[0] || !property.CanWrite || property.PropertyType != typeof(TValue)
            || property.Name is "Id" or "Brand" or "CategoryPaths" or "Data" or "Custom" or "IsPopulated")
            throw new ArgumentException("Select a direct built-in value property. Identity, relationships, and Data use dedicated methods.", nameof(destinationProperty));
        var options = new PropertyMappingOptions<TValue>();
        configure?.Invoke(options);
        var value = Expression.Parameter(typeof(TValue), "value");
        Action<TEntity, TValue> assign = Expression.Lambda<Action<TEntity, TValue>>(
            Expression.Assign(destinationProperty.Body, value), destinationProperty.Parameters[0], value).Compile();
        Definition.AddProperty(property.Name, new PropertyMapping<TEntity, TValue>(sourcePropertyAlias, property.Name, assign, options.Converter));
    }
    /// <summary>Map an Umbraco property to Data, defaulting the destination key to its source alias.</summary>
    public void MapData(string sourcePropertyAlias, Action<DataMappingOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        var options = new DataMappingOptions(sourcePropertyAlias);
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataKey);
        if (!Definition.DataKeys.Add(options.DataKey)) throw new InvalidOperationException($"Data key '{options.DataKey}' is already mapped.");
        Definition.DataMappings.Add(new DataMapping(sourcePropertyAlias, options));
    }
}

/// <summary>Maps a Product and its directly nested Variant documents.</summary>
public class ProductMappingBuilder : EntityMappingBuilder<Product>
{
    internal ProductMappingBuilder() { }
    /// <summary>Resolve a single Brand picker through the referenced Brand mapping.</summary>
    public void BrandFrom(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.BrandPropertyAlias != null) throw new InvalidOperationException("A Brand reference is already configured.");
        Definition.BrandPropertyAlias = sourcePropertyAlias;
    }
    /// <summary>Add category paths from a multiple picker, alongside mapped category ancestors.</summary>
    public void AdditionalCategoriesFrom(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.CategoryPropertyAlias != null) throw new InvalidOperationException("Additional category references are already configured.");
        Definition.CategoryPropertyAlias = sourcePropertyAlias;
    }
    /// <summary>Map direct child documents as Variants within this Product.</summary>
    public void VariantsFromChildren(string documentTypeAlias, Action<EntityMappingBuilder<ProductVariant>>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTypeAlias);
        if (Definition.Variant != null) throw new InvalidOperationException("A Variant mapping is already configured.");
        var builder = new EntityMappingBuilder<ProductVariant>();
        configure?.Invoke(builder);
        builder.Definition.DocumentTypeAlias = documentTypeAlias;
        Definition.Variant = builder.Definition;
    }
}

internal sealed class MappingDefinition(Type entityType)
{
    internal Type EntityType { get; } = entityType;
    internal string DocumentTypeAlias { get; set; } = string.Empty;
    internal bool Export { get; set; } = true;
    internal bool TrackViews { get; set; } = true;
    internal bool AutomaticData { get; set; }
    internal bool Legacy { get; set; }
    internal IContentTypeMapping? LegacyMapper { get; set; }
    internal Func<object, IPublishedContent, string[], MappingOperation, Task>? Mapper { get; set; }
    internal string? IdPropertyAlias { get; set; }
    internal string? BrandPropertyAlias { get; set; }
    internal string? CategoryPropertyAlias { get; set; }
    internal MappingDefinition? Variant { get; set; }
    internal AdministrativeActionPolicy AdministrativeActions { get; set; } = AdministrativeActionPolicy.Synchronize();
    internal List<IPropertyMapping> Properties { get; } = new();
    internal HashSet<string> PropertyNames { get; } = new(StringComparer.Ordinal);
    internal List<DataMapping> DataMappings { get; } = new();
    internal HashSet<string> DataKeys { get; } = new(StringComparer.Ordinal);
    internal void AddProperty(string destinationProperty, IPropertyMapping mapping)
    {
        if (!PropertyNames.Add(destinationProperty)) throw new InvalidOperationException($"Property '{destinationProperty}' is already mapped.");
        Properties.Add(mapping);
    }
    internal void Validate()
    {
        if ((AutomaticData && (DataMappings.Count > 0 || Properties.Count > 0)) || ((LegacyMapper != null || Mapper != null) && (DataMappings.Count > 0 || Properties.Count > 0)) || (Mapper != null && (LegacyMapper != null || AutomaticData)))
            throw new InvalidOperationException($"Mapping '{DocumentTypeAlias}' combines automatic/custom and explicit field mappings.");
        foreach (DataMapping data in DataMappings)
            if (data.Options.DataKey is "contentTypeAlias" or "url" or "createdAt" or "UmbracoVersionId" || data.Options.DataKey.StartsWith("rwUmbraco", StringComparison.Ordinal))
                throw new InvalidOperationException($"Data key '{data.Options.DataKey}' is reserved for integration metadata.");
        Variant?.Validate();
    }
}
internal sealed record DataMapping(string SourcePropertyAlias, DataMappingOptions Options);
internal interface IPropertyMapping
{
    string SourcePropertyAlias { get; }
    string DestinationProperty { get; }
    void Apply(object entity, PropertyMappingContext context);
}
internal sealed class PropertyMapping<TEntity, TValue>(string sourcePropertyAlias, string destinationProperty,
    Action<TEntity, TValue> assign, Func<PropertyMappingContext, TValue>? converter) : IPropertyMapping where TEntity : class
{
    public string SourcePropertyAlias { get; } = sourcePropertyAlias;
    public string DestinationProperty { get; } = destinationProperty;
    public void Apply(object entity, PropertyMappingContext context)
    {
        if (entity is not TEntity destination) throw new InvalidOperationException("The mapped entity type does not match its configured property.");
        try
        {
            TValue value = converter != null ? converter(context) : BuiltInPropertyConversion.Convert<TValue>(context);
            assign(destination, value);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Cannot map '{context.Property.Alias}' on document '{context.PublishedContent.Key}' to '{context.DestinationProperty}' for cultures '{string.Join(", ", context.CulturesToPublish)}'.", error);
        }
    }
}
