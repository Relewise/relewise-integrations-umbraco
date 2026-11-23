using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Umbraco.Cms.Core.Dictionary;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoConnectionTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DisabledSearchAndPredictionsReturnUnavailableWithoutResolvingASearchClient(
        bool dataOnly,
        bool suggestionsOnly)
    {
        var dictionary = new Mock<ICultureDictionary>();

        dictionary.Setup(value => value[It.IsAny<string>()]).Returns("Configure Relewise");

        var factory = new Mock<ICultureDictionaryFactory>();

        factory.Setup(value => value.CreateDictionary(It.IsAny<CultureInfo>())).Returns(dictionary.Object);

        using ServiceProvider services = new ServiceCollection().AddSingleton(factory.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };

        http.Request.Path = "/en/store/results";

        StorefrontResponse result = suggestionsOnly
            ? Assert.IsType<StorefrontResponse>(await StorefrontPredictions.GetAsync(http))
            : await StorefrontListing.GetAsync(http, dataOnly: dataOnly);

        Assert.Equal(503, http.Response.StatusCode);
        Assert.Contains("Configure Relewise", result.Json);
        Assert.Contains("error", result.Json);
    }

    [Fact]
    public void MissingConfigurationEnablesContentPreviewWithoutResolvingAnySearchProvider()
    {
        Assert.False(DemoSetup.HasRelewiseConfiguration(new ConfigurationBuilder().Build()));

        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };

        Assert.False(DemoConnection.IsConfigured(http));
        Assert.False(DemoConnection.SearchConfigured(http));
    }

    [Theory]
    [InlineData("Relewise:DatasetId", "not-a-guid")]
    [InlineData("Relewise:ApiKey", "example")]
    [InlineData("Relewise:ServerUrl", "https://localhost:5000")]
    public void PartialConfigurationIsRejectedInsteadOfSilentlyEnteringPreview(
        string key,
        string value)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() => DemoSetup.HasRelewiseConfiguration(configuration));
    }

    [Fact]
    public void UpstreamFailuresAreRecognizedWithoutSwallowingRequestCancellationOrProgrammingErrors()
    {
        var failure = new HttpRequestException("Rejected credential", null, System.Net.HttpStatusCode.Unauthorized);

        Assert.True(DemoConnection.IsConnectionFailure(failure, CancellationToken.None));
        Assert.True(
            DemoConnection.IsConnectionFailure(
                new InvalidOperationException("Wrapper", failure),
                CancellationToken.None));
        Assert.True(DemoConnection.IsConnectionFailure(new TaskCanceledException(), CancellationToken.None));
        Assert.False(DemoConnection.IsConnectionFailure(failure, new CancellationToken(true)));
        Assert.False(
            DemoConnection.IsConnectionFailure(
                new InvalidOperationException("Programming error"),
                CancellationToken.None));
    }

    [Fact]
    public async Task UnrelatedErrorsStillPropagate()
    {
        var error = new InvalidOperationException("Programming error");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DemoConnection.InvokeAsync(new DefaultHttpContext(), _ => throw error));

        Assert.Same(error, actual);
    }
}
