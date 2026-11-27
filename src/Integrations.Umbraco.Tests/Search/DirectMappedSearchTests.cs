using System.Net;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.Extensions;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;
using UFacet = Umbraco.Cms.Core.Search.Querying.Faceting.Facet;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class DirectMappedSearchTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ResultsAndTypedFacetsUseOneHttpRequest(bool product, bool range, bool binary)
    {
        var node = Guid.NewGuid();
        using var handler = new ResponseHandler(product, range, binary, node);
        using var transport = new HttpClient(handler);
        var sdk = new Relewise.Client.Search.Searcher(Guid.NewGuid(), "test-only-key", "https://test.invalid")
        {
            OverriddenHttpClient = transport,
            UseFastBinarySerializer = binary
        };
        await using ServiceProvider services = Services(sdk, product);
        var searcher = services.GetRequiredService<MappedEntitySearcher>();
        UFacet numeric = range
            ? new IntegerRangeFacet(
                "stock",
                [
                    new IntegerRangeFacetRange("low", 0, 20),
                    new IntegerRangeFacetRange("high", 20, null)
                ])
            : new IntegerExactFacet("stock");

        SearchResult result = await searcher.SearchAsync(
            "native",
            "camp",
            culture: "da-DK",
            filters:
            [
                new KeywordFilter("tags", ["camp"], false),
                new KeywordFilter("available", ["true"], false)
            ],
            facets: [new KeywordFacet("tags"), new KeywordFacet("available"), numeric],
            sorters: [new IntegerSorter("stock", Direction.Descending), new ScoreSorter(Direction.Descending)]);

        Assert.Equal(1, handler.Count);
        Assert.Equal(1, result.Total);
        Assert.Equal(node, Assert.Single(result.Documents).Id);
        Assert.Equal(3, result.Facets.Count());
        Assert.Equal(1, Assert.Single(result.Facets.First().Values).Count);
        Assert.Equal(1, result.Facets.Last().Values.Sum(value => value.Count));

        if (!binary)
        {
            Assert.Contains("inventory", handler.RequestBody);
            Assert.DoesNotContain("rwUmbracoField", handler.RequestBody);
            Assert.DoesNotContain("inventory_da", handler.RequestBody);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsupportedOperationsSendNoSearchRequests(bool product)
    {
        var sdk = new Mock<Relewise.Client.Search.ISearcher>(MockBehavior.Strict);
        await using ServiceProvider services = Services(sdk.Object, product);
        var searcher = services.GetRequiredService<MappedEntitySearcher>();

        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            sorters: [new IntegerSorter("sizes", Direction.Ascending)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            filters: [new IntegerRangeFilter("sizes", [new IntegerRangeFilterRange(1, 9)], false)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            facets: [new DateTimeOffsetExactFacet("stock")]));
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            filters: [new TextFilter("tags", ["camp"], false)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            filters: [new KeywordFilter("unknown", ["camp"], false)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            facets: [new IntegerRangeFacet("stock", [])]));

        if (product)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() => searcher.SearchAsync(
                "native",
                culture: "en-US",
                facets: [new DecimalRangeFacet("price", [])]));
        }

        await Assert.ThrowsAsync<ArgumentException>(() => searcher.SearchAsync(
            "native",
            culture: "en-US",
            filters: [new KeywordFilter("available", ["yes"], false)]));

        sdk.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpstreamFailureDoesNotRetryOrRunFacetQueries()
    {
        using var handler = new ResponseHandler(true, false, false, Guid.NewGuid(), HttpStatusCode.ServiceUnavailable);
        using var transport = new HttpClient(handler);
        var sdk = new Relewise.Client.Search.Searcher(Guid.NewGuid(), "test-only-key", "https://test.invalid")
        { OverriddenHttpClient = transport, UseFastBinarySerializer = false };
        await using ServiceProvider services = Services(sdk, true);
        await Assert.ThrowsAsync<HttpRequestException>(() => services.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync("native", culture: "en-US", facets: [new IntegerExactFacet("stock")]));
        Assert.Equal(1, handler.Count);
    }

    private static ServiceProvider Services(Relewise.Client.Search.ISearcher sdk, bool product)
    {
        var clients = new Mock<IRelewiseClientFactory>();
        clients.Setup(factory => factory.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>())).Returns(sdk);
        var services = new ServiceCollection();
        services.AddSingleton(clients.Object);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IRelewiseUserLocator, Visitor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.ConfigureSearch(search =>
                {
                    search.AddProductProvider(
                        "native",
                        fields =>
                        {
                            fields.Map(entity => entity.SalesPrice, "price");
                            fields.MapData<int>("stock", field => field.DataKey = "inventory");
                            fields.MapData<string[]>("tags");
                            fields.MapData<int[]>("sizes");
                            fields.MapData<bool>("available", field => field.DataKey = "inStock");
                        });
                });
            }
            else
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider(
                        "native",
                        fields =>
                        {
                            fields.MapData<int>("stock", field => field.DataKey = "inventory");
                            fields.MapData<string[]>("tags");
                            fields.MapData<int[]>("sizes");
                            fields.MapData<bool>("available", field => field.DataKey = "inStock");
                        });
                });
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class Visitor : IRelewiseUserLocator
    {
        public Task<User> GetUserAsync() => Task.FromResult(User.ByTemporaryId("direct-search-test"));
    }

    private sealed class ResponseHandler(
        bool product,
        bool range,
        bool binary,
        Guid node,
        HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token)
        {
            Count++;
            Assert.EndsWith(
                product ? "/ProductSearchRequest" : "/ContentSearchRequest",
                request.RequestUri!.AbsolutePath);

            if (!binary)
            {
                RequestBody = await request.Content!.ReadAsStringAsync(token);
            }

            var numbers = new List<AvailableFacetValue<double>> { new(20, false, 1) };
            var ranges = new List<AvailableFacetValue<ChainableRange<double?>>>
            {
                new(new ChainableRange<double?>(0, 20), false, 0),
                new(new ChainableRange<double?>(20, null), false, 1)
            };
            var words = new List<AvailableFacetValue<string>> { new("camp", true, 1) };
            var booleans = new List<AvailableFacetValue<bool>> { new(true, true, 1) };
            var data = new Dictionary<string, DataValue> { [RelewiseDataKeys.NodeKey] = node.ToString("D") };
            HttpContent content;

            if (product)
            {
                var response = new ProductSearchResponse
                {
                    Statistics = new Statistics(),
                    Hits = 1,
                    Results = [new ProductResult("sku", 1) { Data = data }],
                    Facets = new ProductFacetResult
                    {
                        Items =
                        [
                            new ProductDataStringValueFacetResult(
                                DataSelectionStrategy.Product,
                                "tags",
                                ["camp"],
                                null,
                                words),
                            new ProductDataBooleanValueFacetResult(
                                DataSelectionStrategy.Product,
                                "inStock",
                                [true],
                                null,
                                booleans),
                            range
                                ? new ProductDataDoubleRangesFacetResult(
                                    "inventory",
                                    DataSelectionStrategy.Product,
                                    null,
                                    [],
                                    ranges)
                                : new ProductDataDoubleValueFacetResult(
                                    DataSelectionStrategy.Product,
                                    "inventory",
                                    [],
                                    null,
                                    numbers)
                        ]
                    }
                };
                content = binary
                    ? new ByteArrayContent(MessagePackSerializer.Serialize(response))
                    : new StringContent(
                        JsonConvert.SerializeObject(
                            response,
                            new JsonSerializerSettings
                            { TypeNameHandling = TypeNameHandling.Auto }));
            }
            else
            {
                var response = new ContentSearchResponse
                {
                    Statistics = new Statistics(),
                    Hits = 1,
                    Results = [new ContentResult("article", 1) { Data = data }],
                    Facets = new ContentFacetResult
                    {
                        Items =
                        [
                            new ContentDataStringValueFacetResult("tags", ["camp"], null, words),
                            new ContentDataBooleanValueFacetResult("inStock", [true], null, booleans),
                            range
                                ? new ContentDataDoubleRangesFacetResult("inventory", null, [], ranges)
                                : new ContentDataDoubleValueFacetResult("inventory", [], null, numbers)
                        ]
                    }
                };
                content = binary
                    ? new ByteArrayContent(MessagePackSerializer.Serialize(response))
                    : new StringContent(
                        JsonConvert.SerializeObject(
                            response,
                            new JsonSerializerSettings
                            { TypeNameHandling = TypeNameHandling.Auto }));
            }

            return new HttpResponseMessage(status) { Content = content };
        }
    }
}
