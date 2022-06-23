using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Umbraco.Application.Infrastructure.Extensions;

public static class PublishedContentExtensions
{
    public static string GetTitle(this IPublishedContent content)
    {
        return content.Value<string>("Title")?.NullIfEmpty() ?? content.Name ?? string.Empty;
    }

    public static string? NullIfEmpty(this string str)
    {
        return string.IsNullOrWhiteSpace(str) ? null : str;
    }
}