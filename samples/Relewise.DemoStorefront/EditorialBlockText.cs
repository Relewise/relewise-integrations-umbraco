using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;

namespace Relewise.MappingExamples;

public static class EditorialBlockText
{
    // Only these example properties contribute text. Add your own aliases intentionally.
    private static readonly string[] TextFields = ["heading", "text", "caption"];
    private static readonly string[] NestedFields = ["items", "contentRows", "pageGrid"];

    public static string Extract(object? value, string culture, CancellationToken token = default)
    {
        var fragments = new List<string>();
        Visit(value, culture, fragments, token);
        return string.Join("\n", fragments);
    }

    private static void Visit(object? value, string culture, List<string> fragments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        switch (value)
        {
            case BlockListModel list:
                foreach (var item in list)
                    Visit(item, culture, fragments, token);
                break;
            case BlockListItem item: // Also supports a Block List configured for a single block.
                if (!IsHidden(item.Settings, culture))
                    VisitElement(item.Content, culture, fragments, token);
                break;
            case BlockGridModel grid:
                foreach (var item in grid)
                    VisitGridItem(item, culture, fragments, token);
                break;
            case BlockGridItem item:
                VisitGridItem(item, culture, fragments, token);
                break;
        }
    }

    private static void VisitGridItem(BlockGridItem item, string culture, List<string> fragments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsHidden(item.Settings, culture))
            return; // A hidden container also hides its area children.

        VisitElement(item.Content, culture, fragments, token);
        foreach (var area in item.Areas)
            foreach (var child in area)
                VisitGridItem(child, culture, fragments, token);
    }

    private static void VisitElement(IPublishedElement element, string culture, List<string> fragments, CancellationToken token)
    {
        foreach (var alias in TextFields)
        {
            var value = element.GetProperty(alias)?.GetValue(culture);
            var text = value switch
            {
                IHtmlEncodedString html => PlainText(html.ToHtmlString() ?? string.Empty),
                string plain => plain,
                _ => string.Empty // Never stringify arbitrary editor objects or settings.
            };
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length > 0)
                fragments.Add(text);
        }

        foreach (var alias in NestedFields)
            Visit(element.GetProperty(alias)?.GetValue(culture), culture, fragments, token);
    }

    private static bool IsHidden(IPublishedElement? settings, string culture) =>
        settings?.GetProperty("hide")?.GetValue(culture) is true;

    private static string PlainText(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.SelectNodes("//script|//style|//template")?.ToArray()
                             ?? Array.Empty<HtmlNode>())
            node.Remove();

        // Separate text nodes so adjacent paragraphs do not become a single word.
        var textNodes = document.DocumentNode.SelectNodes("//text()");
        return textNodes is null ? string.Empty : WebUtility.HtmlDecode(
            string.Join(" ", textNodes.Select(node => node.InnerText)));
    }
}
