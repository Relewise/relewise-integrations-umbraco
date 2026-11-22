using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

internal class DefaultAnonymousUserLocator : IRelewiseUserLocator
{
    public Task<User> GetUser() => Task.FromResult(User.Anonymous());
}
