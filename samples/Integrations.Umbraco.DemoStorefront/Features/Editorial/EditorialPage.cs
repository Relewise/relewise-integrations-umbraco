using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Editorial;

internal static class EditorialPage
{
    public static EditorialPageModel Create(IPublishedContent page, HttpContext http)
    {
        string culture = DemoMarket.Current(http).Culture;

        return new EditorialPageModel(
            page,
            [
                .. new[] { PropertyAliases.ContentRows, PropertyAliases.PageGrid }
                    .SelectMany(alias => StorefrontEditorial.Sections(page.GetProperty(alias)?.GetValue(), culture))
            ]);
    }
}
