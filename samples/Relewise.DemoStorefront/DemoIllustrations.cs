using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

internal static class DemoIllustrations
{
    internal static string? Render(IPublishedContent content, string image)
    {
        string? name = image.StartsWith("/images/", StringComparison.Ordinal) && image.EndsWith(".svg", StringComparison.Ordinal)
            ? image[8..^4] : null;
        if (name == null || !Assets.Contains(name))
        {
            return null;
        }
        string colour = content.Value<string>("color", culture: "en-US") switch
        {
            "Sand" => "#aa8e62",
            "Slate" => "#4b6070",
            "Rust" => "#ad634b",
            "Copper" => "#a16e46",
            "Moss" => "#6f7853",
            _ => "#556746"
        };
        return $"<svg viewBox='0 0 300 300' aria-hidden='true' style='color:{colour}'><use href='/images/products.svg#{name}'></use></svg>";
    }

    private static readonly HashSet<string> Assets = new(StringComparer.Ordinal)
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
