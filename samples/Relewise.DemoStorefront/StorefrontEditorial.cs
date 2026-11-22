using System.Net;
using System.Text;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;

namespace Relewise.DemoStorefront;

internal static class StorefrontEditorial
{
    internal static string Render(object? value, string culture)
    {
        var html = new StringBuilder();
        void Element(IPublishedElement content)
        {
            if (content.GetProperty("heading")?.GetValue(culture) is string heading && !string.IsNullOrWhiteSpace(heading))
            {
                html.Append($"<h2>{WebUtility.HtmlEncode(heading)}</h2>");
            }

            if (content.GetProperty("text")?.GetValue(culture) is IHtmlEncodedString text)
            {
                html.Append(text.ToHtmlString());
            }

            Visit(content.GetProperty("items")?.GetValue(culture));
        }

        void Visit(object? blocks)
        {
            switch (blocks)
            {
                case BlockListModel list:
                    foreach (BlockListItem item in list)
                    {
                        Visit(item);
                    }

                    break;
                case BlockListItem item when item.Settings?.GetProperty("hide")?.GetValue(culture) is not true:
                    Element(item.Content);
                    break;
                case BlockGridModel grid:
                    foreach (BlockGridItem item in grid)
                    {
                        Visit(item);
                    }

                    break;
                case BlockGridItem item when item.Settings?.GetProperty("hide")?.GetValue(culture) is not true:
                    Element(item.Content);
                    foreach (BlockGridArea area in item.Areas)
                    {
                        foreach (BlockGridItem child in area)
                        {
                            Visit(child);
                        }
                    }

                    break;
            }
        }

        Visit(value);
        return html.ToString();
    }
}
