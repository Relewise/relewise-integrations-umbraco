using Relewise.Integrations.Umbraco;

/// <summary>Globally converts the demo's relative image paths into URLs usable by a separate frontend.</summary>
internal sealed class ImageUrlConverter(IConfiguration configuration) : IAsyncRelewisePropertyValueConverter
{
    public bool CanHandle(RelewisePropertyConverterContext context) => context.Property.Alias == "heroImage";

    public ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? path = context.Property.GetValue(context.Property.PropertyType.VariesByCulture() ? context.Culture : null)?.ToString();
        string? url = string.IsNullOrWhiteSpace(path) ? null : new Uri(new Uri(configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!), path).AbsoluteUri;
        context.Add(context.Property.Alias, url);
        return default;
    }
}
