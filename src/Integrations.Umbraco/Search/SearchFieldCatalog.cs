using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using PropertyEditors = Umbraco.Cms.Core.Constants.PropertyEditors.Aliases;

namespace Relewise.Integrations.Umbraco.Search;

internal static class SearchFieldCatalog
{
    internal static SearchFieldDefinition Resolve(
        string fieldName,
        RelewiseUmbracoConfiguration configuration,
        SearchProviderDefinition provider,
        IServiceProvider services)
    {
        Dictionary<string, SearchFieldDefinition> bindings = provider.Options is ProductSearchProviderOptions product
            ? product.Fields
            : ((ContentSearchProviderOptions)provider.Options).Fields;

        if (bindings.TryGetValue(fieldName, out SearchFieldDefinition? configured))
        {
            return configured;
        }

        return provider.ResolvedFields.GetOrAdd(fieldName, _ => Infer(fieldName, configuration, provider, services));
    }

    private static SearchFieldDefinition Infer(
        string fieldName,
        RelewiseUmbracoConfiguration configuration,
        SearchProviderDefinition provider,
        IServiceProvider services)
    {
        var found = new List<SearchFieldDefinition>();

        foreach (MappingDefinition mapping in configuration.Mappings.Where(mapping =>
                     mapping.Export && mapping.EntityType == provider.EntityType &&
                     (provider.ContentOptions?.DocumentTypeAliases == null ||
                      provider.ContentOptions.DocumentTypeAliases.Contains(
                          mapping.DocumentTypeAlias,
                          StringComparer.OrdinalIgnoreCase))))
        {
            IPropertyMapping? property = mapping.Properties.SingleOrDefault(p => p.SourcePropertyAlias == fieldName);
            SearchFieldDefinition? field = null;

            if (property != null)
            {
                field = SearchFieldRegistration.BuiltIn(mapping.EntityType, property.DestinationProperty);
            }
            else if (mapping.IdPropertyAlias == fieldName)
            {
                field = new SearchFieldDefinition(SearchFieldKind.Id, null, typeof(string));
            }
            else if (mapping.BrandPropertyAlias == fieldName)
            {
                field = new SearchFieldDefinition(SearchFieldKind.Brand, null, typeof(string));
            }
            else
            {
                DataMapping? data = mapping.DataMappings.SingleOrDefault(p => p.SourcePropertyAlias == fieldName);

                if (data != null || mapping.AutomaticData)
                {
                    if (data?.Options.ConverterType != null)
                    {
                        throw new NotSupportedException(
                            $"Field '{fieldName}' has a custom converter; " +
                            "bind its stored value with provider.MapData<T>().");
                    }

                    IContentType? schema = services.GetService<IContentTypeService>()?.Get(mapping.DocumentTypeAlias);
                    IPropertyType? source = schema?.CompositionPropertyTypes.FirstOrDefault(p => p.Alias == fieldName);

                    if (source != null)
                    {
                        field = FromProperty(source, data?.Options.DataKey ?? fieldName);
                    }
                    else if (data != null)
                    {
                        throw new InvalidOperationException(
                            $"Cannot resolve the schema for '{fieldName}'; " +
                            "bind its stored value with provider.MapData<T>().");
                    }
                }
            }

            if (field != null)
            {
                found.Add(field);
            }
        }

        SearchFieldDefinition[] distinct = [.. found.Distinct()];

        return distinct.Length switch
        {
            1 => distinct[0],
            0 => throw new NotSupportedException(
                $"Query field '{fieldName}' is not mapped. Configure provider.Map() or provider.MapData<T>()."),
            _ => throw new InvalidOperationException(
                $"Query field '{fieldName}' resolves inconsistently across Content mappings. " +
                "Configure one explicit provider binding or separate providers.")
        };
    }

    private static SearchFieldDefinition FromProperty(IPropertyType property, string dataKey)
    {
        Type type = property.PropertyEditorAlias switch
        {
            PropertyEditors.Integer => typeof(int),
            PropertyEditors.Decimal => typeof(decimal),
            PropertyEditors.Boolean => typeof(bool),
            PropertyEditors.TextBox or PropertyEditors.TextArea or PropertyEditors.RichText => typeof(string),
            PropertyEditors.Tags => typeof(string[]),
            _ => throw new NotSupportedException(
                $"Query field '{property.Alias}' requires an explicit stored Data binding with provider.MapData<T>().")
        };
        bool collection = type.IsArray;
        var field = new SearchFieldDefinition(
            SearchFieldKind.Data,
            dataKey,
            collection ? type.GetElementType()! : type,
            collection,
            property.VariesByCulture());

        if (field.Localized && field.ValueType != typeof(string))
        {
            throw new NotSupportedException(
                $"Culture-varying non-text field '{property.Alias}' requires an explicit binding " +
                "to a stable stored key. " +
                "Legacy AutoMap output is unchanged.");
        }

        return field;
    }
}
