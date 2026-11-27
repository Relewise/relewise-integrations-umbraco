using System.Globalization;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;

internal static class CartHttp
{
    public static void SetCulture(HttpContext http)
    {
        http.RequestServices.GetRequiredService<IVariationContextAccessor>().VariationContext =
            new VariationContext(DemoMarket.Current(http).Culture);
    }

    public static string Money(HttpContext http, decimal amount, string? currency = null)
    {
        DemoMarket market = DemoMarket.Current(http);
        var value = amount.ToString("N2", CultureInfo.GetCultureInfo(market.Culture));

        return (currency ?? market.Currency) + " " + value;
    }

    public static IResult Error(
        HttpContext http,
        string key,
        params string[] values)
    {
        if (WantsJson(http))
        {
            int status = key is "RelewiseActionFailed" or "CartFailed" ? 503 :
                key is "CartStockLimit" or "CartUnavailable" ? 409 : 400;

            return Results.Json(
                new
                {
                    error = Storefront.Text(http, key, [.. values])
                },
                statusCode: status
            );
        }

        StorefrontNavigation.Notify(http, key, true, values);

        return Results.LocalRedirect(DemoMarket.Current(http).LanguagePath + "/cart");
    }

    public static bool WantsJson(HttpContext http)
    {
        return http.Request.Headers.Accept.Any(value =>
            value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }
}
