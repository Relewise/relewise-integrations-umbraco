using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

internal class MapContent
{
    public MapContent(IPublishedContent publishedContent, long version, ContentUpdate.UpdateKind updateKind)
    {
        PublishedContent = publishedContent;
        Version = version;
        UpdateKind = updateKind;
    }

    public IPublishedContent PublishedContent { get; }
    public long Version { get; }
    public ContentUpdate.UpdateKind UpdateKind { get; }
}