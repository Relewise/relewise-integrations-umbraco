using Relewise.DemoStorefront.Features.Cart;
using Relewise.DemoStorefront.Setup;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Relewise.DemoStorefront.Site;

internal static class MarketEndpoints
{
    internal static void MapMarket(this WebApplication app)
    {
        app.MapGet("/store/market", ChangeAsync);
    }

    private static async Task<IResult> ChangeAsync(
        HttpContext http,
        string? market,
        string? returnUrl)
    {
        DemoMarket? selected = DemoMarket.All.FirstOrDefault(value => value.Id == market);

        if (selected == null)
        {
            return Results.BadRequest("Unknown market.");
        }

        http.Items[typeof(DemoMarket)] = selected;

        try
        {
            await CartOperations.ChangeMarketAsync(http);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("StorefrontCart")
                .LogError(error, "Unable to change the cart currency.");
            StorefrontNavigation.Notify(
                http,
                DemoConnection.IsConnectionFailure(error, http.RequestAborted) ? "RelewiseActionFailed" : "CartFailed",
                true
            );

            return Results.LocalRedirect(selected.ReturnUrl(returnUrl));
        }

        http.Response.Cookies.Append(
            DemoMarket.CookieName,
            selected.Id,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30)
            }
        );

        string target = selected.ReturnUrl(returnUrl);

        // Resolve translated detail-page slugs through Umbraco, keeping the same content item.
        if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(
                "http://localhost" + returnUrl,
                UriKind.Absolute,
                out Uri? uri
            ))
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();
            IPublishedContent home = SiteContent.Home(http.RequestServices);
            IPublishedContent? page = home.DescendantsOrSelf()
                .FirstOrDefault(node =>
                    node.Url("en-US") == uri.AbsolutePath || node.Url("da-DK") == uri.AbsolutePath);

            if (page != null)
            {
                target = page.Url(selected.Culture) + uri.Query;
            }
        }

        StorefrontNavigation.Notify(
            http,
            "MarketChanged",
            false,
            Storefront.Text(http, selected.LabelKey),
            selected.Currency);

        return Results.LocalRedirect(target);
    }
}
