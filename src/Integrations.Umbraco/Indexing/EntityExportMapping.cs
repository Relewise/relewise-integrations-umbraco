using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Indexing;

internal static class EntityExportMapping
{
    public static async Task<MappedExportBatch> MapAsync(
        RelewiseUmbracoConfiguration configuration,
        IUmbracoContext context,
        MappingOperation operation,
        IReadOnlyDictionary<Guid, ExportSourceDocument> documents,
        string[] siteCultures,
        long version,
        ContentUpdate.UpdateKind? contentUpdateKind,
        CancellationToken token)
    {
        var mapper = new EntityMapper(configuration);
        var desired = new List<MappedEntity>();
        var unmapped = new HashSet<Guid>();

        foreach (KeyValuePair<Guid, ExportSourceDocument> entry in documents)
        {
            token.ThrowIfCancellationRequested();

            IPublishedContent? source = context.Content?.GetById(entry.Key);

            if (source == null || entry.Value.Cultures.Length == 0)
            {
                continue;
            }

            if (!configuration.Mappings.TryGetMapping(source.ContentType.Alias, out MappingDefinition? mapping) ||
                !mapping.Export)
            {
                unmapped.Add(entry.Key);
                continue;
            }

            if (mapping.EntityType == typeof(ProductVariant))
            {
                continue;
            }

            string[]? cultures = entry.Value.Cultures.Any(value => value == null)
                ? null
                : entry.Value.Cultures.Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            // Invariant values are shared across the site's languages, rather than belonging only to its default
            // language.
            if (!source.ContentType.VariesByCulture())
            {
                cultures = siteCultures;
            }

            MappedEntity mapped = await mapper.MapAsync(
                source,
                mapping,
                operation,
                version,
                contentUpdateKind: contentUpdateKind,
                publishedCultures: cultures);
            operation.ClaimIdentity(mapping.EntityType, mapped.Id, mapped.NodeKey);

            if (mapped.Entity is Content or Product)
            {
                CoreSearchMetadata.Apply(mapped, entry.Value);
            }

            desired.Add(mapped);
        }

        return new MappedExportBatch([.. desired], unmapped);
    }
}
