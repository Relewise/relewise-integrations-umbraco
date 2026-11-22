using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Client.Requests.Search;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoConfigurationTests
{
    [Theory]
    [InlineData("catalogue")]
    [InlineData("offers")]
    [InlineData("commerceCatalogue")]
    public void ProductProvidersShareMarketVariantAndBusinessDataQueryBehavior(string alias)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Demo:ProductPolicy"] = "None"
        }).Build();
        var options = new RelewiseUmbracoOptionsBuilder();
        DemoConfiguration.Configure(options, configuration, connected: true);
        var provider = Assert.IsType<ProductSearchProviderOptions>(options.SearchProviders.Providers[alias].Options);
        Assert.Equal("inStock", provider.BooleanDataFields["inStock"]);
        Assert.All(DemoMarket.All, market => Assert.Equal("onSale_" + market.Currency, provider.BooleanDataFields["onSale_" + market.Currency]));
        using var services = new ServiceCollection().AddScoped(_ => DemoMarket.All.Single(market => market.Id == "dk")).BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
        using var scope = services.CreateScope();
        var user = User.Anonymous();
        var request = new ProductSearchRequest(new Language("da-DK"), new Currency("EUR"), user, "Test", "skov", 0, 24);
        provider.Configure!(request, new SearchRequestContext(scope.ServiceProvider, user, "da-DK"));
        Assert.Equal("DKK", request.Currency!.Value);
        Assert.Equal(1, request.Settings!.VariantRequestSettings!.MaxVariantsPerProduct);
        Assert.Equal(alias != "commerceCatalogue", provider.RequirePublishedPage);
    }
}
