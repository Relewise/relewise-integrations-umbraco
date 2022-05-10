using Relewise.Client.DataTypes;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Services;

public interface IContentMapper
{
    // NOTE: Bør tage imod et objekt som wrapper argumenterne
    // NOTE: Bør returnere et objekt som wrapper ContentUpdate?
    ContentUpdate? Map(IPublishedContent content, long version);
}