using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Relewise.Umbraco.Application.Infrastructure.CookieConsent;

public class CookieConsent
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieConsent(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool HasGivenConsentFor(CookieType type)
    {
        string? cookie = _httpContextAccessor.HttpContext?.Request.Cookies["_cookieconsent"];
        if (cookie == null)
            return false;

        CookieData? data = JsonConvert.DeserializeObject<CookieData>(cookie);

        if (data == null)
            throw new InvalidOperationException("Could not find 3rd party cookieconsent cookie");


        return type switch
        {
            CookieType.Functional => data.Cookies.Functional,
            CookieType.Marketing => data.Cookies.Marketing,
            CookieType.Statistics => data.Cookies.Statistics,
            _ => throw new ArgumentException("Invalid enum value for command", nameof(type)),
        };
    }

    public string? UserId()
    {
        string? cookie = _httpContextAccessor.HttpContext?.Request.Cookies["_cookieconsent"];
        if (cookie == null)
            return null;

        var data = JsonConvert.DeserializeObject<CookieData>(cookie);

        return data?.UserId;
    }

    private class CookieData
    {
        public string UserId { get; set; } = default!;
        public CookieTypes Cookies { get; set; } = default!;
    }

    private class CookieTypes
    {
        public bool Functional { get; set; }
        public bool Marketing { get; set; }
        public bool Statistics { get; set; }
    }
}