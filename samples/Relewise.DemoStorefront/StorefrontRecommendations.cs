using System.Text.Json;
using Microsoft.Extensions.Options;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Conditions;
using Relewise.Client.Requests.Filters;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Shared;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

// Recommendations use the SDK directly; catalogue search continues through Umbraco's abstraction.
internal static class StorefrontRecommendations
{
    internal const int VisibleCount = 4;

    internal static void MapRecommendations(this WebApplication app)
    {
        app.MapGet("/{language:regex(^(en|da)$)}/recommendations/{key:guid}", GetAsync);
    }

    internal static string Placeholder(IPublishedContent page, HttpContext http)
    {
        var configuration = Configuration(page, http);
        if (configuration == null)
        {
            return string.Empty;
        }

        string json = JsonSerializer.Serialize(configuration, StorefrontChrome.Json);
        return $"<div id='product-recommendations'></div><script id='recommendation-data' type='application/json'>{json}</script>";
    }

    internal static RecommendationModel? Configuration(IPublishedContent page, HttpContext http, bool emptyCart = false)
    {
        if (!DemoConnection.IsConfigured(http))
        {
            return null;
        }

        string[] keys = ["SimilarProducts", "RecentlyViewedProducts", "PopularProducts", "PopularCategories", "LoadingRecommendations", "Equipment", "FieldNotes", "OnOffer", "ReadStory"];
        return new RecommendationModel
        {
            Endpoint = DemoMarket.Current(http).LanguagePath + "/recommendations/" + page.Key + (emptyCart ? "?placement=cart" : ""),
            Labels = keys.ToDictionary(key => key, key => Storefront.Text(http, key))
        };
    }

    private static async Task<IResult> GetAsync(HttpContext http, Guid key)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (DemoConnection.RequireConnection(http, json: true) is { } blocked)
        {
            return blocked;
        }

        var market = DemoMarket.Current(http);
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext = new VariationContext(market.Culture);
        using var context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        var published = context.UmbracoContext.Content!;
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        var home = StorefrontPages.Home(http.RequestServices);
        bool Available(IPublishedContent page) => page.Root().Key == home.Key &&
            page.Cultures.ContainsKey(market.Culture) && !access.IsProtected(page.Path).Success;
        IPublishedContent? page = published.GetById(key);
        if (page == null || page.ContentType.Alias is not ("home" or "product" or "productVariant") || !Available(page))
        {
            return Results.NotFound();
        }

        IPublishedContent product = page.ContentType.Alias == "productVariant" ? page.Parent()! : page;
        IPublishedContent? category = product.Parent();
        bool isHome = page.ContentType.Alias == "home";
        if (!Available(product) || !isHome && category?.ContentType.Alias != "productCategory")
        {
            return Results.NotFound();
        }

