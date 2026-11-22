using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

internal class DefaultAnonymousUserLocator : IRelewiseUserLocator
{
    public Task<User> GetUserAsync()
    {
        return Task.FromResult(User.Anonymous());
    }
}
