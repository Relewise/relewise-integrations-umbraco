using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

internal class MapContent
{
    public MapContent(IPublishedContent publishedContent, long version)
    {
        PublishedContent = publishedContent;
        Version = version;
    }

    public IPublishedContent PublishedContent { get; }
    public long Version { get; }
}