        string Id(IPublishedContent content) => DemoConfiguration.EntityId(content, http.RequestServices);
        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUser();
        string site = http.RequestServices.GetRequiredService<IOptions<GlobalSettings>>().Value.Id!;
        string? productType = product.Value<string>("productType");
        bool emptyCart = isHome && http.Request.Query["placement"] == "cart";
        bool similarEnabled = !isHome && !string.IsNullOrWhiteSpace(productType);
        var batch = !similarEnabled
            ? new ProductRecommendationRequestCollection(false, CreateRecentlyViewed(user, market, site, isHome ? null : Id(product),
                emptyCart ? "Empty cart: Recently viewed" : null))
            : CreateRequests(user, market, site, Id(product), page == product ? null : Id(page), Id(category!), productType!);
        if (isHome && !emptyCart)
        {
            batch = new ProductRecommendationRequestCollection(false,
                CreateRecentlyViewed(user, market, site), CreatePopularProducts(user, market, site));
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var client = http.RequestServices.GetRequiredService<IRelewiseClientFactory>().GetClient<IRecommender>();
            var response = await client.RecommendAsync(batch, timeout.Token);
            var responses = response.Responses ?? [];

            ListingCard[] Cards(IEnumerable<ProductResult> results, bool requireStock = false, bool requireSimilarity = false)
            {
                var cards = new List<ListingCard>();
                var seen = new HashSet<Guid>();
                foreach (var result in results)
                {
                    if (result.Data?.TryGetValue("rwUmbracoNodeKey", out var data) != true || data == null ||
                        !Guid.TryParse(data.ValueAs<string>(), out Guid node) || node == product.Key || !seen.Add(node))
                    {
                        continue;
                    }

                    IPublishedContent? candidate = published.GetById(node);
                    if (candidate?.ContentType.Alias != "product" || !Available(candidate) ||
                        !string.Equals(Id(candidate), result.ProductId, StringComparison.OrdinalIgnoreCase) ||
                        !HasPrice(candidate, market) || requireStock && candidate.Value<int>("stock") <= 0 ||
                        requireSimilarity && (candidate.Parent()?.Key != category!.Key ||
                            !string.Equals(candidate.Value<string>("productType"), productType, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    cards.Add(StorefrontPages.CardData(candidate, http));
                    if (cards.Count == VisibleCount)
                    {
                        break;
                    }
                }
                return cards.ToArray();
            }

            var similar = similarEnabled
                ? Cards(responses.ElementAtOrDefault(0)?.Recommendations ?? [], requireStock: true, requireSimilarity: true)
                : [];
            var recentlyViewed = Cards(responses.ElementAtOrDefault(similarEnabled ? 1 : 0)?.Recommendations ?? []);
            ListingCard[] popular = isHome && !emptyCart
                ? Cards(responses.ElementAtOrDefault(1)?.Recommendations ?? [], requireStock: true)
                : [];
            if (emptyCart && recentlyViewed.Length == 0)
            {
                // Request the fallback only when no visible history remains, keeping analytics meaningful.
                var fallback = await client.RecommendAsync(CreatePopularProducts(user, market, site, emptyCart: true), timeout.Token);
                popular = Cards(fallback.Recommendations ?? [], requireStock: true);
            }

            var categories = new List<CategoryRecommendationCard>();
            if (isHome && !emptyCart)
            {
                var candidates = StorefrontPages.ShoppingCategories(home).Where(Available)
                    .ToDictionary(Id, StringComparer.OrdinalIgnoreCase);
                if (candidates.Count > 0)
                {
                    try
                    {
                        var categoryResponse = await client.RecommendAsync(CreatePopularCategories(user, market, candidates.Keys), timeout.Token);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var result in categoryResponse.Recommendations ?? [])
                        {
                            if (!seen.Add(result.CategoryId) || !candidates.TryGetValue(result.CategoryId, out var candidate))
                            {
                                continue;
                            }

                            var example = candidate.Descendants().FirstOrDefault(node => node.ContentType.Alias == "product" &&
                                Available(node) && node.Value<int>("stock") > 0 && HasPrice(node, market));
                            if (example != null)
                            {
                                categories.Add(new CategoryRecommendationCard(candidate.Key, Id(candidate), candidate.Url(market.Culture),
                                    StorefrontPages.Title(candidate), Storefront.Image(example)));
                            }
                            if (categories.Count == VisibleCount)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception error) when (DemoConnection.IsConnectionFailure(error, http.RequestAborted))
                    {
                        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("StorefrontRecommendations")
                            .LogWarning(error, "Unable to load optional category recommendations.");
                    }
                }
            }
            return Results.Json(new { similar, recentlyViewed, popular, categories });
        }
        catch (Exception error) when (DemoConnection.IsConnectionFailure(error, http.RequestAborted))
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("StorefrontRecommendations")
                .LogWarning(error, "Unable to load optional Product recommendations.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    internal static ProductRecommendationRequestCollection CreateRequests(User user, DemoMarket market, string site, string productId, string? variantId, string categoryId, string productType)
    {
        var similar = new SimilarProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            "Product page: Similar products",
            user,
            new ProductAndVariantId(productId, variantId),
            new SimilarProductsEvaluationSettings
            {
                SignificanceOfSimilaritiesInDisplayName = 4,
                SignificanceOfSimilarListPrice = 0.15,
                SignificanceOfSimilarSalesPrice = 0.15,
                SignificanceOfCommonImmediateParentCategories = 0,
                SignificanceOfCommonParentsParentCategories = 0,
                SignificanceOfCommonAncestorCategories = 0,
                SignificanceOfSimilarBrand = 0,
                SignificanceOfIdenticalProductDataValues = 0,
                SignificantProductDataFields =
                [
                    new("capacity", SignificantDataValue.SignificantDataValueComparer.StringSimilarity),
                    new("weightGrams", SignificantDataValue.SignificantDataValueComparer.NumericPercentDifference, 0.25)
                ]
            });
        similar.Filters.Add(new ProductDataFilter("productType", new EqualsCondition(productType)));
        similar.Filters.Add(new ProductCategoryIdFilter(categoryId, CategoryScope.ImmediateParent));
        similar.Filters.Add(new ProductDataFilter("inStock", new EqualsCondition(true)));
        ConfigureRequest(similar, market, site, productId);
        // The sections serve different purposes; an actual recently viewed item may also be a similar Product.
        return new ProductRecommendationRequestCollection(false, similar, CreateRecentlyViewed(user, market, site, productId));
    }

    internal static RecentlyViewedProductsRequest CreateRecentlyViewed(User user, DemoMarket market, string site, string? excludedProductId = null, string? displayedAtLocation = null)
    {
        var request = new RecentlyViewedProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            displayedAtLocation ?? (excludedProductId == null ? "Home page: Recently viewed" : "Product page: Recently viewed"),
            user);
        ConfigureRequest(request, market, site, excludedProductId);
        return request;
    }

    internal static PopularProductsRequest CreatePopularProducts(User user, DemoMarket market, string site, bool emptyCart = false)
    {
        var request = new PopularProductsRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            emptyCart ? "Empty cart: Popular products" : "Home page: Popular products",
            user,
            PopularityTypes.MostViewed,
            TimeSpan.FromDays(30));
        ConfigureRequest(request, market, site, excludedProductId: null);
        request.Filters.Add(new ProductDataFilter("inStock", new EqualsCondition(true)));
        return request;
    }

    internal static PopularProductCategoriesRecommendationRequest CreatePopularCategories(User user, DemoMarket market, IEnumerable<string> categoryIds)
    {
        var request = new PopularProductCategoriesRecommendationRequest(
            new Language(market.Culture),
            new Currency(market.Currency),
            "Home page: Popular categories",
            user,
            new ProductCategoryRecommendationWeights(categoryViews: 1, productViews: 1, productPurchases: 0),
            TimeSpan.FromDays(30));
        // Only the published browsing categories on this site may be recommended.
        request.Filters.Add(new ProductCategoryIdFilter(categoryIds, CategoryScope.ImmediateParent));
        request.Settings.NumberOfRecommendations = VisibleCount;
        request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations = false;
        request.Settings.AllowReplacingOfRecentlyShownRecommendations = false;
        return request;
    }

    private static void ConfigureRequest(ProductRecommendationRequest request, DemoMarket market, string site, string? excludedProductId)
    {
        if (excludedProductId != null)
        {
            // Exclude the current Product, including its other Variants.
            request.Filters.Add(new ProductIdFilter(excludedProductId, negated: true));
        }

        request.Filters.Add(new ProductDataFilter("rwUmbracoSite", new EqualsCondition(site)));
        request.Filters.Add(new ProductDataFilter("contentTypeAlias", new EqualsCondition("product")));
        request.Filters.Add(new ProductDataFilter("rwUmbracoPublished", new EqualsCondition(true)));
        request.Filters.Add(new ProductDataFilter("rwUmbracoSearchProtected", new EqualsCondition(false)));
        request.Filters.Add(new ProductDataFilter("rwUmbracoSearchCulture", new ContainsCondition(market.Culture.ToLowerInvariant())));
        request.Settings.NumberOfRecommendations = VisibleCount;
        // Keep browsing history genuine: no fill or novelty replacement.
        request.Settings.AllowFillIfNecessaryToReachNumberOfRecommendations = false;
        request.Settings.AllowReplacingOfRecentlyShownRecommendations = false;
        request.Settings.AllowProductsCurrentlyInCart = true;
        request.Settings.VariantRequestSettings = new() { MaxVariantsPerProduct = 0 };
        request.Settings.SelectedProductProperties = new() { DataKeys = ["rwUmbracoNodeKey"] };
    }

    private static bool HasPrice(IPublishedContent product, DemoMarket market) =>
        PriceConverter.Read(product.Value("salesPrice"))?.Values?.Any(value => value.Currency.Value.Equals(market.Currency, StringComparison.OrdinalIgnoreCase)) == true;
}
