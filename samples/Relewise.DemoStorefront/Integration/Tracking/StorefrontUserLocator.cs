using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

namespace Relewise.DemoStorefront.Integration.Tracking;

internal sealed class StorefrontUserLocator(IHttpContextAccessor http) : IRelewiseUserLocator
{
    public Task<User> GetUser()
    {
        HttpContext? context = http.HttpContext;
        var id = context?.Items["storefront-visitor"] as string ?? context?.Request.Cookies["storefront-visitor"];

        return Task.FromResult(id == null ? User.Anonymous() : User.ByTemporaryId(id));
    }
}
