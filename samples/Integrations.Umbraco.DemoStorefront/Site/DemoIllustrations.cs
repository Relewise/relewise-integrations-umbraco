using Microsoft.AspNetCore.Mvc.Rendering;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

internal static class DemoIllustrations
{
    public static string? Render(
        IPublishedContent content,
        string image)
    {
        string? name = image.StartsWith("/images/", StringComparison.Ordinal) &&
                       image.EndsWith(".svg", StringComparison.Ordinal)
            ? image[8..^4]
            : null;

        if (name == null || !Assets.Contains(name))
        {
            return null;
        }

        string colour = content.Value<string>(PropertyAliases.Color, "en-US") switch
        {
            "Sand" => "#aa8e62",
            "Slate" => "#4b6070",
            "Rust" => "#ad634b",
            "Copper" => "#a16e46",
            "Moss" => "#6f7853",
            _ => "#556746"
        };

        var svg = new TagBuilder("svg");
        svg.Attributes["viewBox"] = "0 0 300 300";
        svg.Attributes["aria-hidden"] = "true";
        svg.Attributes["style"] = "color:" + colour;

        var symbol = new TagBuilder("use");
        symbol.Attributes["href"] = "/images/products.svg#" + name;
        svg.InnerHtml.AppendHtml(symbol);

        return Storefront.Markup(svg);
    }

    private static readonly IReadOnlySet<string> Assets = new HashSet<string>(StringComparer.Ordinal)
    {
        "accessories",
        "beanie",
        "binoculars",
        "blanket",
        "cap",
        "chair",
        "cookware",
        "daypack",
        "duffel",
        "first-aid",
        "flask",
        "gloves",
        "hammock",
        "headlamp",
        "lantern",
        "mug",
        "phone-pouch",
        "pillow",
        "pouch",
        "shelter",
        "sleeping-bag",
        "socks",
        "straps",
        "sunglasses"
    };
}
