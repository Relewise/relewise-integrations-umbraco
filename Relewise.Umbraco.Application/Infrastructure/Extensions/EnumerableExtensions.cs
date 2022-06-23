namespace Relewise.Umbraco.Application.Infrastructure.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source) where T : class
    {
        return source ?? Array.Empty<T>();
    }
}