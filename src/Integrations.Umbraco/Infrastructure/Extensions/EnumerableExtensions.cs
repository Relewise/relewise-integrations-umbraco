using System.Collections.Generic;
using System.Linq;

namespace Relewise.Integrations.Umbraco.Infrastructure.Extensions;

internal static class EnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class
    {
        return source.Where(x => x is not null)!;
    }
}