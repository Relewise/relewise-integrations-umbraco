using System.Globalization;
using Relewise.DemoStorefront.Integration;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.DemoStorefront.Site;

internal static class IntegrationDetails
{
    internal static string Render(
        IPublishedContent page,
        HttpContext http)
    {
        var fields = new List<(string Label, string Value)>
        {
            ("Umbraco node ID", page.Id.ToString(CultureInfo.InvariantCulture)),
            ("Umbraco key", page.Key.ToString("D")),
            ("Document type", page.ContentType.Alias),
            ("Culture", DemoMarket.Current(http).Culture)
        };

        if (page.ContentType.Alias == "productVariant" && page.Parent() is { } product)
        {
            fields.Add(("Relewise Product ID", DemoConfiguration.EntityId(product, http.RequestServices)));
            fields.Add(("Relewise Variant ID", DemoConfiguration.EntityId(page, http.RequestServices)));
            fields.Add(("Parent Product key", product.Key.ToString("D")));
        }
        else
        {
            fields.Add(
                (page.ContentType.Alias == "product" ? "Relewise Product ID" : "Relewise Content ID",
                    DemoConfiguration.EntityId(page, http.RequestServices)));
        }

        var rows = string.Concat(
            fields.Select(field =>
                $"<dt>{Storefront.E(field.Label)}</dt><dd><code>{Storefront.E(field.Value)}</code></dd>"));

        return
            $"<details class='developer-details' lang='en'><summary>Integration details</summary><dl>" +
            $"{rows}</dl></details>";
    }
}
