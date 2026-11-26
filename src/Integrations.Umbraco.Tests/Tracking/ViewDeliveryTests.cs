using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Integrations.Umbraco.Infrastructure.Mvc.Middlewares;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using static Relewise.Integrations.Umbraco.Tests.Mapping.ContentIdentityTestSupport;

namespace Relewise.Integrations.Umbraco.Tests.Tracking;

public class ViewDeliveryTests
{
    [Theory]
    [InlineData("HEAD", 200)]
    [InlineData("POST", 200)]
    [InlineData("GET", 302)]
    [InlineData("GET", 404)]
    [InlineData("GET", 503)]
    public async Task NonPageRequestsAndUnsuccessfulResponsesDoNotProduceViews(string method, int status)
    {
        using var scenario = new Scenario(method, status, _ => Task.CompletedTask);

        await scenario.Middleware.InvokeAsync(scenario.Http);
        await scenario.Completion.CompleteAsync();

        Assert.Equal(status, scenario.Http.Response.StatusCode);
        scenario.Tracker.Verify(value => value.TrackAsync(It.IsAny<ContentView>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JsonResponsesDoNotProducePageViews()
    {
        using var scenario = new Scenario("GET", 200, _ => Task.CompletedTask);
        await scenario.Middleware.InvokeAsync(scenario.Http);
        scenario.Http.Response.ContentType = "application/json";

        await scenario.Completion.CompleteAsync();

        scenario.Tracker.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task TransportFailuresAreLoggedAfterPageDeliveryWithoutChangingTheResponse(int status)
    {
        using var scenario = new Scenario("GET", 200, _ =>
            Task.FromException(new HttpRequestException("Tracking failed", null, (HttpStatusCode)status)));

        await scenario.Middleware.InvokeAsync(scenario.Http);
        scenario.Tracker.VerifyNoOtherCalls();
        await scenario.Completion.CompleteAsync();

        Assert.Equal(200, scenario.Http.Response.StatusCode);
        scenario.Tracker.Verify(value => value.TrackAsync(It.IsAny<ContentView>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains(scenario.Logger.Invocations, invocation =>
            invocation.Method.Name == "Log" && invocation.Arguments[0] is LogLevel.Warning);
    }

    [Fact]
    public async Task SlowTrackingDoesNotDelayTheHandlerAndHonorsDisconnection()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var disconnected = new CancellationTokenSource();
        using var scenario = new Scenario("GET", 200, token => release.Task.WaitAsync(token));
        scenario.Http.RequestAborted = disconnected.Token;

        await scenario.Middleware.InvokeAsync(scenario.Http).WaitAsync(TimeSpan.FromSeconds(1));
        scenario.Tracker.VerifyNoOtherCalls();
        Task completed = scenario.Completion.CompleteAsync();
        Assert.False(completed.IsCompleted);
        disconnected.Cancel();
        await completed.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(200, scenario.Http.Response.StatusCode);
        Assert.DoesNotContain(scenario.Logger.Invocations, invocation => invocation.Method.Name == "Log");
    }

    private sealed class Scenario : IDisposable
    {
        private readonly ServiceProvider _services;
        internal Mock<ITracker> Tracker { get; } = new();
        internal Mock<ILogger<RelewiseViewTrackingMiddleware>> Logger { get; } = new();
        internal DefaultHttpContext Http { get; }
        internal ResponseCompletion Completion { get; } = new();
        internal RelewiseViewTrackingMiddleware Middleware { get; }

        internal Scenario(string method, int status, Func<CancellationToken, Task> track)
        {
            var content = new Mock<IPublishedContent>();
            content.SetupGet(value => value.Id).Returns(1234);
            content.SetupGet(value => value.Key).Returns(SharedKey);
            content.SetupGet(value => value.ContentType.Alias).Returns("article");
            var request = new Mock<IPublishedRequest>();
            request.SetupGet(value => value.PublishedContent).Returns(content.Object);
            var umbraco = new Mock<IUmbracoContext>();
            umbraco.SetupGet(value => value.PublishedRequest).Returns(request.Object);
            var contexts = new Mock<IUmbracoContextFactory>();
            contexts.Setup(value => value.EnsureUmbracoContext()).Returns(() =>
                new UmbracoContextReference(umbraco.Object, false, Mock.Of<IUmbracoContextAccessor>()));
            Tracker.Setup(value => value.TrackAsync(It.IsAny<ContentView>(), It.IsAny<CancellationToken>()))
                .Returns((ContentView _, CancellationToken token) => track(token));
            var clients = new Mock<IRelewiseClientFactory>();
            clients.Setup(value => value.GetClient<ITracker>(Constants.ClientNames.Integration)).Returns(Tracker.Object);
            var locator = new Mock<IRelewiseUserLocator>();
            locator.Setup(value => value.GetUserAsync()).ReturnsAsync(User.Anonymous());
            _services = new ServiceCollection().AddSingleton(contexts.Object).AddSingleton(clients.Object)
                .AddSingleton(locator.Object).AddSingleton(Logger.Object).BuildServiceProvider();
            Http = new DefaultHttpContext { RequestServices = _services };
            Http.Features.Set<IHttpResponseFeature>(Completion);
            Http.Request.Method = method;
            Middleware = new RelewiseViewTrackingMiddleware(http =>
            {
                http.Response.StatusCode = status;
                http.Response.ContentType = "text/html";
                return Task.CompletedTask;
            }, Configuration("true"));
        }

        public void Dispose()
        {
            _services.Dispose();
        }
    }
}
