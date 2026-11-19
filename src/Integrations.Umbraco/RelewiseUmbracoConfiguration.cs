#pragma warning disable CS0618 // Compatibility bridge for pre-existing synchronous converters and Content mappers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines the Relewise Umbraco Integration configuration
/// </summary>
public class RelewiseUmbracoConfiguration
{
    internal IReadOnlyDictionary<string, MappingDefinition> Mappings { get; }
    internal SearchProviderOptions SearchProviders { get; }
    private readonly HashSet<string> _trackableDocTypes;
    private readonly HashSet<string> _autoMappedContentTypes;
    private readonly Dictionary<string, IContentTypeMapping?> _customMappers;

#pragma warning disable 1591
    public RelewiseUmbracoConfiguration(IServiceProvider provider)
#pragma warning restore 1591
    {
        var options = new RelewiseUmbracoOptionsBuilder
        {
            UseContentKeysAsIds = provider.GetService<IConfiguration>()?.GetValue<bool>("Relewise:Umbraco:UseContentKeysAsIds") ?? false
        };

        foreach (Configure configure in provider.GetServices<Configure>())
        {
            configure(options, provider);
        }

        UseContentKeysAsIds = options.UseContentKeysAsIds;
        var mappings = new Dictionary<string, MappingDefinition>(options.EntityMappings, StringComparer.OrdinalIgnoreCase);
        foreach (var content in options.ContentBuilders)
        {
            MappingDefinition definition = content.Value?.Definition ?? new MappingDefinition(typeof(Relewise.Client.DataTypes.Content)) { AutomaticData = true };
            definition.DocumentTypeAlias = content.Key;
            definition.Validate();
            if (!mappings.TryAdd(content.Key, definition))
            {
                throw new InvalidOperationException($"Document Type '{content.Key}' is registered for multiple entity targets.");
            }
        }
        var variants = new List<MappingDefinition>();
        foreach (MappingDefinition mapping in mappings.Values)
        {
            mapping.Validate();
            if (mapping.Variant == null)
            {
                continue;
            }

            mapping.Variant.Export &= mapping.Export;
            mapping.Variant.TrackViews &= mapping.TrackViews;
            variants.Add(mapping.Variant);
        }
        foreach (MappingDefinition variant in variants)
        {
            if (!mappings.TryAdd(variant.DocumentTypeAlias, variant))
            {
                throw new InvalidOperationException($"Variant Document Type '{variant.DocumentTypeAlias}' is already registered.");
            }
        }

        Mappings = mappings;
        SearchProviders = options.SearchProviders;
        SearchProviders.Validate(Mappings);

        _trackableDocTypes = mappings.Where(x => x.Value.TrackViews).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _autoMappedContentTypes = options
            .ContentBuilders
            .Where(x => x.Value == null || x.Value.AutoMapContent)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _customMappers = options
            .ContentBuilders
            .Where(x => x.Value is { Mapper: { } })
            .ToDictionary(x => x.Key, x => x.Value?.Mapper, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether stable Umbraco content keys are used as Relewise IDs.
    /// Read from Relewise:Umbraco:UseContentKeysAsIds, with registration callbacks taking precedence.
    /// </summary>
    public bool UseContentKeysAsIds { get; }

    /// <summary>
    /// Resolves the Relewise ID for published content, including references in custom mappers.
    /// </summary>
    public string GetContentId(IPublishedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Mappings.TryGetValue(content.ContentType?.Alias ?? string.Empty, out MappingDefinition? mapping) && mapping.IdPropertyAlias is { } alias)
        {
            var property = content.GetProperty(alias) ?? throw new InvalidOperationException($"ID property '{alias}' does not exist on '{content.Key}'.");
            if (property.PropertyType.VariesByCulture() || property.PropertyType.VariesBySegment())
            {
                throw new InvalidOperationException($"ID property '{alias}' must be culture and segment invariant.");
            }

            return ResolvePropertyId(property.GetValue(), alias, content.Key);
        }
        return GetContentId(content.Id, content.Key);
    }

    /// <summary>
    /// Resolves the same Relewise ID for unpublished, recycled, or deleted content.
    /// </summary>
    public string GetContentId(IContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Mappings.TryGetValue(content.ContentType?.Alias ?? string.Empty, out MappingDefinition? mapping) && mapping.IdPropertyAlias is { } alias)
        {
            return ResolvePropertyId(content.GetValue(alias), alias, content.Key);
        }
        return GetContentId(content.Id, content.Key);
    }

    private static string ResolvePropertyId(object? raw, string sourcePropertyAlias, Guid nodeKey)
    {
        if (raw is not (string or Guid or byte or short or int or long or ushort or uint or ulong or decimal))
        {
            throw new InvalidOperationException($"ID property '{sourcePropertyAlias}' on '{nodeKey}' must contain a string, GUID, or integer/decimal ID.");
        }

        string? value = Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"ID property '{sourcePropertyAlias}' is empty on '{nodeKey}'.");
        }

        return value;
    }

    private string GetContentId(int id, Guid key) => UseContentKeysAsIds ? key.ToString("D") : id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Contains all contentType aliases that are being tracked
    /// </summary>
    public IReadOnlyCollection<string> TrackedContentTypes => _trackableDocTypes;

    /// <summary>
    /// Contains all contentType aliases that are being exported
    /// </summary>
    public IReadOnlyCollection<string> ExportedContentTypes => Mappings.Where(x => x.Value.Export).Select(x => x.Key).ToArray();

    /// <summary>
    /// If the contents type is registered, this will tell that it can be mapped by the automapping or by a mapper
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool CanMap(IContent content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return CanMap(content.ContentType.Alias);
    }

    /// <summary>
    /// If contentType is registered, this will tell that it can be mapped by the automapping or by a mapper
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool CanMap(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));
        }

        return Mappings.TryGetValue(contentType, out MappingDefinition? mapping) && mapping.Export;
    }

    /// <summary>
    /// Check if the content is registered to be tracked
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public bool IsTrackable(IPublishedElement content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return IsTrackable(content.ContentType.Alias);
    }

    private bool IsTrackable(string docAlias)
    {
        return _trackableDocTypes.Contains(docAlias);
    }

    /// <summary>
    /// Find a mapper for a ContentType Alias, if one exists
    /// </summary>
    /// <param name="contentType"></param>
    /// <param name="mapping"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool TryGetMapper(string contentType, out IContentTypeMapping? mapping)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Value cannot be null or whitespace", nameof(contentType));
        }

        return _customMappers.TryGetValue(contentType, out mapping) && mapping != null;
    }

    internal delegate void Configure(RelewiseUmbracoOptionsBuilder builder, IServiceProvider services);
}
