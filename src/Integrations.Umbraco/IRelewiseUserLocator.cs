using System.Threading.Tasks;
using Relewise.Client.DataTypes;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Resolves the Relewise <see cref="User"/> shared by behavioral tracking and core search for the current request.
/// Register a custom implementation to preserve your site's user identifiers and consent policy.
/// </summary>
public interface IRelewiseUserLocator
{
    /// <summary>
    /// Gets the current <see cref="User"/> for the current request
    /// </summary>
    /// <returns><see cref="User"/></returns>
    Task<User> GetUser();
}
