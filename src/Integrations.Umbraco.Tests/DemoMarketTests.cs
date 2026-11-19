using Microsoft.AspNetCore.Http;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoMarketTests
{
    [Theory]
    [InlineData("dk", "da-DK", "DKK", "/da/")]
    [InlineData("eu", "en-US", "EUR", "/en/")]
    [InlineData("us", "en-US", "USD", "/en/")]
    public void ASelectedMarketKeepsLanguageCurrencyAndEditorialRoutingTogether(string id, string culture, string currency, string url)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = DemoMarket.CookieName + "=" + id;

        var market = DemoMarket.Current(http);

        Assert.Equal(culture, market.Culture);
        Assert.Equal(currency, market.Currency);
        Assert.Equal(url, market.ReturnUrl("/da/"));
        Assert.Equal(url, market.ReturnUrl("/en/"));
        Assert.Equal(url + "?example=blocks", market.ReturnUrl("/en/?example=blocks"));
        Assert.Equal("/store?provider=offers&q=flask", market.ReturnUrl("/store?provider=offers&q=flask"));
        Assert.Equal("/trail-daypack/", market.ReturnUrl("/trail-daypack/"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public void MissingOrInvalidPreferencesUseTheDefaultMarket(string? preference)
    {
        var http = new DefaultHttpContext();
        if (preference != null)
        {
            http.Request.Headers.Cookie = DemoMarket.CookieName + "=" + preference;
        }

        Assert.Equal("dk", DemoMarket.Current(http).Id);
        Assert.Equal("dk", DemoMarket.Current(null).Id);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
    [InlineData("/store\r\nLocation: https://example.com")]
    [InlineData("")]
    public void MarketSelectionCannotRedirectToAnExternalSite(string returnUrl)
        => Assert.Equal("/store", DemoMarket.All[0].ReturnUrl(returnUrl));
}
