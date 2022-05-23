using System;
using System.Collections.Generic;
using System.Linq;

namespace Relewise.Integrations.Umbraco;

public class RelewiseMappingConfiguration
{
    internal HashSet<string>? AutoMappedDocTypes { get; private set; }

    public RelewiseMappingConfiguration AutoMapping(params string[] contentTypeAliases)
    {
        if (contentTypeAliases == null) throw new ArgumentNullException(nameof(contentTypeAliases));

        AutoMappedDocTypes = contentTypeAliases
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return this;
    }
}