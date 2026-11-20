using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
internal sealed class StorefrontUserLocator(IHttpContextAccessor http) : IRelewiseUserLocator
{
    public Task<User> GetUser()
    {
        var context = http.HttpContext;
        string? id = context?.Items["storefront-visitor"] as string ?? context?.Request.Cookies["storefront-visitor"];
        return Task.FromResult(id == null ? User.Anonymous() : User.ByTemporaryId(id));
    }
}
