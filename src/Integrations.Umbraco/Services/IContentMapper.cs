using System.Threading;
using System.Threading.Tasks;

namespace Relewise.Integrations.Umbraco.Services;

internal interface IContentMapper
{
    Task<MapContentResult> Map(MapContent content, CancellationToken token);
}