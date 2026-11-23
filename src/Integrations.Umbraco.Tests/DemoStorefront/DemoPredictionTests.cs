using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Relewise.DemoStorefront.Features.Browsing;
using ISearcher = Relewise.Client.Search.ISearcher;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoPredictionTests
{
    [Theory]
    [InlineData(false, "/en/store/suggestions", "EUR", "en-us")]
    [InlineData(true, "/da/store/suggestions", "DKK", "da-dk")]
    public async Task AutocompleteOnlyPredictsAndRetainsVisitorMarketAndPublicEntityScope(
        bool editorial,
        string path,
        string currency,
        string language)
    {
        User user = User.ByTemporaryId("prediction-visitor");
        var locator = new Mock<IRelewiseUserLocator>();
        locator.Setup(value => value.GetUserAsync()).ReturnsAsync(user);
        var client = new Mock<ISearcher>(MockBehavior.Strict);
        SearchTermPredictionRequest? sent = null;
        using var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        client.Setup(value => value.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), token))
            .Callback<SearchTermPredictionRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new SearchTermPredictionResponse
            {
                Predictions =
                [
                    new SearchTermPredictionResult { Term = "Trail" },
                    new SearchTermPredictionResult { Term = "trail" },
                    new SearchTermPredictionResult { Term = "Travel" }
                ]
            });
        var factory = new Mock<IRelewiseClientFactory>();
        factory.Setup(value => value.GetClient<ISearcher>(null)).Returns(client.Object);
        var services = new ServiceCollection();
        services.AddSingleton(factory.Object);
        services.AddScoped(_ => locator.Object);
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = token };
        http.Request.Path = path;
        http.Request.QueryString = new QueryString("?q=tr&type=" + (editorial ? "article" : "product"));

        IResult result = await StorefrontPredictions.GetAsync(http);

        Assert.Equal(new[] { "Trail", "Travel" }, Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.NotNull(sent);
        Assert.Same(user, sent.User);
        Assert.Equal(currency, sent.Currency!.Value);
        Assert.Equal(language, sent.Language!.Value);
        Assert.Equal(new[] { editorial ? EntityType.Content : EntityType.Product }, sent.Settings!.TargetEntityTypes);
        string filters = JsonConvert.SerializeObject(sent.Filters);
        Assert.Contains("contentTypeAlias", filters);
        Assert.Contains("rwUmbracoPublished", filters);
        Assert.Contains("rwUmbracoSearchProtected", filters);
        Assert.Contains("rwUmbracoSearchCulture", filters);
        Assert.Contains(editorial ? "localizedArticle" : "product", filters);
        client.Verify(value => value.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), token), Times.Once);
        client.VerifyNoOtherCalls();
        locator.Verify(value => value.GetUserAsync(), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task ShortTermsDoNotResolveSdkClients(string term)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<RelewiseUmbracoConfiguration>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.QueryString = QueryString.Create("q", term);

        IResult result = await StorefrontPredictions.GetAsync(http);

        Assert.Empty(Assert.IsType<string[]>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value));
    }
}
