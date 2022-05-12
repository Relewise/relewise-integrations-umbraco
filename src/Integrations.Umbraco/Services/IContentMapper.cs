using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

public interface IContentMapper
{
    MapContentResult Map(MapContent content);
}

public class MapContent
{
    public MapContent(IPublishedContent publishedContent, long version)
    {
        PublishedContent = publishedContent;
        Version = version;
    }

    public IPublishedContent PublishedContent { get; }
    public long Version { get; }
}

public class MapContentResult
{
    public MapContentResult(ContentUpdate? contentUpdate)
    {
        ContentUpdate = contentUpdate;
    }

    public ContentUpdate? ContentUpdate { get; }
    public bool Successful => ContentUpdate != null;

    public static MapContentResult Failed => new(null);
}