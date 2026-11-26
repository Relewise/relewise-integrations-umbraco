using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Relewise.Client;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Responses;
using Relewise.Integrations.Umbraco.Search;
using Relewise.Integrations.Umbraco.Search.Indexing;
using Umbraco.Cms.Core.Search.Indexing;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class IndexMetadataServiceTests
{
    [Fact]
    public async Task CountsEachExportedEntityTypeOnceWithoutAQueryProvider()
    {
        await using var fixture = new Fixture(options =>
        {
            options.MapContent("article");
            options.MapContent("guide");
            options.MapContent("excluded", mapping => mapping.Export = false);
            options.MapProduct("product", mapping => mapping.VariantsFromChildren("variant"));
            options.MapBrand("brand");
            options.MapProductCategory("category");
            options.MapContentCategory("topic");
        });

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentDetailsCollectionResponse([], 14));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([], 92));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<BrandQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandDetailsCollectionResponse([], 3));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductCategoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductCategoryDetailsCollectionResponse([], 7));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentCategoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentCategoryDetailsCollectionResponse([], 1));

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);

        Assert.Equal(117, metadata.DocumentCount);
        Assert.Equal(HealthStatus.Healthy, metadata.HealthStatus);
        Assert.Equal(5, fixture.Reader.Invocations.Count);

        foreach (IInvocation invocation in fixture.Reader.Invocations)
        {
            string query = JsonConvert.SerializeObject(invocation.Arguments[0]);

            Assert.DoesNotContain("rwUmbracoSite", query);
            Assert.Contains("contentTypeAlias", query);
            Assert.DoesNotContain("excluded", query);
            Assert.DoesNotContain("variant", query, StringComparison.Ordinal);
            Assert.Contains("\"ReturnTotalNumberOfResults\":true", query);
        }

        ContentQuery content = fixture.Reader.Invocations.Select(value => value.Arguments[0])
            .OfType<ContentQuery>()
            .Single();

        Assert.True(content.IncludeDisabledContents);
        Assert.Equal(0, content.NumberOfResults);

        ProductQuery product = fixture.Reader.Invocations.Select(value => value.Arguments[0])
            .OfType<ProductQuery>()
            .Single();

        Assert.True(product.IncludeDisabledProducts);
        Assert.False(product.ExcludeProductsWithNoVariants);
        Assert.Equal(1, product.PageSize);
        Assert.False(product.ResultSettings!.SelectedProductDetailsProperties!.AllData);
        Assert.False(product.ResultSettings.SelectedProductDetailsProperties.AllVariants);
        Assert.Equal(1, fixture.DisposedScopes);
    }

    [Fact]
    public async Task NamedProvidersCountOnlyTheirConfiguredMembershipWithoutExecutingCallbacks()
    {
        await using var fixture = new Fixture(options =>
        {
            options.MapContent("article");
            options.MapContent("guide");
            options.MapProduct("product");
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider(
                    "articles",
                    provider =>
                    {
                        provider.DocumentTypeAliases = ["article"];
                        provider.ConfigureRequest((_, _) => throw new InvalidOperationException("Must not run"));
                    });
                search.AddProductProvider("catalogue");
            });
        });

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([], 92));

        IndexMetadata articles = await fixture.Metadata.GetMetadataAsync("ARTICLES");
        IndexMetadata products = await fixture.Metadata.GetMetadataAsync("catalogue");
        string query = JsonConvert.SerializeObject(fixture.Reader.Invocations[0].Arguments[0]);

        Assert.Equal(HealthStatus.Healthy, articles.HealthStatus);
        Assert.Equal(92, products.DocumentCount);
        Assert.Contains("article", query);
        Assert.DoesNotContain("guide", query);
        Assert.DoesNotContain("product", query);
        Assert.Equal(2, fixture.Reader.Invocations.Count);
    }

    [Fact]
    public async Task ConcurrentRequestsShareOneReadAndRefreshAfterExpiry()
    {
        await using var fixture = new Fixture();
        var pending = new TaskCompletionSource<ContentDetailsCollectionResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .Returns(pending.Task);

        IndexMetadataService metadata = fixture.Metadata;
        Task<IndexMetadata>[] requests =
        [
            .. Enumerable.Range(0, 20)
                .Select(_ => metadata.GetMetadataAsync(SearchExportOperations.IndexAlias))
        ];

        Assert.Single(fixture.Reader.Invocations);
        Assert.Equal(0, fixture.DisposedScopes);
        pending.SetResult(new ContentDetailsCollectionResponse([], 4));
        await Task.WhenAll(requests);
        Assert.All(requests, request => Assert.Equal(4, request.Result.DocumentCount));
        Assert.Equal(1, fixture.DisposedScopes);

        await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);
        Assert.Single(fixture.Reader.Invocations);

        fixture.Clock.Advance();
        await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);
        Assert.Equal(2, fixture.Reader.Invocations.Count);
        Assert.Equal(2, fixture.DisposedScopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrMissingTotalsReturnUnknownAndCacheTheFailure(bool missingTotal)
    {
        await using var fixture = new Fixture();

        if (missingTotal)
        {
            fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], null));
        }
        else
        {
            fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Unauthorized or unavailable"));
        }

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);
        await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);

        Assert.Equal(0, metadata.DocumentCount);
        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Single(fixture.Reader.Invocations);
        Assert.Equal(1, fixture.DisposedScopes);

        fixture.Clock.Advance();
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentDetailsCollectionResponse([], 8));
        IndexMetadata recovered = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);

        Assert.Equal(8, recovered.DocumentCount);
        Assert.Equal(HealthStatus.Healthy, recovered.HealthStatus);
    }

    [Fact]
    public async Task TimeoutCancelsTheReadAndDisposesItsScope()
    {
        await using var fixture = new Fixture();

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .Returns(async (ContentQuery _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return new ContentDetailsCollectionResponse([], 0);
            });

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Equal(1, fixture.DisposedScopes);
    }

    [Fact]
    public async Task UnknownAliasesDoNotQueryOrReuseAnUnrelatedCount()
    {
        await using var fixture = new Fixture();

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync("unknown");

        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Empty(fixture.Reader.Invocations);
    }

    [Fact]
    public async Task FailureInOneEntityTypeDoesNotPublishAPartialTotal()
    {
        await using var fixture = new Fixture(options =>
        {
            options.MapProduct("product");
            options.MapContent("article");
        });

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductDetailsCollectionResponse([], 92));
        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unavailable"));

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);

        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Equal(0, metadata.DocumentCount);
        Assert.Equal(2, fixture.Reader.Invocations.Count);
        Assert.Equal(1, fixture.DisposedScopes);
    }

    [Fact]
    public async Task ShutdownCancelsPendingReadsAndDisposesTheirScope()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
            .Returns(async (ContentQuery _, CancellationToken token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return new ContentDetailsCollectionResponse([], 0);
            });

        Task<IndexMetadata> request = fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);
        await entered.Task;
        await fixture.Stopping.CancelAsync();
        IndexMetadata metadata = await request;

        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Equal(1, fixture.DisposedScopes);
    }

    [Fact]
    public async Task NoExportedMappingsCannotEstablishHealth()
    {
        await using var fixture = new Fixture(options =>
            options.MapContent("article", mapping => mapping.Export = false));

        IndexMetadata metadata = await fixture.Metadata.GetMetadataAsync(SearchExportOperations.IndexAlias);

        Assert.Equal(HealthStatus.Unknown, metadata.HealthStatus);
        Assert.Empty(fixture.Reader.Invocations);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        internal Mock<IDataAccessor> Reader { get; } = new();
        internal TestClock Clock { get; } = new();
        internal CancellationTokenSource Stopping { get; } = new();
        internal IndexMetadataService Metadata { get; }
        internal int DisposedScopes { get; private set; }

        internal Fixture(Action<RelewiseUmbracoOptionsBuilder>? configure = null)
        {
            Reader.Setup(value => value.QueryAsync(It.IsAny<ContentQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContentDetailsCollectionResponse([], 0));
            var clients = new Mock<IRelewiseClientFactory>();

            clients.Setup(value => value.GetClient<IDataAccessor>(Constants.ClientNames.Integration))
                .Returns(Reader.Object);

            var services = new ServiceCollection();

            services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
                (configure ?? (builder => builder.MapContent("article")))(options));
            services.AddSingleton<RelewiseUmbracoConfiguration>();
            services.AddScoped(_ => new ScopeProbe(() => DisposedScopes++));
            services.AddScoped<IRelewiseClientFactory>(provider =>
            {
                provider.GetRequiredService<ScopeProbe>();

                return clients.Object;
            });
            _services = services.BuildServiceProvider(
                new ServiceProviderOptions
                { ValidateScopes = true, ValidateOnBuild = true });
            var lifetime = new Mock<IHostApplicationLifetime>();

            lifetime.SetupGet(value => value.ApplicationStopping).Returns(Stopping.Token);
            Metadata = new IndexMetadataService(
                _services.GetRequiredService<IServiceScopeFactory>(),
                _services.GetRequiredService<RelewiseUmbracoConfiguration>(),
                lifetime.Object,
                NullLogger<IndexMetadataService>.Instance,
                Clock);
        }

        public ValueTask DisposeAsync()
        {
            Stopping.Dispose();

            return _services.DisposeAsync();
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        internal void Advance()
        {
            _now += TimeSpan.FromMinutes(1);
        }
    }

    private sealed class ScopeProbe(Action dispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            dispose();

            return ValueTask.CompletedTask;
        }
    }
}
