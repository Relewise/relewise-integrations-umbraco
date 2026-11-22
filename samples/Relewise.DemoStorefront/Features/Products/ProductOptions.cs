using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Products;

internal static class ProductOptions
{
    internal static string Label(IPublishedContent page)
    {
        foreach (string alias in new[] { "optionLabel", "color", "size" })
        {
            string? value = page.Value<string>(alias);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return SiteContent.Title(page);
    }
}
