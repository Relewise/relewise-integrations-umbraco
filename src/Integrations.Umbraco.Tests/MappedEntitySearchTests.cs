using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Relewise.Client.DataTypes;
using Relewise.Client.DataTypes.Search.Facets.Result;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Search;
using Relewise.Client.Requests.Search.Settings;
using Relewise.Client.Responses.Search;
using Relewise.Integrations.Umbraco.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Faceting;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.Tests;

public class MappedEntitySearchTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ProtectionRequiresAPrincipalAndBypassRetainsSiteMembership(bool product,
        bool bypass,
        bool authenticated)
    {
        FilterCollection? sent = null;
        var sdk = new Mock<Relewise.Client.Search.ISearcher>();

        sdk.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent = request.Filters)
            .ReturnsAsync(new ContentSearchResponse { Hits = 0, Results = [], Facets = new ContentFacetResult() });
        sdk.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent = request.Filters)
            .ReturnsAsync(new ProductSearchResponse { Hits = 0, Results = [], Facets = new ProductFacetResult() });

        var factory = new Mock<IRelewiseClientFactory>();

        factory.Setup(value => value.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(sdk.Object);

        var store = new Mock<IKeyValueService>();

        store.Setup(value => value.GetValue("Relewise:SiteId")).Returns("protected-site");

        var services = new ServiceCollection();

        services.Configure<global::Umbraco.Cms.Core.Configuration.Models.GlobalSettings>(settings =>
            settings.Id = "protected-site");
        services.AddSingleton(factory.Object);
        services.AddSingleton(store.Object);
        services.AddScoped<IRelewiseUserLocator, ScopedVisitor>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.SearchProviders.AddProduct("profile");
            }
            else
            {
                options.MapContent("article");
                options.SearchProviders.AddContent("profile", provider => provider.DocumentTypeAliases = ["ARTICLE"]);
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateScopes = true, ValidateOnBuild = true });
        Guid principal = authenticated ? Guid.NewGuid() : Guid.Empty, group = Guid.NewGuid();
        AccessContext access = bypass ? AccessContext.BypassProtection() : new AccessContext(principal, [group]);

        await root.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync("profile", culture: "DA-dk", accessContext: access);

        var json = JsonConvert.SerializeObject(sent);

        Assert.Contains("protected-site", json);
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
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task RequestCustomizationRetainsIdentityMembershipAndPaging(bool product,
        bool? requirePublishedPage)
    {
        var sent = new ConcurrentBag<PaginatedSearchRequest>();
        var searcher = new Mock<Relewise.Client.Search.ISearcher>();

        searcher.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(new ContentSearchResponse { Hits = 0, Results = [], Facets = new ContentFacetResult() });
        searcher.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent.Add(request))
            .ReturnsAsync(new ProductSearchResponse { Hits = 0, Results = [], Facets = new ProductFacetResult() });

        var clients = new Mock<IRelewiseClientFactory>();

        clients.Setup(factory => factory.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(searcher.Object);

        var store = new Mock<IKeyValueService>();

        store.Setup(service => service.GetValue("Relewise:SiteId")).Returns("test-site");

        var services = new ServiceCollection();

        services.Configure<global::Umbraco.Cms.Core.Configuration.Models.GlobalSettings>(settings =>
            settings.Id = "test-site");
        services.AddSingleton(clients.Object);
        services.AddSingleton(store.Object);
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
            }
        }

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options,
            _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.SearchProviders.AddProduct("catalogue",
                    provider =>
                    {
                        if (requirePublishedPage.HasValue)
                        {
                            provider.RequirePublishedPage = requirePublishedPage.Value;
                        }

                        provider.ConfigureRequest((request, _) => Customize(request));
                    });
            }
            else
            {
                options.MapContent("article");
                options.SearchProviders.AddContent("articles",
                    provider => provider.ConfigureRequest((request, _) => Customize(request)));
                options.SearchProviders.DefaultPublishedContentProviderAlias = "articles";
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateScopes = true, ValidateOnBuild = true });
        var accessor = root.GetRequiredService<IHttpContextAccessor>();
        await using AsyncServiceScope scope = root.CreateAsyncScope();

        accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        User expected = await scope.ServiceProvider.GetRequiredService<IRelewiseUserLocator>().GetUser();
        var alias = product ? "catalogue" : global::Umbraco.Cms.Core.Constants.Search.IndexAliases.PublishedContent;

        try
        {
            await root.GetRequiredService<MappedEntitySearcher>()
                .SearchAsync(alias,
                    "trail",
                    culture: "en-US",
                    skip: 2,
                    take: 3,
                    facets:
                    [
                        new IntegerRangeFacet("number",
                            [new IntegerRangeFacetRange("first", 0, 10), new IntegerRangeFacetRange("second", 10, 20)])
                    ]);
        }
        finally
        {
            accessor.HttpContext = null;
        }

        Assert.Equal(3, sent.Count);
        Assert.Equal(3, configured.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(sent,
            request =>
            {
                Assert.Same(expected, request.User);
                Assert.False(string.IsNullOrWhiteSpace(request.DisplayedAtLocation));
                Assert.NotNull(request.Filters);
                Assert.True(request.Filters.Items!.Count >= 5);

                DataFilter[] predicates = [.. request.Filters.Items.SelectMany(DataFilters)];

                Assert.Equal(!product || requirePublishedPage != false,
                    predicates.Any(filter => filter.Key == "rwUmbracoPublished"));

                foreach (var key in new[]
                         {
                             "rwUmbracoSite", "contentTypeAlias", "rwUmbracoSearchCulture", "rwUmbracoSearchSegments",
                             "rwUmbracoSearchProtected"
                         })
                {
                    Assert.Contains(predicates, filter => filter.Key == key);
                }

                Assert.Null(request.IndexSelector);

                if (request is ProductSearchRequest productRequest)
                {
                    Assert.Equal(0, productRequest.Settings!.VariantRequestSettings!.MaxVariantsPerProduct);
                }

                Assert.Equal("en-us", request.Language!.Value);
            });
        Assert.Single(sent, request => request.Skip == 2 && request.Take == 3);
        Assert.Equal(2, sent.Count(request => request.Skip == 0 && request.Take == 0));
        Assert.All(sent,
            request => Assert.Equal(product ? "Umbraco search: catalogue" : "Umbraco search: articles",
                request.DisplayedAtLocation));
    }

    [Theory]
    [InlineData(false, "tra", 4)]
    [InlineData(true, "tra", 4)]
    [InlineData(true, "tra", 1)]
    [InlineData(true, "   ", 4)]
    [InlineData(false, "tra", 0)]
    public async Task PredictionsRetainConfiguredScopeIdentityAndCancellation(bool product,
        string term,
        int maximum)
    {
        SearchRequest? sent = null;
        SearchTermPredictionRequest? prediction = null;
        var sdk = new Mock<Relewise.Client.Search.ISearcher>();

        sdk.Setup(client => client.SearchAsync(It.IsAny<ContentSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContentSearchRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new ContentSearchResponse { Hits = 0, Results = [], Facets = new ContentFacetResult() });
        sdk.Setup(client => client.SearchAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProductSearchRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new ProductSearchResponse { Hits = 0, Results = [], Facets = new ProductFacetResult() });

        using var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;

        sdk.Setup(client => client.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), cancellationToken))
            .Callback<SearchTermPredictionRequest, CancellationToken>((request, _) => prediction = request)
            .ReturnsAsync(new SearchTermPredictionResponse
            {
                Predictions =
                [
                    new SearchTermPredictionResult { Term = "Trail" },
                    new SearchTermPredictionResult { Term = "trail" },
                    new SearchTermPredictionResult { Term = "Travel" }, new SearchTermPredictionResult { Term = " " }
                ]
            });

        var clients = new Mock<IRelewiseClientFactory>();

        clients.Setup(factory => factory.GetClient<Relewise.Client.Search.ISearcher>(It.IsAny<string>()))
            .Returns(sdk.Object);

        var services = new ServiceCollection();

        services.Configure<global::Umbraco.Cms.Core.Configuration.Models.GlobalSettings>(settings =>
            settings.Id = "prediction-site");
        services.AddSingleton(clients.Object);
        services.AddScoped<IRelewiseUserLocator, ScopedVisitor>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options,
            _) =>
        {
            if (product)
            {
                options.MapProduct("product");
                options.SearchProviders.AddProduct("profile",
                    provider => provider.ConfigureRequest((request,
                        _) =>
                    {
                        request.Currency = new Currency("DKK");
                        request.Filters!.Add(new ProductDataFilter("offer",
                            new Client.Requests.Conditions.EqualsCondition(true)));
                        request.PostFilters = new FilterCollection(new ProductDataFilter("stock",
                            new Client.Requests.Conditions.EqualsCondition(true)));
                    }));
            }
            else
            {
                options.MapContent("article");
                options.SearchProviders.AddContent("profile",
                    provider => provider.ConfigureRequest((request,
                        _) =>
                    {
                        request.Currency = new Currency("DKK");
                        request.Filters!.Add(new ContentDataFilter("offer",
                            new Client.Requests.Conditions.EqualsCondition(true)));
                    }));
            }
        });
        services.AddSingleton<RelewiseUmbracoConfiguration>();
        services.AddTransient<MappedEntitySearcher>();

        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions
        { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        var accessor = root.GetRequiredService<IHttpContextAccessor>();

        accessor.HttpContext = new DefaultHttpContext
        { RequestServices = scope.ServiceProvider, RequestAborted = cancellationToken };

        var principal = Guid.NewGuid();
        SearchResult result = await root.GetRequiredService<MappedEntitySearcher>()
            .SearchAsync("profile",
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
        Assert.Equal(product ? EntityType.Product : EntityType.Content,
            Assert.Single(prediction.Settings!.TargetEntityTypes!));
        Assert.Null(prediction.IndexSelector);

        var json = JsonConvert.SerializeObject(prediction.Filters);

        Assert.Contains("prediction-site", json);
        Assert.Contains("rwUmbracoPublished", json);
        Assert.Contains("rwUmbracoSearchProtected", json);
        Assert.Contains(principal.ToString("D"), json);
        Assert.Contains("da-dk", json);
        Assert.Contains("offer", json);

        if (product)
        {
            Assert.Contains("stock", json);
        }

        sdk.Verify(client => client.PredictAsync(It.IsAny<SearchTermPredictionRequest>(), cancellationToken),
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
