#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Mapping;

internal sealed class EntityMapper(RelewiseUmbracoConfiguration configuration)
{
    internal async Task<MappedEntity> MapAsync(
        IPublishedContent source,
        MappingDefinition definition,
        MappingOperation operation,
        long version,
        bool parentAdministrativeActions = true,
        ContentUpdate.UpdateKind? contentUpdateKind = null,
        string[]? publishedCultures = null)
    {
        string[] cultures = publishedCultures ?? Cultures(source, operation);
        string id = configuration.GetContentId(source);
        object entity = Create(definition.EntityType, id);
        bool synchronize = definition.AdministrativeActions.Enabled && parentAdministrativeActions;
        Dictionary<string, DataValue?> data = Data(entity);

        if (synchronize && entity is not Brand)
        {
            entity.GetType().GetProperty("DisplayName")!.SetValue(entity, DisplayName(source, cultures));
        }

        if (synchronize && entity is Brand brand)
        {
            brand.DisplayName = source.Name(await operation.DefaultCultureAsync(cultures));
        }

        if (synchronize)
        {
            data[RelewiseDataKeys.Url] = new Multilingual(
                cultures
                    .Select(culture => new Multilingual.Value(culture, source.Url(culture, UrlMode.Absolute)))
                    .ToList());
            data[RelewiseDataKeys.CreatedAt] = new DateTimeOffset(source.CreateDate).ToUnixTimeSeconds();
        }

        ContentUpdate.UpdateKind? mappedContentKind = contentUpdateKind;

        if (definition.LegacyMapper != null)
        {
            if (synchronize)
            {
                ((Content)entity).CategoryPaths = CategoryPaths(
                    source,
                    typeof(ContentCategory),
                    cultures,
                    !HasMapping(typeof(ContentCategory)));
            }

            var update = new ContentUpdate(
                (Content)entity,
                contentUpdateKind ?? ContentUpdate.UpdateKind.ReplaceProvidedProperties);

            update = await definition.LegacyMapper.Map(
                         new ContentMappingContext(source, update, [.. cultures], operation.Services),
                         operation.CancellationToken)
                     ?? throw new InvalidOperationException($"Mapper for '{source.ContentType.Alias}' returned null.");
            mappedContentKind = update.Kind;
            entity = update.Content ??
                     throw new InvalidOperationException(
                         $"Mapper for '{source.ContentType.Alias}' returned an empty Content update.");
            id = ((Content)entity).Id; // Preserve the legacy mapper's returned update identity.
            data = Data(entity);

            if (synchronize)
            {
                // Legacy mappers may replace Content/Data; these fields were historically appended afterward.
                data[RelewiseDataKeys.Url] = new Multilingual(
                    cultures.Select(culture =>
                            new Multilingual.Value(culture, source.Url(culture, UrlMode.Absolute)))
                        .ToList());
                data[RelewiseDataKeys.CreatedAt] = new DateTimeOffset(source.CreateDate).ToUnixTimeSeconds();
            }
        }
        else
        {
            if (definition.AutomaticData)
            {
                IReadOnlyDictionary<string, DataValue?> converted = await operation.Services
                    .GetRequiredService<IRelewisePropertyConverter>()
                    .ConvertAsync(source, source, cultures, operation.Services, operation.CancellationToken);

                foreach (KeyValuePair<string, DataValue?> field in converted)
                {
                    data.Add(field.Key, field.Value);
                }
            }

            foreach (IPropertyMapping mapping in definition.Properties)
            {
                IPublishedProperty property = RequiredProperty(source, mapping.SourcePropertyAlias);

                await mapping.ApplyAsync(
                    entity,
                    new PropertyMappingContext(
                        source,
                        source,
                        property,
                        cultures,
                        mapping.DestinationProperty,
                        operation));
            }

            var converter =
                new RelewisePropertyConverter(operation.Services.GetServices<IRelewisePropertyValueConverter>());

            foreach (DataMapping mapping in definition.DataMappings)
            {
                IPublishedProperty property = RequiredProperty(source, mapping.SourcePropertyAlias);
                IAsyncRelewisePropertyValueConverter? selected = mapping.Options.ConverterType == null
                    ? null
                    : (IAsyncRelewisePropertyValueConverter)operation.Resolve(mapping.Options.ConverterType);
                PropertyConversion converted =
                    await converter.ConvertPropertyAsync(source, source, property, cultures, operation, selected);

                if (converted.Data.Count == 0)
                {
                    if (!converted.Handled && cultures.Any(culture => property.GetValue(culture) != null))
                    {
                        throw new InvalidOperationException(
                            $"No converter supports '{property.Alias}' on '{source.Key}'. " +
                            "Configure a field-specific converter.");
                    }

                    data.Add(mapping.Options.DataKey, null);
                    continue;
                }

                foreach (KeyValuePair<string, DataValue?> field in converted.Data)
                {
                    string destination = field.Key == property.Alias ? mapping.Options.DataKey : field.Key;

                    if (field.Key != property.Alias && mapping.Options.DataKey != property.Alias)
                    {
                        throw new InvalidOperationException(
                            $"Converter for '{property.Alias}" +
                            $"' emits multiple/custom keys; DataKey remapping requires " +
                            "a value under the source alias.");
                    }

                    if (!data.TryAdd(destination, field.Value))
                    {
                        throw new InvalidOperationException(
                            $"Data destination '{destination}' was written more than once.");
                    }
                }
            }
        }

        if (entity is Product || entity is Category || entity is Content)
        {
            Type categoryType = entity is Content || entity is ContentCategory
                ? typeof(ContentCategory)
                : typeof(ProductCategory);
            List<CategoryPath> paths = CategoryPaths(
                source,
                categoryType,
                cultures,
                entity is Content && !HasMapping(categoryType));

            if (entity is Product && definition.CategoryPropertyAlias != null)
            {
                foreach (IPublishedContent category in Picked(
                             source,
                             definition.CategoryPropertyAlias,
                             cultures[0],
                             operation))
                {
                    RequireMapping(category, typeof(ProductCategory));

                    List<CategoryPath> pickedPath =
                        CategoryPaths(category, typeof(ProductCategory), cultures, legacy: false, includeSelf: true);

                    foreach (CategoryPath path in pickedPath)
                    {
                        if (!paths.Any(existing =>
                                existing.BreadcrumbPathStartingFromRoot.Select(x => x.Id)
                                    .SequenceEqual(
                                        path.BreadcrumbPathStartingFromRoot.Select(x => x.Id),
                                        StringComparer.Ordinal)))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }

            if ((synchronize || definition.CategoryPropertyAlias != null) && definition.LegacyMapper == null)
            {
                entity.GetType().GetProperty("CategoryPaths")!.SetValue(entity, paths);
            }
        }

        if (entity is Product product && definition.BrandPropertyAlias != null)
        {
            IPublishedContent[] brands = [.. Picked(source, definition.BrandPropertyAlias, cultures[0], operation)];

            if (brands.Length > 1)
            {
                throw new InvalidOperationException("BrandFrom requires a single picked Brand document.");
            }

            if (brands.Length == 1)
            {
                RequireMapping(brands[0], typeof(Brand));
                product.Brand = new Brand(configuration.GetContentId(brands[0]));
            }
        }

        var result = new MappedEntity(source.Key, source.ContentType.Alias, id, entity, synchronize)
        {
            // Collection replacement must respect the child mapping even when no Variants remain published.
            ReplaceExistingVariants = definition.ReplaceExistingVariants,
            ContentUpdateKind = mappedContentKind,
            ClearAndReplace = definition.UpdatePolicy.Clears(operation.FullRebuild)
        };

        if (entity is Product && definition.Variant is { Export: true } variantMapping)
        {
            // Background exports must include published Variants in every language, independent of a request culture.
            foreach (IPublishedContent child in source.Children("*"))
            {
                if (!child.ContentType.Alias.Equals(
                        variantMapping.DocumentTypeAlias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] childCultures = Cultures(child, operation);
                bool parentInvariant = !source.ContentType.VariesByCulture();
                bool childInvariant = !child.ContentType.VariesByCulture();
                string[] effective = childInvariant ? cultures :
                    parentInvariant ? childCultures :
                    [.. childCultures.Intersect(cultures, StringComparer.OrdinalIgnoreCase)];

                if (effective.Length == 0)
                {
                    continue;
                }

                MappedEntity variant = await MapAsync(
                    child,
                    variantMapping,
                    operation,
                    version,
                    synchronize,
                    publishedCultures: effective);

                variant.Synchronize = synchronize && variant.Synchronize;

                if (result.Variants.Any(existing => existing.Id == variant.Id))
                {
                    throw new InvalidOperationException(
                        $"Product '{id}' has multiple Variants with ID '{variant.Id}'.");
                }

                result.Variants.Add(variant);
            }
        }

        if (definition.Mapper != null)
        {
            await definition.Mapper(entity, source, cultures, operation);

            if (Identity(entity) != id)
            {
                throw new InvalidOperationException("A custom mapper must not replace the configured canonical ID.");
            }

            data = Data(entity);
        }

        data[RelewiseDataKeys.DocumentTypeAlias] = source.ContentType.Alias;
        data[RelewiseDataKeys.NodeKey] = source.Key.ToString("D");
        data[RelewiseDataKeys.Published] = true;
        data[RelewiseDataKeys.Version] = version;

        return result;
    }

    internal static IPublishedProperty RequiredProperty(
        IPublishedElement source,
        string alias)
    {
        return source.GetProperty(alias) ??
               throw new InvalidOperationException($"Source property '{alias}' does not exist on '{source.Key}'.");
    }

    private bool HasMapping(Type type)
    {
        return configuration.Mappings.HasEntityType(type);
    }

    private void RequireMapping(
        IPublishedContent source,
        Type type)
    {
        if (!configuration.Mappings.TryGetMapping(source.ContentType.Alias, out MappingDefinition? mapping) ||
            mapping.EntityType != type)
        {
            throw new InvalidOperationException($"Referenced document '{source.Key}' is not mapped as {type.Name}.");
        }
    }

    private List<CategoryPath> CategoryPaths(
        IPublishedContent source,
        Type categoryType,
        string[] cultures,
        bool legacy,
        bool includeSelf = false)
    {
        var entries = new List<CategoryNameAndId>();

        foreach (IPublishedContent ancestor in source.Breadcrumbs(includeSelf))
        {
            if (!legacy && (!configuration.Mappings.TryGetMapping(
                    ancestor.ContentType.Alias,
                    out MappingDefinition? mapping) || mapping.EntityType != categoryType))
            {
                continue;
            }

            entries.Add(
                new CategoryNameAndId(
                    configuration.GetContentId(ancestor),
                    legacy ? DisplayName(ancestor, cultures) : null));
        }

        return entries.Count == 0 ? [] : [new CategoryPath([.. entries])];
    }

    private static IEnumerable<IPublishedContent> Picked(
        IPublishedContent source,
        string alias,
        string culture,
        MappingOperation operation)
    {
        IPublishedProperty property = RequiredProperty(source, alias);

        if (property.PropertyType.VariesByCulture() || property.PropertyType.VariesBySegment())
        {
            throw new InvalidOperationException(
                $"Relationship '{alias}' on '{source.Key}" +
                $"' must be culture and segment invariant; Relewise entity relationships have global IDs.");
        }

        object? value = property.GetValue(culture);

        if (value is IPublishedContent single)
        {
            return [single];
        }

        if (value is IEnumerable<IPublishedContent> multiple)
        {
            return multiple;
        }

        if (property.PropertyType.EditorAlias == "Umbraco.DocumentPicker.Multiple")
        {
            using UmbracoContextReference context =
                operation.Services.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();

            // This picker exposes raw document keys instead of converted published references.
            var raw = property.GetSourceValue(culture)?.ToString();
            Guid[] keys;

            try
            {
                keys = string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<Guid[]>(raw) ?? [];
            }
            catch (JsonException error)
            {
                throw new InvalidOperationException(
                    $"Reference property '{alias}' on '{source.Key}' contains invalid document keys.",
                    error);
            }

            return keys.Select(key => context.UmbracoContext.Content.GetById(key))
                .OfType<IPublishedContent>()
                .ToArray();
        }

        if (value == null)
        {
            return [];
        }

        throw new InvalidOperationException($"Reference property '{alias}' must return published document(s).");
    }

    internal static string[] Cultures(
        IPublishedContent source,
        MappingOperation operation)
    {
        string[] published = source.Cultures.Keys.Where(culture => !string.IsNullOrWhiteSpace(culture)).ToArray();

        if (published.Length > 0)
        {
            return published;
        }

        var contextFactory = operation.Services.GetRequiredService<IUmbracoContextFactory>();
        using UmbracoContextReference context = contextFactory.EnsureUmbracoContext();

        return
        [
            operation.Services.GetRequiredService<IDefaultCultureAccessor>()
                .DefaultCulture
        ];
    }

    private static Multilingual DisplayName(IPublishedContent source, string[] cultures)
    {
        return new Multilingual(
            cultures.Select(culture => new Multilingual.Value(culture, source.Name(culture)))
                .ToList());
    }

    internal static object Create(
        Type type,
        string id)
    {
        object entity = type == typeof(Content) ? new Content(id)
            : type == typeof(Product) ? new Product(id)
            : type == typeof(Brand) ? new Brand(id)
            : type == typeof(ProductCategory) ? new ProductCategory(id)
            : type == typeof(ContentCategory) ? new ContentCategory(id)
            : type == typeof(ProductVariant) ? new ProductVariant(id)
            : throw new InvalidOperationException($"Unsupported entity type {type}.");

        // SDK constructors initialize collections; leave them unset to preserve commerce-owned values on partial
        // updates.
        entity.GetType().GetProperty("Assortments")!.SetValue(entity, null);
        entity.GetType().GetProperty("CategoryPaths")?.SetValue(entity, null);

        if (entity is ProductVariant variant)
        {
            variant.Specification = null;
        }

        return entity;
    }

    internal static string Identity(object entity)
    {
        return (string)entity.GetType().GetProperty("Id")!.GetValue(entity)!;
    }

    internal static Dictionary<string, DataValue?> Data(object entity)
    {
        PropertyInfo property = entity.GetType().GetProperty("Data")!;

        if (property.GetValue(entity) is Dictionary<string, DataValue?> data)
        {
            return data;
        }

        data = new Dictionary<string, DataValue?>();
        property.SetValue(entity, data);

        return data;
    }
}
