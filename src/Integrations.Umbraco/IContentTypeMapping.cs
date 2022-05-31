using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

public interface IContentTypeMapping
{
    public Task<ContentUpdate> Map(ContentMappingContext context);
}