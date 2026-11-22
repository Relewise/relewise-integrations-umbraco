using Microsoft.AspNetCore.Http;
using Relewise.DemoStorefront.Site;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoMarketTests
{
    [Theory]
    [InlineData("dk", "da-DK", "DKK", "/da")]
    [InlineData("eu", "en-US", "EUR", "/en")]
    [InlineData("us", "en-US", "USD", "/en")]
    public void ASelectedMarketKeepsLanguageCurrencyAndEditorialRoutingTogether(
        string id,
        string culture,
        string currency,
        string url)
    {
        var http = new DefaultHttpContext();

        http.Request.Headers.Cookie = DemoMarket.CookieName + "=" + id;

        DemoMarket market = DemoMarket.Current(http);

        Assert.Equal(culture, market.Culture);
        Assert.Equal(currency, market.Currency);
        Assert.Equal(url, market.ReturnUrl("/da"));
        Assert.Equal(url, market.ReturnUrl("/en"));
        Assert.Equal(url + "?example=blocks", market.ReturnUrl("/en?example=blocks"));
        Assert.Equal(url + "/store?provider=offers&q=flask", market.ReturnUrl("/store?provider=offers&q=flask"));
        Assert.Equal(url + "/store", market.ReturnUrl("/trail-daypack/"));
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

        Assert.Equal("eu", DemoMarket.Current(http).Id);
        Assert.Equal("eu", DemoMarket.Current(null).Id);
    }

    [Theory]
    [InlineData("dk", "/en/store", "eu")]
    [InlineData("eu", "/da/store", "dk")]
    [InlineData("us", "/en/store", "us")]
    [InlineData("us", "/da/fieldwork-udstyr/", "dk")]
    public void ExplicitLanguageUrlsOverrideIncompatibleMarketPreferences(string saved, string path, string expected)
    {
        var http = new DefaultHttpContext();

        http.Request.Headers.Cookie = DemoMarket.CookieName + "=" + saved;
        http.Request.Path = path;

        Assert.Equal(expected, DemoMarket.Current(http).Id);
    }

    [Theory]
    [InlineData("/en/cart/add")]
    [InlineData("/en/cart/update?quantity=3")]
    [InlineData("/da/cart/clear")]
    public void MarketChangesRecoverPostOnlyCartUrlsToTheCartPage(string returnUrl)
    {
        Assert.Equal("/en/cart", DemoMarket.All.Single(market => market.Id == "eu").ReturnUrl(returnUrl));
        Assert.Equal("/da/cart", DemoMarket.All.Single(market => market.Id == "dk").ReturnUrl(returnUrl));
    }

    [Fact]
    public void ASelectedMarketIsAvailableBeforeItsPreferenceCookieIsWritten()
    {
        var http = new DefaultHttpContext();

        http.Request.Headers.Cookie = DemoMarket.CookieName + "=us";
        http.Items[typeof(DemoMarket)] = DemoMarket.All.Single(market => market.Id == "dk");

        Assert.Equal("DKK", DemoMarket.Current(http).Currency);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
    [InlineData("/store\r\nLocation: https://example.com")]
    [InlineData("")]
    public void MarketSelectionCannotRedirectToAnExternalSite(string returnUrl)
    {
        Assert.Equal("/en/store", DemoMarket.All[0].ReturnUrl(returnUrl));
    }
}
