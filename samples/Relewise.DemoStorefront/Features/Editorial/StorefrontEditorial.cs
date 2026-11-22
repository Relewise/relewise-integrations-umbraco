using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;

namespace Relewise.DemoStorefront.Features.Editorial;

internal static class StorefrontEditorial
{
    internal static EditorialSection[] Sections(
        object? value,
        string culture)
    {
        var sections = new List<EditorialSection>();

        void Element(IPublishedElement content)
        {
            var heading = content.GetProperty("heading")?.GetValue(culture) as string;
            var text = content.GetProperty("text")?.GetValue(culture) as IHtmlEncodedString;

            if (!string.IsNullOrWhiteSpace(heading) || text != null)
            {
                sections.Add(new EditorialSection(heading, text));
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

        return [.. sections];
    }
}
