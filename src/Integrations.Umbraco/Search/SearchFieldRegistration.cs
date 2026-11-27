using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco.Search;

internal static class SearchFieldRegistration
{
    public static void Map<TEntity, TValue>(
        Dictionary<string, SearchFieldDefinition> fields,
        Expression<Func<TEntity, TValue>> destinationProperty,
        string fieldName)
    {
        ArgumentNullException.ThrowIfNull(destinationProperty);

        if (destinationProperty.Body is not MemberExpression
            {
                Expression: ParameterExpression, Member: PropertyInfo property
            })
        {
            throw new ArgumentException("Select a direct built-in entity property.", nameof(destinationProperty));
        }

        Add(fields, fieldName, BuiltIn(typeof(TEntity), property.Name));
    }

    public static SearchFieldDefinition BuiltIn(Type entityType, string property)
    {
        SearchFieldKind kind = property switch
        {
            nameof(Product.Id) => SearchFieldKind.Id,
            nameof(Product.DisplayName) => SearchFieldKind.DisplayName,
            nameof(Product.CategoryPaths) => SearchFieldKind.Categories,
            nameof(Product.SalesPrice) when entityType == typeof(Product) => SearchFieldKind.SalesPrice,
            nameof(Product.Brand) when entityType == typeof(Product) => SearchFieldKind.Brand,
            _ => throw new NotSupportedException($"Built-in query property '{property}' is not supported.")
        };

        return new SearchFieldDefinition(
            kind,
            null,
            kind == SearchFieldKind.SalesPrice ? typeof(decimal) : typeof(string),
            kind == SearchFieldKind.Categories);
    }

    public static void MapData<TValue>(
        Dictionary<string, SearchFieldDefinition> fields,
        string fieldName,
        Action<SearchDataMappingOptions>? configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        var options = new SearchDataMappingOptions(fieldName);
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataKey);

        Type type = typeof(TValue);
        bool collection = type.IsArray;
        Type valueType = collection ? type.GetElementType()! : type;
        var field = new SearchFieldDefinition(SearchFieldKind.Data, options.DataKey, valueType, collection);

        if (valueType != typeof(string) && valueType != typeof(bool) && !field.Numeric ||
            collection && valueType == typeof(bool))
        {
            throw new NotSupportedException($"Query Data type '{type.Name}' is not supported.");
        }

        Add(fields, fieldName, field);
    }

    private static void Add(
        Dictionary<string, SearchFieldDefinition> fields,
        string fieldName,
        SearchFieldDefinition field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        if (!fields.TryAdd(fieldName, field))
        {
            throw new InvalidOperationException($"Query field '{fieldName}' is already bound.");
        }
    }
}
