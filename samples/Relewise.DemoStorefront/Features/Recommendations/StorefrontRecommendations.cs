using Microsoft.Extensions.Options;
using Relewise.Client;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Responses;
using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using static Relewise.DemoStorefront.Features.Recommendations.RecommendationRequests;

namespace Relewise.DemoStorefront.Features.Recommendations;

internal static class StorefrontRecommendations
{
    internal static async Task<IResult> GetAsync(
        HttpContext http,
        Guid key)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (DemoConnection.RequireConnection(http, true) is { } blocked)
        {
            return blocked;
        }

        DemoMarket market = DemoMarket.Current(http);

        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(market.Culture);

        using UmbracoContextReference context =
            http.RequestServices.GetRequiredService<IUmbracoContextFactory>().EnsureUmbracoContext();
        IPublishedContentCache published = context.UmbracoContext.Content;
        var access = http.RequestServices.GetRequiredService<IPublicAccessService>();
        IPublishedContent home = SiteContent.Home(http.RequestServices);

        bool Available(IPublishedContent page)
        {
            return page.Root().Key == home.Key &&
                   page.Cultures.ContainsKey(market.Culture) &&
                   !access.IsProtected(page.Path).Success;
        }

        IPublishedContent? page = published.GetById(key);

        if (page == null || page.ContentType.Alias is not ("home" or "product" or "productVariant") || !Available(page))
        {
            return Results.NotFound();
        }

        IPublishedContent product = page.ContentType.Alias == "productVariant" ? page.Parent()! : page;
        IPublishedContent? category = product.Parent();
        bool isHome = page.ContentType.Alias == "home";

        if (!Available(product) || (!isHome && category?.ContentType.Alias != "productCategory"))
        {
            return Results.NotFound();
        }

        string Id(IPublishedContent content)
        {
            return DemoConfiguration.EntityId(content, http.RequestServices);
        }

        User user = await http.RequestServices.GetRequiredService<IRelewiseUserLocator>().GetUser();
        string site = http.RequestServices.GetRequiredService<IOptions<GlobalSettings>>().Value.Id;

        var productType = product.Value<string>("productType");
        bool emptyCart = isHome && http.Request.Query["placement"] == "cart";
        bool similarEnabled = !isHome && !string.IsNullOrWhiteSpace(productType);

        ProductRecommendationRequestCollection batch = !similarEnabled
            ? new ProductRecommendationRequestCollection(
                false,
                CreateRecentlyViewed(
                    user,
                    market,
                    site,
                    isHome ? null : Id(product),
                    emptyCart ? "Empty cart: Recently viewed" : null
                )
            )
            : CreateRequests(
                user,
                market,
                site,
                Id(product),
                page == product ? null : Id(page),
                Id(category!),
                productType!
            );

        if (isHome && !emptyCart)
        {
            batch = new ProductRecommendationRequestCollection(
                false,
                CreateRecentlyViewed(user, market, site),
                CreatePopularProducts(user, market, site)
            );
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);

            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var client = http.RequestServices.GetRequiredService<IRelewiseClientFactory>()
                .GetClient<IRecommender>();
            ProductRecommendationResponseCollection response = await client.RecommendAsync(batch, timeout.Token);
            ProductRecommendationResponse[] responses = response.Responses ?? [];

