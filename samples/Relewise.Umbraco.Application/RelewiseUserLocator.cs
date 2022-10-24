using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Umbraco.Application.Infrastructure.CookieConsent;

namespace Relewise.Umbraco.Application;

public class RelewiseUserLocator : IRelewiseUserLocator
{
    private readonly CookieConsent _cookieConsent;

    public RelewiseUserLocator(CookieConsent cookieConsent)
    {
        _cookieConsent = cookieConsent;
    }

    public Task<User> GetUser()
    {
        bool consentGiven = _cookieConsent.HasGivenConsentFor(CookieType.Marketing);

        return Task.FromResult(consentGiven
            ? User.ByTemporaryId(_cookieConsent.UserId())
            : User.Anonymous());
    }
}