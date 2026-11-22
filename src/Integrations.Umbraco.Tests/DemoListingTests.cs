using Microsoft.AspNetCore.Http;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoListingTests
{
    [Fact]
    public void ApiFilterLinksKeepThePublishedCollectionPathAndHideTransportParameters()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/en/store/results";
        http.Request.QueryString = new QueryString("?collection=71ac7895-78e4-4f31-b8f9-e7de3ef83973&view=offers&provider=offers&q=tent&price=low&sort=price-asc&page=2");
        var search = new StorefrontSearch(http.Request, editorial: false, offers: true, listingPath: "/en/equipment/camping/");

        string reset = search.ResetUrl();
        Assert.StartsWith("/en/equipment/camping/?", reset);
        Assert.Contains("provider=offers", reset);
        Assert.Contains("q=tent", reset);
        Assert.Contains("sort=price-asc", reset);
        Assert.DoesNotContain("collection=", reset);
        Assert.DoesNotContain("view=", reset);
        Assert.DoesNotContain("price=", reset);
        Assert.DoesNotContain("page=", reset);

        string clearQuery = search.ClearQueryUrl();
        Assert.StartsWith("/en/equipment/camping/?", clearQuery);
        Assert.Contains("price=low", clearQuery);
        Assert.DoesNotContain("collection=", clearQuery);
        Assert.DoesNotContain("view=", clearQuery);
        Assert.DoesNotContain("q=", clearQuery);
    }
}
