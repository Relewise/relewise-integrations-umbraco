using System.Globalization;
using System.Net;
using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Integration.Converters;
using Umbraco.Cms.Core.Dictionary;

namespace Relewise.DemoStorefront.Site;

public static class Storefront
{
    internal static string Text(HttpContext http,
        string key,
        params object[] values)
    {
        var culture = CultureInfo.GetCultureInfo(DemoMarket.Current(http).Culture);
        var text =
            http.RequestServices.GetRequiredService<ICultureDictionaryFactory>().CreateDictionary(culture)[
                "Fieldwork." + key];

        return values.Length == 0 ? text : string.Format(culture, text, values);
    }

    public static string E(string? value)
    {
        return WebUtility.HtmlEncode(value ?? "");
    }

    public static string Prices(object? value,
        HttpContext http)
    {
        DemoMarket market = DemoMarket.Current(http);
        Money? price = PriceConverter.Read(value)
            ?.Values
            ?.SingleOrDefault(price => price.Currency.Value == market.Currency);

        return price == null
            ? E(Text(http, "PriceUnavailable", market.Currency))
            : E(market.Currency + " " + price.Amount.ToString("N2", CultureInfo.GetCultureInfo(market.Culture)));
    }

    public static string Image(Umbraco.Cms.Core.Models.PublishedContent.IPublishedContent content)
    {
        var image = content.Value<string>("heroImage");

        if (string.IsNullOrWhiteSpace(image) && content.ContentType.Alias == "productVariant")
        {
            image = content.Parent()?.Value<string>("heroImage");
        }

        image = string.IsNullOrWhiteSpace(image) ? "/images/daypack.svg" : image;

        return DemoIllustrations.Render(content, image) ?? $"<img alt='' width='190' height='190' src='{E(image)}'>";
    }

    public static string Icon()
    {
        return
            "<svg viewBox='0 0 200 180' aria-hidden='true'><path d='M20 150 100 30 180 150z'/><path d='M100 30v120h60z' fill='white' opacity='.2'/><path d='m70 150 30-60 30 60' fill='#edf1e9'/></svg>";
    }

    public static string Layout(string title, string body, bool connected, HttpContext http, string section)
    {
        return StorefrontChrome.Render(title, body, http, section);
    }
}
