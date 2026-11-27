using System;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed record SearchFieldDefinition(
    SearchFieldKind Kind,
    string? DataKey,
    Type ValueType,
    bool Collection = false,
    bool Localized = false)
{
    public bool Numeric => ValueType == typeof(int) || ValueType == typeof(long) ||
                           ValueType == typeof(double) || ValueType == typeof(decimal) ||
                           ValueType == typeof(float);
}
