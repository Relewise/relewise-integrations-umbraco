using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Enums;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
#if !UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
#endif
#if UMBRACO_SEARCH_ADDON
using Umbraco.Cms.Search.Core.Models.Searching;
using Umbraco.Cms.Search.Core.Models.Searching.Faceting;
#endif
#if !UMBRACO_SEARCH_ADDON
using SearchConstants = Umbraco.Cms.Core.Constants.Search;
#endif
#if UMBRACO_SEARCH_ADDON
using SearchConstants = Umbraco.Cms.Search.Core.Constants;
#endif

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class MappedEntitySearchTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ProtectionRequiresAPrincipalAndBypassRetainsMappingMembership(
        bool product,
        bool bypass,
        bool authenticated)
    {
        FilterCollection? sent = null;
        var sdk = new Mock<Relewise.Client.Search.ISearcher>();

        sdk.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent = request.Filters)
            .ReturnsAsync(
                new ContentSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ContentFacetResult
                    { Items = [new ContentDataDoubleValueFacetResult("number", [], null, [])] }
                });
        sdk.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent = request.Filters)
            .ReturnsAsync(
                new ProductSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ProductFacetResult
                    {
                        Items =
                        [
                            new ProductDataDoubleValueFacetResult(
                                DataSelectionStrategy.Product,
                                "number",
                                [],
                                null,
                                [])
                        ]
                    }
                });

        var factory = new Mock<IRelewiseClientFactory>();

        factory.Setup(value => value.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(sdk.Object);

        var services = new ServiceCollection();

        services.AddSingleton(factory.Object);
        services.AddScoped<IRelewiseUserLocator, ScopedVisitor>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.ConfigureSearch(search =>
                {
                    search.AddProductProvider("profile");
                });
            }
            else
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider("profile", provider => provider.DocumentTypeAliases = ["ARTICLE"]);
                });
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        Guid principal = authenticated ? Guid.NewGuid() : Guid.Empty, group = Guid.NewGuid();
        AccessContext access = bypass ? AccessContext.BypassProtection() : new AccessContext(principal, [group]);

        await root.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync("profile", culture: "DA-dk", accessContext: access);

        string json = JsonConvert.SerializeObject(sent);

        Assert.DoesNotContain("rwUmbracoSite", json);
        Assert.Contains("contentTypeAlias", json);
        Assert.Contains("rwUmbracoPublished", json);
        Assert.Contains(product ? "product" : "article", json);
        Assert.Contains("da-dk", json);
        Assert.Equal(!bypass, json.Contains("rwUmbracoSearchProtected"));
        Assert.Equal(!bypass && authenticated, json.Contains(group.ToString("D")));

        if (authenticated && !bypass)
        {
            Assert.Contains(principal.ToString("D"), json);
        }
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(false, null, "Configured location")]
    [InlineData(true, null, null)]
    [InlineData(true, null, "Configured location")]
    [InlineData(true, true, null)]
    [InlineData(true, true, "Configured location")]
    [InlineData(true, false, null)]
    [InlineData(true, false, "Configured location")]
    public async Task RequestCustomizationRetainsIdentityMembershipAndPaging(
        bool product,
        bool? requirePublishedPage,
        string? displayedAtLocation)
    {
        var sent = new ConcurrentBag<PaginatedSearchRequest>();
        var searcher = new Mock<Relewise.Client.Search.ISearcher>();

        searcher.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(
                new ContentSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ContentFacetResult
                    { Items = [new ContentDataDoubleValueFacetResult("number", [], null, [])] }
                });
        searcher.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(
                new ProductSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ProductFacetResult
                    {
                        Items =
                        [
                            new ProductDataDoubleValueFacetResult(
                                DataSelectionStrategy.Product,
                                "number",
                                [],
                                null,
                                [])
                        ]
                    }
                });

        var clients = new Mock<IRelewiseClientFactory>();

        clients.Setup(factory => factory.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(searcher.Object);

        var services = new ServiceCollection();

        services.AddSingleton(clients.Object);
        services.AddScoped<IRelewiseUserLocator, ScopedVisitor>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        var configured = new ConcurrentBag<SearchRequest>();

        void Customize(PaginatedSearchRequest request)
        {
            configured.Add(request);
            request.User = User.ByAuthenticatedId("incorrect");
            request.Skip = 999;
            request.Take = 999;
            request.Filters = null;
            request.DisplayedAtLocation = " ";

            if (request is ProductSearchRequest productRequest)
            {
                productRequest.Settings = new ProductSearchSettings();
                productRequest.Facets = null;
            }
            else if (request is ContentSearchRequest contentRequest)
            {
                contentRequest.Facets = null;
            }
        }

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((
            options,
            _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.ConfigureSearch(search =>
                {
                    search.AddProductProvider(
                        "catalogue",
                        provider =>
                        {
                            if (requirePublishedPage.HasValue)
                            {
                                provider.RequirePublishedPage = requirePublishedPage.Value;
                            }

                            provider.DisplayedAtLocation = displayedAtLocation;
                            provider.MapData<int>("number");
                            provider.ConfigureRequest((request, _) => Customize(request));
                        });
                });
            }
            else
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider(
                        "articles",
                        provider =>
                        {
                            provider.DisplayedAtLocation = displayedAtLocation;
                            provider.MapData<int>("number");
                            provider.ConfigureRequest((request, _) => Customize(request));
                        });
                    search.DefaultPublishedContentProviderAlias = "articles";
                });
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        var accessor = root.GetRequiredService<IHttpContextAccessor>();
        await using AsyncServiceScope scope = root.CreateAsyncScope();

        accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        User expected = await scope.ServiceProvider.GetRequiredService<IRelewiseUserLocator>().GetUserAsync();
        string alias = product ? "catalogue" : SearchConstants.IndexAliases.PublishedContent;

        try
        {
            await root.GetRequiredService<MappedEntitySearcher>()
                .SearchAsync(
                    alias,
                    "trail",
                    culture: "en-US",
                    skip: 2,
                    take: 3,
                    facets:
                    [
                        new IntegerExactFacet("number")
                    ]);
        }
        finally
        {
            accessor.HttpContext = null;
        }

        Assert.Single(sent);
        Assert.Single(configured);
        Assert.All(
            sent,
            request =>
            {
                Assert.Same(expected, request.User);
                Assert.False(string.IsNullOrWhiteSpace(request.DisplayedAtLocation));
                Assert.NotNull(request.Filters);
                Assert.True(request.Filters.Items!.Count >= 3);

                DataFilter[] predicates = [.. request.Filters.Items.SelectMany(DataFilters)];

                Assert.Equal(
                    !product || requirePublishedPage != false,
                    predicates.Any(filter => filter.Key == "rwUmbracoPublished"));

                foreach (string key in new[]
                         {
                             "contentTypeAlias", "rwUmbracoSearchCulture",
                             "rwUmbracoSearchProtected"
                         })
                {
                    Assert.Contains(predicates, filter => filter.Key == key);
                }

                Assert.Null(request.IndexSelector);

                if (request is ProductSearchRequest productRequest)
                {
                    Assert.Equal(0, productRequest.Settings!.VariantRequestSettings!.MaxVariantsPerProduct);
                    Assert.Single(productRequest.Facets!.Items);
                }
                else
                {
                    Assert.Single(Assert.IsType<ContentSearchRequest>(request).Facets!.Items);
                }

                Assert.Equal("en-us", request.Language!.Value);
            });
        Assert.Single(sent, request => request.Skip == 2 && request.Take == 3);
        Assert.DoesNotContain(sent, request => request.Skip == 0 && request.Take == 0);
        Assert.All(
            sent,
            request => Assert.Equal(
                displayedAtLocation ?? (product ? "Umbraco search: catalogue" : "Umbraco search: articles"),
                request.DisplayedAtLocation));
    }

    [Theory]
    [InlineData(false, "tra", 4)]
    [InlineData(true, "tra", 4)]
    [InlineData(true, "tra", 1)]
    [InlineData(true, "   ", 4)]
    [InlineData(false, "tra", 0)]
    public async Task PredictionsRetainConfiguredScopeIdentityAndCancellation(
        bool product,
        string term,
        int maximum)
    {
        SearchRequest? sent = null;
        SearchTermPredictionRequest? prediction = null;
        var sdk = new Mock<Relewise.Client.Search.ISearcher>();

        sdk.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(
                new ContentSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ContentFacetResult
                    { Items = [new ContentDataDoubleValueFacetResult("number", [], null, [])] }
                });
        sdk.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(
                new ProductSearchResponse
                {
                    Hits = 0,
                    Results = [],
                    Facets = new ProductFacetResult
                    {
                        Items =
                        [
                            new ProductDataDoubleValueFacetResult(
                                DataSelectionStrategy.Product,
                                "number",
                                [],
                                null,
                                [])
                        ]
                    }
                });

        using var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;

        sdk.Setup(client => client.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), token))
            .Callback<SearchTermPredictionRequest, CancellationToken>((request, _) => prediction = request)
            .ReturnsAsync(
                new SearchTermPredictionResponse
                {
                    Predictions =
                    [
                        new SearchTermPredictionResult { Term = "Trail" },
                        new SearchTermPredictionResult { Term = "trail" },
                        new SearchTermPredictionResult { Term = "Travel" },
                        new SearchTermPredictionResult { Term = " " }
                    ]
                });

        var clients = new Mock<IRelewiseClientFactory>();

        clients.Setup(factory => factory.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(sdk.Object);

        var services = new ServiceCollection();

        services.AddSingleton(clients.Object);
        services.AddScoped<IRelewiseUserLocator, ScopedVisitor>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((
            options,
            _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.ConfigureSearch(search =>
                {
                    search.AddProductProvider(
                        "profile",
                        provider => provider.ConfigureRequest((
                            request,
                            _) =>
                        {
                            request.Currency = new Currency("DKK");
                            request.Filters!.Add(
                                new ProductDataFilter(
                                    "offer",
                                    new Client.Requests.Conditions.EqualsCondition(true)));
                            request.PostFilters = new FilterCollection(
                                new ProductDataFilter(
                                    "stock",
                                    new Client.Requests.Conditions.EqualsCondition(true)));
                        }));
                });
            }
            else
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider(
                        "profile",
                        provider => provider.ConfigureRequest((
                            request,
                            _) =>
                        {
                            request.Currency = new Currency("DKK");
                            request.Filters!.Add(
                                new ContentDataFilter(
                                    "offer",
                                    new Client.Requests.Conditions.EqualsCondition(true)));
                        }));
                });
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        var accessor = root.GetRequiredService<IHttpContextAccessor>();

        accessor.HttpContext = new DefaultHttpContext
        { RequestServices = scope.ServiceProvider, RequestAborted = token };

        var principal = Guid.NewGuid();
        SearchResult result = await root.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync(
                "profile",
                term,
                culture: "da-DK",
                accessContext: new AccessContext(principal, []),
                maxSuggestions: maximum);

        if (maximum == 0 || string.IsNullOrWhiteSpace(term))
        {
            Assert.Null(prediction);
            Assert.Empty(result.Suggestions!);

            return;
        }

        Assert.NotNull(prediction);
        Assert.Equal(new[] { "Trail", "Travel" }.Take(maximum), result.Suggestions);
        Assert.Equal(term, prediction.Term);
        Assert.Equal(maximum, prediction.Take);
        Assert.Same(sent!.User, prediction.User);
        Assert.Equal(sent.Currency, prediction.Currency);
        Assert.Equal(sent.Language, prediction.Language);
        Assert.Equal(sent.DisplayedAtLocation, prediction.DisplayedAtLocation);
        Assert.Equal(
            product ? EntityType.Product : EntityType.Content,
            Assert.Single(prediction.Settings!.TargetEntityTypes!));
        Assert.Null(prediction.IndexSelector);

        string json = JsonConvert.SerializeObject(prediction.Filters);

        Assert.DoesNotContain("rwUmbracoSite", json);
        Assert.Contains("rwUmbracoPublished", json);
        Assert.Contains("rwUmbracoSearchProtected", json);
        Assert.Contains(principal.ToString("D"), json);
        Assert.Contains("da-dk", json);
        Assert.Contains("offer", json);

        if (product)
        {
            Assert.Contains("stock", json);
        }

        sdk.Verify(
            client => client.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), token),
            Times.Once);
    }

    private static IEnumerable<DataFilter> DataFilters(Filter filter)
    {
        return filter switch
        {
            DataFilter data => [data],
            AndFilter and => and.Filters.SelectMany(DataFilters),
            OrFilter or => or.Filters.SelectMany(DataFilters),
            _ => []
        };
    }

    private sealed class ScopedVisitor : IRelewiseUserLocator
    {
        private readonly User _user = User.ByTemporaryId(Guid.NewGuid().ToString("N"));

        public Task<User> GetUser()
        {
            return Task.FromResult(_user);
        }
    }
}
