using Microsoft.AspNetCore.Antiforgery;

namespace Relewise.DemoStorefront.Site;

internal static class FormFields
{
    internal static string Token(HttpContext http)
    {
        AntiforgeryTokenSet token = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);

        return Hidden(token.FormFieldName, token.RequestToken ?? "");
    }

    internal static string Hidden(
        string name,
        string value)
    {
        return $"<input type='hidden' name='{Storefront.E(name)}' value='{Storefront.E(value)}'>";
    }
}
