using Relewise.Client.DataTypes;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Integration.Tracking;

internal sealed class StorefrontUserLocator(IHttpContextAccessor http) : IRelewiseUserLocator
{
    public Task<User> GetUserAsync()
    {
        HttpContext? context = http.HttpContext;
        string? id = context?.Items[Cookies.Visitor] as string ?? context?.Request.Cookies[Cookies.Visitor];

        return Task.FromResult(id == null ? User.Anonymous() : User.ByTemporaryId(id));
    }
}
