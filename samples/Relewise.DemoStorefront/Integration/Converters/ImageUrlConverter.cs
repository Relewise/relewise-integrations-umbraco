using Relewise.Integrations.Umbraco;
using static Relewise.DemoStorefront.DemoConstants;

namespace Relewise.DemoStorefront.Integration.Converters;

/// <summary>
/// Globally converts the demo's relative image paths into URLs usable by a separate frontend.
/// </summary>
internal sealed class ImageUrlConverter(IConfiguration configuration) : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context)
    {
        return context.Property.Alias == PropertyAliases.HeroImage;
    }

    public ValueTask ConvertAsync(
        RelewisePropertyConverterContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var path = context.Property
            .GetValue(context.Property.PropertyType.VariesByCulture() ? context.Culture : null)
            ?.ToString();
        string? url = string.IsNullOrWhiteSpace(path)
            ? null
            : new Uri(new Uri(configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!), path).AbsoluteUri;

        context.Add(context.Property.Alias, url);

        return default;
    }
}
