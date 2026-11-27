using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Indexing;

internal sealed class PublishedCultureResolver(
    IDocumentNavigationQueryService navigation,
    IPublishedContentCache? cache)
{
    private readonly Dictionary<Guid, IPublishedContent?> _ancestors = new();

    public string?[] Resolve(IPublishedContent source)
    {
        _ancestors.TryAdd(source.Key, source);
        if (!source.ContentType.VariesByCulture())
            return [null];

        if (!navigation.TryGetAncestorsKeys(source.Key, out IEnumerable<Guid> keys))
            return [];

        IEnumerable<string> cultures = source.Cultures.Keys;
        foreach (Guid key in keys)
        {
            // Reuse ancestor snapshots within this export; never read draft values from a preview context.
            if (!_ancestors.TryGetValue(key, out IPublishedContent? ancestor))
            {
                ancestor = cache?.GetById(preview: false, key);
                _ancestors.Add(key, ancestor);
            }

            if (ancestor == null)
                return [];

            if (ancestor.ContentType.VariesByCulture())
            {
                cultures = cultures.Intersect(ancestor.Cultures.Keys, StringComparer.OrdinalIgnoreCase);
            }
        }

        return [.. cultures];
    }
}
