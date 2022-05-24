using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

public interface IRelewiseUserLocator
{
    Task<User> GetUser();
}