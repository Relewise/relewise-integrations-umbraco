using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Features.Editorial;

internal static class EditorialPage
{
    internal static EditorialPageModel Create(IPublishedContent page, HttpContext http)
    {
        string culture = DemoMarket.Current(http).Culture;

        return new EditorialPageModel(
            page,
            [
                .. new[] { "contentRows", "pageGrid" }
                    .SelectMany(alias => StorefrontEditorial.Sections(page.GetProperty(alias)?.GetValue(), culture))
            ]);
    }
}
