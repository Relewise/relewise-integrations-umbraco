using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Relewise.Umbraco.Application.Infrastructure.CookieConsent;

public class CookieConsent
{
    public readonly string CookieName = "_cookieConsent";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieConsent(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool HasGivenConsentFor(CookieType type)
    {
        string? cookie = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];

        if (cookie == null)
            return false;

        CookieData? data = JsonConvert.DeserializeObject<CookieData>(cookie);

        if (data == null)
            throw new InvalidOperationException($"Unable to deserialize cookie '{CookieName}' with data: '{cookie}'.");
        
        return type switch
        {
            CookieType.Functional => data.Cookies.Functional,
            CookieType.Marketing => data.Cookies.Marketing,
            CookieType.Statistics => data.Cookies.Statistics,
            _ => throw new ArgumentException("Invalid enum value for command", nameof(type)),
        };
    }

    public string UserId()
    {
        string? cookie = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];

        if (cookie == null)
            throw new InvalidOperationException("UserId is null as cookie does not exist.");

        var data = JsonConvert.DeserializeObject<CookieData>(cookie);

        return data?.UserId ?? throw new InvalidOperationException($"Unable to deserialize cookie '{CookieName}' with data: '{cookie}'.");
    }

    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
    private class CookieData
    {
        public string UserId { get; set; } = default!;
        public CookieTypes Cookies { get; set; } = default!;
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private class CookieTypes
    {
        public bool Functional { get; set; }
        public bool Marketing { get; set; }
        public bool Statistics { get; set; }
    }
}