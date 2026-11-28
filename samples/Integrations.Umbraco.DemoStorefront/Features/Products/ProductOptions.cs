using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Products;

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
