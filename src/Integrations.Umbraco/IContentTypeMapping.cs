using System.Threading;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines a contract on how to map a PublishedContent
/// </summary>
public interface IContentTypeMapping
{
    /// <summary>
    /// Handles mapping a PublishedContent to a ContentUpdate. Context provides the shared handling of Content Id's and ensure the feed versioning is correct
    /// </summary>
    /// <param name="context"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<ContentUpdate> Map(ContentMappingContext context, CancellationToken token);
}