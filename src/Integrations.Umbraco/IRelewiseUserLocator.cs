using System;
using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Infrastructure;

namespace Relewise.Integrations.Umbraco;

/// <summary>
/// Resolves the Relewise <see cref="User"/> shared by behavioral tracking and core search for the current request.
/// Register a custom implementation to preserve your site's user identifiers and consent policy.
/// </summary>
public interface IRelewiseUserLocator
{
    /// <summary>Resolve the current request's Relewise user.</summary>
    /// <returns>The user identity used for search, recommendations and behavioral tracking.</returns>
    /// <remarks>Implement this method for new locators. Existing locators may implement only GetUser.</remarks>
    /// <exception cref="InvalidOperationException">Neither user-resolution method is implemented.</exception>
    Task<User> GetUserAsync()
    {
#pragma warning disable CS0618 // Keep existing custom user locators usable through the new async entry point.
        AsyncApiCompatibility.RequireLegacyImplementation<IRelewiseUserLocator>(
            this,
            nameof(GetUser),
            nameof(GetUserAsync));

        return GetUser();
#pragma warning restore CS0618
    }

    /// <summary>Compatibility entry point forwarding to GetUserAsync for new implementations.</summary>
    /// <returns>The current request's Relewise user.</returns>
    /// <exception cref="InvalidOperationException">Neither user-resolution method is implemented.</exception>
    [Obsolete("Use GetUserAsync instead. This member will be removed in the next major version.")]
    Task<User> GetUser()
    {
        return GetUserAsync();
    }
}
