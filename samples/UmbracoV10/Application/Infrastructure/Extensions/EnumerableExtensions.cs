using System;
using System.Collections.Generic;

namespace Relewise.UmbracoV10.Application.Infrastructure.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source) where T : class
    {
        return source ?? Array.Empty<T>();
    }
}