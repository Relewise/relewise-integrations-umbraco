using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Integration.Tracking;

internal sealed class StorefrontUserLocator(IHttpContextAccessor http) : IRelewiseUserLocator
{
    public Task<User> GetUserAsync()
    {
        HttpContext? context = http.HttpContext;
        string? id = context?.Items[Cookies.Visitor] as string ?? context?.Request.Cookies[Cookies.Visitor];

        return Task.FromResult(id == null ? User.Anonymous() : User.ByTemporaryId(id));
    }
}
