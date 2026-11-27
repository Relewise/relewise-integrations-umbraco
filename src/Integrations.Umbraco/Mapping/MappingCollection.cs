using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Relewise.Integrations.Umbraco.Mapping;

// Runtime mappings own alias lookup and entity/export membership; builders retain mutable registration state.
internal sealed class MappingCollection : IReadOnlyCollection<MappingDefinition>
{
    private readonly Dictionary<string, MappingDefinition> _byAlias;

    internal MappingCollection(IEnumerable<MappingDefinition> mappings)
    {
        _byAlias = mappings.ToDictionary(mapping => mapping.DocumentTypeAlias, StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _byAlias.Count;
    internal MappingDefinition this[string documentTypeAlias] => _byAlias[documentTypeAlias];
    internal IEnumerable<MappingDefinition> Exported => _byAlias.Values.Where(mapping => mapping.Export);
    internal bool HasExports => Exported.Any();

    internal bool TryGetMapping(string documentTypeAlias, [NotNullWhen(true)] out MappingDefinition? mapping)
    {
        return _byAlias.TryGetValue(documentTypeAlias, out mapping);
    }

    internal bool HasEntityType(Type entityType) => _byAlias.Values.Any(mapping => mapping.EntityType == entityType);
    internal bool Exports(Type entityType) => Exported.Any(mapping => mapping.EntityType == entityType);

    public IEnumerator<MappingDefinition> GetEnumerator() => _byAlias.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
