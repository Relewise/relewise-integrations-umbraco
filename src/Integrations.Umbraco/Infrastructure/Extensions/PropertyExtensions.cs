using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Relewise.Integrations.Umbraco.Infrastructure.Extensions;

/// <summary>
/// Helpers for mapping properties to Relewise
/// </summary>
public static class PropertyExtensions
{
    /// <summary>
    /// Gets a property value for a specific Umbraco language
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="property"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static T? GetValue<T>(this IPublishedProperty property, string culture)
    {
        if (!property.PropertyType.VariesByCulture())
        {
            culture = null!;
        }

        return (T?)property.GetValue(culture);
    }
}