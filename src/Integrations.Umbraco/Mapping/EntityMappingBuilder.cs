#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Relewise.Integrations.Umbraco;

/// <summary>Defines source identity, built-in properties, and Data fields for an entity mapping.</summary>
/// <typeparam name="TEntity">The Relewise entity type.</typeparam>
public class EntityMappingBuilder<TEntity> where TEntity : class
{
    internal EntityMappingBuilder()
    {
    }
    internal MappingDefinition Definition { get; } = new(typeof(TEntity));
    /// <summary>Whether this mapping exports data. View-only mappings can set this to false.</summary>
    public bool Export
    {
        get => Definition.Export; set => Definition.Export = value;
    }
    /// <summary>Whether automatic view tracking applies to this mapping.</summary>
    public bool TrackViews
    {
        get => Definition.TrackViews; set => Definition.TrackViews = value;
    }
    /// <summary>Administrative actions allowed for this mapping. Defaults to synchronization.</summary>
    public AdministrativeActionPolicy AdministrativeActions
    {
        get => Definition.AdministrativeActions;
        set => Definition.AdministrativeActions = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// <summary>Run a scoped mapper after declarative fields and configured relationships, allowing final application-field customization.</summary>
    public void UseMapper<TMapper>() where TMapper : class, IEntityMapper<TEntity>
    {
        if (Definition.Mapper != null)
        {
            throw new InvalidOperationException("An entity mapper is already configured.");
        }

        Definition.Mapper = (entity, source, cultures, operation) =>
        {
            if (entity is not TEntity destination)
            {
                throw new InvalidOperationException("The configured mapper has an incompatible destination.");
            }

            return operation.Resolve<TMapper>().MapAsync(new EntityMappingContext<TEntity>(source, destination, cultures, operation), operation.CancellationToken);
        };
    }
    /// <summary>Use a source property as the canonical ID instead of the configured Umbraco node ID/key.</summary>
    public void IdFromProperty(string sourcePropertyAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePropertyAlias);
        if (Definition.IdPropertyAlias != null)
        {
            throw new InvalidOperationException("An ID mapping has already been configured.");
        }

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
        {
            throw new ArgumentException("Select a direct built-in value property. Identity, relationships, and Data use dedicated methods.", nameof(destinationProperty));
        }

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
        if (!Definition.DataKeys.Add(options.DataKey))
        {
            throw new InvalidOperationException($"Data key '{options.DataKey}' is already mapped.");
        }

        Definition.DataMappings.Add(new DataMapping(sourcePropertyAlias, options));
    }
}
