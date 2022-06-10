using System.Threading;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

internal class MapContent
{
    public MapContent(IPublishedContent publishedContent, long version, CancellationToken token)
    {
        PublishedContent = publishedContent;
        Version = version;
        Token = token;
    }

    public IPublishedContent PublishedContent { get; }
    public long Version { get; }
    public CancellationToken Token { get; }
}