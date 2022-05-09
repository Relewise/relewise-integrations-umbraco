using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

public interface IContentMapper
{
    ContentUpdate? Map(IPublishedContent content, long version);
}