using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;

namespace Relewise.Integrations.Umbraco.Tests;

public class BooleanProductSearchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BindingUsesNativeBooleanFiltersAndFacetsWithoutAProjectionAndRetainsMandatoryConstraints(bool omittedSelection)
    {
        var sent = new List<ProductSearchRequest>();
        var client = new Mock<Relewise.Client.Search.ISearcher>();
        client.Setup(value => value.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(new ProductSearchResponse
            {
                Hits = 3,
                Results = [],
                Facets = new ProductFacetResult
                {
                    Items = [new ProductDataBooleanValueFacetResult(DataSelectionStrategy.Product, "onSale_EUR", omittedSelection ? null! : [], null,
                        [new AvailableFacetValue<bool>(true, false, 3), new AvailableFacetValue<bool>(false, false, 7)])]
                }
            });
        var factory = new Mock<IRelewiseClientFactory>();
        factory.Setup(value => value.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>())).Returns(client.Object);
        var services = new ServiceCollection();
        services.Configure<global::Umbraco.Cms.Core.Configuration.Models.GlobalSettings>(options => options.Id = "boolean-site");
        services.AddSingleton(factory.Object);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IRelewiseUserLocator, Visitor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapProduct("product");
            options.SearchProviders.AddProduct("products", provider => provider.MapBooleanData("sale", "onSale_EUR"));
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        SearchResult result = await root.GetRequiredService<MappedEntitySearcher>().SearchAsync("products",
            filters: [new KeywordFilter("sale", ["true"], false)], facets: [new KeywordFacet("sale")], culture: "en-US", take: 0);

        Assert.Equal(2, sent.Count);
        string main = JsonConvert.SerializeObject(sent[0].Filters);
        Assert.Contains("onSale_EUR", main);
        Assert.Contains("\"Value\":true", main);
        Assert.DoesNotContain("rwUmbracoField_sale", main);
        Assert.DoesNotContain("onSale_EUR", JsonConvert.SerializeObject(sent[1].Filters));
        Assert.IsType<ProductDataBooleanValueFacet>(Assert.Single(sent[1].Facets!.Items));
        Assert.All(sent, request =>
        {
            string json = JsonConvert.SerializeObject(request.Filters);
            Assert.Contains("boolean-site", json);
            Assert.Contains("rwUmbracoPublished", json);
            Assert.Contains("rwUmbracoSearchProtected", json);
        });
        global::Umbraco.Cms.Core.Search.Querying.Faceting.FacetResult facet = Assert.Single(result.Facets);
        Assert.Equal("sale", facet.FieldName);
        Assert.Equal(3, Assert.Single(facet.Values.OfType<KeywordFacetValue>(), value => value.Key == "true").Count);
        Assert.Equal(7, Assert.Single(facet.Values.OfType<KeywordFacetValue>(), value => value.Key == "false").Count);
    }

    [Theory]
    [InlineData("true", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void BooleanValuesAndNegationAreTyped(string value, bool negate)
    {
        Client.Requests.Filters.Filter filter = CoreSearchTranslation.MapFilter(new KeywordFilter("sale", [value], negate),
            booleanDataFields: new Dictionary<string, string> { ["sale"] = "onSale_EUR" });
        string json = JsonConvert.SerializeObject(filter);
        Assert.Contains("onSale_EUR", json);
        Assert.Contains("\"Value\":" + value, json);
        Assert.DoesNotContain("rwUmbracoField_", json);
        if (negate)
        {
            Assert.True(Assert.IsType<Relewise.Client.Requests.Filters.AndFilter>(filter).Negated);
        }
    }

    [Fact]
    public void InvalidBindingsAndValuesFailExplicitly()
    {
        var options = new ProductSearchProviderOptions();
        Assert.Throws<ArgumentException>(() => options.MapBooleanData(" "));
        Assert.Throws<ArgumentException>(() => options.MapBooleanData("sale", " "));
        options.MapBooleanData("sale");
        Assert.Throws<InvalidOperationException>(() => options.MapBooleanData("sale"));
        Assert.Throws<ArgumentException>(() => CoreSearchTranslation.MapFilter(new KeywordFilter("sale", ["yes"], false), booleanDataFields: options.BooleanDataFields));
        Assert.Throws<NotSupportedException>(() => CoreSearchTranslation.MapFilter(new DecimalRangeFilter("sale", [], false), booleanDataFields: options.BooleanDataFields));
    }

    private sealed class Visitor : IRelewiseUserLocator
    {
        public Task<User> GetUser() => Task.FromResult(User.Anonymous());
    }
}
