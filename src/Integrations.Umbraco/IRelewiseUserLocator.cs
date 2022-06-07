using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Defines a common way for the Relewise Umbraco Integration to find the Relewise <see cref="User"/> for the current request
/// </summary>
public interface IRelewiseUserLocator
{
    /// <summary>
    /// Gets the current <see cref="User"/> for the current request
    /// </summary>
    /// <returns><see cref="User"/></returns>
    Task<User> GetUser();
}