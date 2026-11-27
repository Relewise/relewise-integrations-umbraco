using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Features.Products;

internal static class ProductOptions
{
    public static string Label(IPublishedContent page)
    {
        foreach (string alias in new[] { PropertyAliases.OptionLabel, PropertyAliases.Color, PropertyAliases.Size })
        {
            var value = page.Value<string>(alias);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return SiteContent.Title(page);
    }
}
