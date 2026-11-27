using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Integration.Converters;

internal static class EditorialBlockText
{
    // Only these example properties contribute text. Add your own aliases intentionally.
    private static readonly string[] TextFields = [
        PropertyAliases.Heading,
        PropertyAliases.Text,
        PropertyAliases.Caption
    ];
    private static readonly string[] NestedFields = [
        PropertyAliases.Items,
        PropertyAliases.ContentRows,
        PropertyAliases.PageGrid
    ];

    public static string ExtractProperty(
        IPublishedProperty property,
        string culture,
        IVariationContextAccessor variations,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        // Umbraco uses the ambient culture when deciding which localized blocks are exposed.
        VariationContext? previous = variations.VariationContext;

        try
        {
            variations.VariationContext = new VariationContext(culture);

            return Extract(property.GetValue(property.PropertyType.VariesByCulture() ? culture : null), culture, token);
        }
        finally
        {
            variations.VariationContext = previous;
        }
    }

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
                foreach (BlockListItem item in list)
                {
                    Visit(item, culture, fragments, token);
                }

                break;
            case BlockListItem item: // Also supports a Block List configured for a single block.
                if (!IsHidden(item.Settings, culture))
                {
                    VisitElement(item.Content, culture, fragments, token);
                }

                break;
            case BlockGridModel grid:
                foreach (BlockGridItem item in grid)
                {
                    VisitGridItem(item, culture, fragments, token);
                }

                break;
            case BlockGridItem item:
                VisitGridItem(item, culture, fragments, token);
                break;
        }
    }

    private static void VisitGridItem(
        BlockGridItem item,
        string culture,
        List<string> fragments,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (IsHidden(item.Settings, culture))
        {
            return; // A hidden container also hides its area children.
        }

        VisitElement(item.Content, culture, fragments, token);

        foreach (BlockGridArea area in item.Areas)
        {
            foreach (BlockGridItem child in area)
            {
                VisitGridItem(child, culture, fragments, token);
            }
        }
    }

    private static void VisitElement(
        IPublishedElement element,
        string culture,
        List<string> fragments,
        CancellationToken token)
    {
        foreach (string alias in TextFields)
        {
            object? value = element.GetProperty(alias)?.GetValue(culture);
            string text = value switch
            {
                IHtmlEncodedString html => PlainText(html.ToHtmlString() ?? string.Empty),
                string plain => plain,
                _ => string.Empty // Never stringify arbitrary editor objects or settings.
            };

            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length > 0)
            {
                fragments.Add(text);
            }
        }

        foreach (string alias in NestedFields)
        {
            Visit(element.GetProperty(alias)?.GetValue(culture), culture, fragments, token);
        }
    }

    private static bool IsHidden(IPublishedElement? settings, string culture)
    {
        return settings?.GetProperty(PropertyAliases.Hide)?.GetValue(culture) is true;
    }

    private static string PlainText(string html)
    {
        var document = new HtmlDocument();

        document.LoadHtml(html);

        foreach (HtmlNode node in document.DocumentNode.SelectNodes("//script|//style|//template")?.ToArray()
                                  ?? [])
        {
            node.Remove();
        }

        // Separate text nodes so adjacent paragraphs do not become a single word.
        HtmlNodeCollection? textNodes = document.DocumentNode.SelectNodes("//text()");

        return textNodes is null
            ? string.Empty
            : WebUtility.HtmlDecode(
                string.Join(" ", textNodes.Select(node => node.InnerText)));
    }
}
