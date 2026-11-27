using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Umbraco.Cms.Core.Dictionary;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Site;

internal static class Storefront
{
    public static string Text(
        HttpContext http,
        string key,
        params object[] values)
    {
        var culture = CultureInfo.GetCultureInfo(DemoMarket.Current(http).Culture);
        string text =
            http.RequestServices.GetRequiredService<ICultureDictionaryFactory>().CreateDictionary(culture)[
                "Fieldwork." + key];

        return values.Length == 0 ? text : string.Format(culture, text, values);
    }

    public static string Image(IPublishedContent content)
    {
        var image = content.Value<string>(PropertyAliases.HeroImage);

        if (string.IsNullOrWhiteSpace(image) && content.ContentType.Alias == DocumentTypeAliases.ProductVariant)
        {
            image = content.Parent()?.Value<string>(PropertyAliases.HeroImage);
        }

        image = string.IsNullOrWhiteSpace(image) ? "/images/daypack.svg" : image;

        string? illustration = DemoIllustrations.Render(content, image);

        if (illustration != null)
        {
            return illustration;
        }

        var tag = new TagBuilder("img") { TagRenderMode = TagRenderMode.SelfClosing };
        tag.Attributes["alt"] = "";
        tag.Attributes["width"] = "190";
        tag.Attributes["height"] = "190";
        tag.Attributes["src"] = image;

        return Markup(tag);
    }

    public static string Icon()
    {
        var svg = new TagBuilder("svg");
        svg.Attributes["viewBox"] = "0 0 200 180";
        svg.Attributes["aria-hidden"] = "true";

        var mountain = new TagBuilder("path") { TagRenderMode = TagRenderMode.SelfClosing };
        mountain.Attributes["d"] = "M20 150 100 30 180 150z";
        svg.InnerHtml.AppendHtml(mountain);

        var light = new TagBuilder("path") { TagRenderMode = TagRenderMode.SelfClosing };
        light.Attributes["d"] = "M100 30v120h60z";
        light.Attributes["fill"] = "white";
        light.Attributes["opacity"] = ".2";
        svg.InnerHtml.AppendHtml(light);

        var entrance = new TagBuilder("path") { TagRenderMode = TagRenderMode.SelfClosing };
        entrance.Attributes["d"] = "m70 150 30-60 30 60";
        entrance.Attributes["fill"] = "#edf1e9";
        svg.InnerHtml.AppendHtml(entrance);

        return Markup(svg);
    }

    // Existing Vue payloads carry image fragments. TagBuilder encodes attributes; page markup lives in Razor.
    public static string Markup(IHtmlContent content)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        content.WriteTo(writer, HtmlEncoder.Default);

        return writer.ToString();
    }
}
