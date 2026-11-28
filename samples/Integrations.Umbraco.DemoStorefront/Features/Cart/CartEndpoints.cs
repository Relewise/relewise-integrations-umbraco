using Microsoft.AspNetCore.Antiforgery;
using Relewise.Integrations.Umbraco.DemoStorefront.Setup;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using static Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart.CartHttp;
using static Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart.CartModelBuilder;
using static Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart.CartPricing;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

internal static class CartEndpoints
{
    public static void MapCart(this WebApplication app)
    {
        app.MapGet(
            "/{language:regex(^(en|da)$)}/cart/state",
            async Task<IResult> (HttpContext http) => await GetStateAsync(http));
        app.MapGet(
            "/{language:regex(^(en|da)$)}/cart",
            async Task<IResult> (HttpContext http) => await CartPages.Page(http, false));
        app.MapGet(
            "/{language:regex(^(en|da)$)}/checkout",
            async Task<IResult> (HttpContext http) => await CartPages.Page(http, true));
        app.MapGet(
            "/{language:regex(^(en|da)$)}/confirmation",
            (HttpContext http) => CartPages.Confirmation(http));
        app.MapPost(
            "/{language:regex(^(en|da)$)}/cart/{action:regex(^(add|update|clear)$)}",
            (HttpContext http, string action) => MutateAsync(http, action));
        app.MapPost(
            "/{language:regex(^(en|da)$)}/checkout",
            async Task<IResult> (HttpContext http) => await MutateAsync(http, "checkout"));
    }

    private static async Task<IResult> GetStateAsync(HttpContext http)
    {
        if (DemoConnection.RequireConnection(http, true) is { } blocked)
        {
            return blocked;
        }

        http.Response.Headers.CacheControl = "no-store";
        await http.Session.LoadAsync(http.RequestAborted);

        CartState basket = CartStateStore.Get(http);

        await basket.Gate.WaitAsync(http.RequestAborted);

        try
        {
            using UmbracoContextReference context = http.RequestServices.GetRequiredService<IUmbracoContextFactory>()
                .EnsureUmbracoContext();

            SetCulture(http);

            List<CartLine> lines = [.. basket.Lines];
            PricedCartLine[] priced = Price(lines, context.UmbracoContext.Content, http, true)!;

            return Results.Json(Model(http, lines, priced));
        }
        finally
        {
            basket.Gate.Release();
        }
    }

    private static async Task<IResult> MutateAsync(HttpContext http, string action)
    {
        if (DemoConnection.RequireConnection(http, WantsJson(http)) is { } blocked)
        {
            return blocked;
        }

        try
        {
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return Error(http, "CartInvalid");
        }

        IFormCollection form = await http.Request.ReadFormAsync(http.RequestAborted);

        return await CartOperations.ExecuteAsync(http, action, form);
    }
}
