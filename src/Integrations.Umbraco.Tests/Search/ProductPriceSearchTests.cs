using System.Net;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Queries;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.DataTypes.Search.Sorting.Product;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Search.Querying.Sorting;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class ProductPriceSearchTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("DKK")]
    public async Task ResultsPriceAndValueFacetsUseOneSearchInTheConfiguredCurrency(string currency)
    {
        ProductSearchRequest? sent = null;
        var client = new Mock<Relewise.Client.Search.ISearcher>(MockBehavior.Strict);
        var nodeKey = Guid.NewGuid();
        client.Setup(value => value.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(
                new ProductSearchResponse
                {
                    Hits = 1,
                    Results =
                    [
                        new ProductResult("sku-1", 1)
                        {
                            Data = new Dictionary<string, DataValue>
                                { [RelewiseDataKeys.NodeKey] = nodeKey.ToString("D") }
                        }
                    ],
                    Facets = new ProductFacetResult
                    {
                        Items =
                        [
                            new PriceRangesFacetResult(
                                FacetingField.SalesPrice,
                                PriceSelectionStrategy.Product,
                                null,
                                [],
                                [
                                    new AvailableFacetValue<ChainableRange<decimal?>>(
                                        new ChainableRange<decimal?>(null, 50),
                                        false,
                                        3),
                                    new AvailableFacetValue<ChainableRange<decimal?>>(
                                        new ChainableRange<decimal?>(50, null),
                                        false,
                                        7)
                                ]),
                            new ProductDataStringValueFacetResult(
                                DataSelectionStrategy.Product,
                                "category",
                                ["a"],
                                CollectionFilterType.Or,
                                [new AvailableFacetValue<string>("a", true, 1)])
                        ]
                    }
                });
        var factory = new Mock<IRelewiseClientFactory>();
        factory.Setup(value => value.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(client.Object);
        var services = new ServiceCollection();
        services.AddSingleton(factory.Object);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IRelewiseUserLocator, Visitor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.MapProduct("product");
            options.ConfigureSearch(search =>
            {
                search.AddProductProvider(
                    "products",
                    provider =>
                    {
                        provider.Map(product => product.SalesPrice, "price");
                        provider.MapData<string[]>("category");
                        provider.ConfigureRequest((request, _) =>
                        {
                            request.Currency = new Currency(currency);
                            request.Filters = null;
                            request.Facets = null;
                        });
                    });
            });
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();
        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        SearchResult result = await root.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync(
                "products",
                "camp",
                culture: "en-US",
                skip: 2,
                take: 3,
                filters:
                [
                    new DecimalRangeFilter("price", [new DecimalRangeFilterRange(10, 50)], false),
                    new KeywordFilter("category", ["a"], false)
                ],
                facets:
                [
                    new KeywordFacet("category"),
                    new DecimalRangeFacet(
                        "price",
                        [new DecimalRangeFacetRange("low", null, 50), new DecimalRangeFacetRange("high", 50, null)])
                ],
                sorters: [new DecimalSorter("price", Direction.Ascending), new ScoreSorter(Direction.Descending)]);

        Assert.NotNull(sent);
        Assert.Equal(currency, sent.Currency!.Value);
        Assert.Equal(2, sent.Skip);
        Assert.Equal(3, sent.Take);
        Assert.Equal(2, sent.Facets!.Items.Count);
        var price = Assert.IsType<PriceRangesFacet>(sent.Facets.Items[1]);
        Assert.Equal(10, Assert.Single(price.Selected!).LowerBoundInclusive);
        Assert.Equal(50, Assert.Single(price.Selected!).UpperBoundExclusive);
        var sorting = Assert.IsType<ProductAttributeSorting>(sent.Sorting!.Value);
        Assert.Equal(ProductAttributeSorting.SortableAttribute.SalesPrice, sorting.Attribute);
        Assert.IsType<ProductRelevanceSorting>(sorting.ThenBy);
        string filters = JsonConvert.SerializeObject(sent.Filters);
        Assert.DoesNotContain("rwUmbracoField_price", filters);
        Assert.Contains("rwUmbracoPublished", filters);
        Assert.Contains("rwUmbracoSearchProtected", filters);
        Assert.Equal(nodeKey, Assert.Single(result.Documents).Id);
        Assert.Equal(new[] { "category", "price" }, result.Facets.Select(facet => facet.FieldName));
        Assert.Equal(new long[] { 3, 7 }, result.Facets.Last().Values.Select(value => value.Count));
        client.Verify(
            value => value.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SdkTransportSendsOneHttpRequestForResultsAndPriceFacets(bool binary)
    {
        var response = new ProductSearchResponse
        {
            Statistics = new Statistics(),
            Hits = 3,
            Results = [],
            Facets = new ProductFacetResult
            {
                Items =
                [
                    new PriceRangesFacetResult(
                        FacetingField.SalesPrice,
                        PriceSelectionStrategy.Product,
                        null,
                        [],
                        [
                            new AvailableFacetValue<ChainableRange<decimal?>>(
                                new ChainableRange<decimal?>(null, 50),
                                false,
                                3)
                        ])
                ]
            }
        };
        using var handler = new SearchResponseHandler(response, binary);
        using var http = new HttpClient(handler);
        var client = new Relewise.Client.Search.Searcher(Guid.NewGuid(), "test-only-key", "https://test.invalid")
        {
            OverriddenHttpClient = http,
            UseFastBinarySerializer = binary
        };
        var request = new ProductSearchRequest(
            new Language("en-US"),
            new Currency("EUR"),
            User.Anonymous(),
            "test",
            "camp",
            0,
            10);
        var prices = new ProductPriceQuery(
            "price",
            [],
            [new DecimalRangeFacet("price", [new DecimalRangeFacetRange("low", null, 50)])],
            []);
        prices.Apply(request);

        ProductSearchResponse result = await client.SearchAsync(request);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(3, result.Hits);
        Assert.Equal(
            3,
            Assert.Single(
                    Assert.IsType<PriceRangesFacetResult>(
                            Assert.Single(result.Facets!.Items))
                        .Available)
                .Hits);
        Assert.Equal(binary, client.UseFastBinarySerializer);
    }

    [Fact]
    public void NonFacetPricePredicatesPreserveExclusiveBoundsAndNegation()
    {
        var filter = new DecimalRangeFilter("price", [new DecimalRangeFilterRange(10.001m, 20.002m)], true);
        var prices = new ProductPriceQuery("price", [filter], [], []);
        var predicate = Assert.IsType<OrFilter>(prices.MapFilter(filter, new Currency("EUR")));
        Assert.True(predicate.Negated);
        var range = Assert.IsType<AndFilter>(Assert.Single(predicate.Filters));
        ProductSalesPriceFilter[] bounds = [.. range.Filters.Cast<ProductSalesPriceFilter>()];
        Assert.Equal(10.001m, bounds[0].Range.LowerBoundInclusive);
        Assert.False(bounds[0].Negated);
        Assert.Equal(20.002m, bounds[1].Range.LowerBoundInclusive);
        Assert.True(bounds[1].Negated);
        Assert.All(bounds, bound => Assert.Null(bound.Range.UpperBoundInclusive));
    }

    [Fact]
    public void UnsupportedPriceOperationsAndMissingCurrencyFailExplicitly()
    {
        Assert.Throws<NotSupportedException>(() => new ProductPriceQuery(
            "price",
            [new IntegerExactFilter("price", [10], false)],
            [],
            []));
        Assert.Throws<NotSupportedException>(() => new ProductPriceQuery(
            "price",
            [],
            [new DecimalExactFacet("price")],
            []));
        Assert.Throws<NotSupportedException>(() => new ProductPriceQuery(
            "price",
            [],
            [],
            [new KeywordSorter("price", Direction.Ascending)]));
        var query = new ProductPriceQuery(
            "price",
            [new DecimalRangeFilter("price", [new DecimalRangeFilterRange(10, 20)], false)],
            [],
            []);
        var request = new ProductSearchRequest(new Language("en-US"), null, User.Anonymous(), "test", null, 0, 10);
        Assert.Throws<InvalidOperationException>(() => query.Apply(request));
    }

    [Fact]
    public void OverlappingPriceBucketsAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => new ProductPriceQuery(
            "price",
            [],
            [
                new DecimalRangeFacet(
                    "price",
                    [new DecimalRangeFacetRange("first", 0, 100), new DecimalRangeFacetRange("second", 50, 150)])
            ],
            []));
    }

    [Fact]
    public void MissingFacetResponseIsNotPresentedAsZeroCounts()
    {
        var query = new ProductPriceQuery(
            "price",
            [],
            [new DecimalRangeFacet("price", [new DecimalRangeFacetRange("low", null, 50)])],
            []);

        Assert.Throws<InvalidOperationException>(() => query.Read(new ContentFacetResult()));
    }

    [Fact]
    public async Task FailedSearchDoesNotRetryOrIssueFacetRequests()
    {
        using var handler = new SearchResponseHandler(
            new ProductSearchResponse(),
            true,
            HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler);
        var client = new Relewise.Client.Search.Searcher(Guid.NewGuid(), "test-only-key", "https://test.invalid")
        {
            OverriddenHttpClient = http
        };
        var request = new ProductSearchRequest(
            new Language("en-US"),
            new Currency("EUR"),
            User.Anonymous(),
            "test",
            "camp",
            0,
            10);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync(request));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void NativeAndDataBindingsCannotShareAQueryName()
    {
        var options = new ProductSearchProviderOptions();
        options.Map(product => product.SalesPrice, "price");
        Assert.Throws<InvalidOperationException>(() => options.MapData<bool>("price"));
        Assert.Throws<InvalidOperationException>(() => options.Map(product => product.DisplayName, "price"));
    }

    private sealed class SearchResponseHandler(
        ProductSearchResponse response,
        bool binary,
        HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.EndsWith("/ProductSearchRequest", request.RequestUri!.AbsolutePath);
            RequestCount++;
            HttpContent content = binary
                ? new ByteArrayContent(MessagePackSerializer.Serialize(response))
                : new StringContent(
                    JsonConvert.SerializeObject(
                        response,
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }));

            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    private sealed class Visitor : IRelewiseUserLocator
    {
        public Task<User> GetUserAsync()
        {
            return Task.FromResult(User.Anonymous());
        }
    }
}
