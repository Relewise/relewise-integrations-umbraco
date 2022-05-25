using System.Threading.Tasks;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.UmbracoV9.Application.Infrastructure.CookieConsent;

namespace Relewise.UmbracoV9.Application;

public class RelewiseUserLocator : IRelewiseUserLocator
{
    private readonly CookieConsent _cookieConsent;

    public RelewiseUserLocator(CookieConsent cookieConsent)
    {
        _cookieConsent = cookieConsent;
    }

    public Task<User> GetUser()
    {
        return Task.FromResult(!_cookieConsent.HasGivenConsentFor(CookieType.Marketing)
            ? User.Anonymous()
            : User.ByTemporaryId(_cookieConsent.UserId()));
    }
}