            ListingCard[] Cards(
                IEnumerable<ProductResult> results,
                bool requireStock = false,
                bool requireSimilarity = false)
            {
                var cards = new List<ListingCard>();
                var seen = new HashSet<Guid>();

                foreach (ProductResult result in results)
                {
                    if (result.Data?.TryGetValue("rwUmbracoNodeKey", out DataValue? data) != true || data == null ||
                        !Guid.TryParse(data.ValueAs<string>(), out Guid node) || node == product.Key || !seen.Add(node))
                    {
                        continue;
                    }

                    IPublishedContent? candidate = published.GetById(node);

                    if (candidate?.ContentType.Alias != "product" || !Available(candidate) ||
                        !string.Equals(Id(candidate), result.ProductId, StringComparison.OrdinalIgnoreCase) ||
                        !HasPrice(candidate, market) || (requireStock && candidate.Value<int>("stock") <= 0) ||
                        (requireSimilarity && (candidate.Parent()?.Key != category!.Key ||
                                               !string.Equals(
                                                   candidate.Value<string>("productType"),
                                                   productType,
                                                   StringComparison.OrdinalIgnoreCase
                                               ))))
                    {
                        continue;
                    }

                    cards.Add(ListingCards.CardData(candidate, http));

                    if (cards.Count == VisibleCount)
                    {
                        break;
                    }
                }

                return [.. cards];
            }

            ListingCard[] similar = similarEnabled
                ? Cards(
                    responses.ElementAtOrDefault(0)?.Recommendations ?? [],
                    true,
                    true
                )
                : [];
            ListingCard[] recentlyViewed =
                Cards(responses.ElementAtOrDefault(similarEnabled ? 1 : 0)?.Recommendations ?? []);
            ListingCard[] popular = isHome && !emptyCart
                ? Cards(responses.ElementAtOrDefault(1)?.Recommendations ?? [], true)
                : [];

            if (emptyCart && recentlyViewed.Length == 0)
            {
                // Request the fallback only when no visible history remains, keeping analytics meaningful.
                ProductRecommendationResponse fallback = await client.RecommendAsync(
                    CreatePopularProducts(
                        user,
                        market,
                        site,
                        true
                    ),
                    timeout.Token
                );

                popular = Cards(fallback.Recommendations ?? [], true);
            }

            var categories = new List<CategoryRecommendationCard>();

            if (isHome && !emptyCart)
            {
                Dictionary<string, IPublishedContent> candidates = SiteContent.ShoppingCategories(home)
                    .Where(Available)
                    .ToDictionary(Id, StringComparer.OrdinalIgnoreCase);

                if (candidates.Count > 0)
                {
                    try
                    {
                        ProductCategoryRecommendationResponse categoryResponse = await client.RecommendAsync(
                            CreatePopularCategories(
                                user,
                                market,
                                candidates.Keys
                            ),
                            timeout.Token
                        );
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ProductCategoryResult? result in categoryResponse.Recommendations ?? [])
                        {
                            if (!seen.Add(result.CategoryId) ||
                                !candidates.TryGetValue(result.CategoryId, out IPublishedContent? candidate))
                            {
                                continue;
                            }

                            IPublishedContent? example = candidate.Descendants()
                                .FirstOrDefault(node => node.ContentType.Alias == "product" &&
                                                        Available(node) && node.Value<int>("stock") > 0 &&
                                                        HasPrice(node, market));

                            if (example != null)
                            {
                                categories.Add(
                                    new CategoryRecommendationCard(
                                        candidate.Key,
                                        Id(candidate),
                                        candidate.Url(market.Culture),
                                        SiteContent.Title(candidate),
                                        Storefront.Image(example)
                                    ));
                            }

                            if (categories.Count == VisibleCount)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception error) when (DemoConnection.IsConnectionFailure(error, http.RequestAborted))
                    {
                        http.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("StorefrontRecommendations")
                            .LogWarning(error, "Unable to load optional category recommendations.");
                    }
                }
            }

            return Results.Json(
                new
                {
                    similar,
                    recentlyViewed,
                    popular,
                    categories
                });
        }
        catch (Exception error) when (DemoConnection.IsConnectionFailure(error, http.RequestAborted))
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("StorefrontRecommendations")
                .LogWarning(error, "Unable to load optional Product recommendations.");

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool HasPrice(
        IPublishedContent product,
        DemoMarket market)
    {
        return PriceConverter.Read(product.Value("salesPrice"))
            ?.Values?.Any(value =>
                value.Currency.Value.Equals(market.Currency, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
