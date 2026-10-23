using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Strings;

namespace Relewise.Integrations.Umbraco.Tests;

public class RichTextEditorPropertyValueConverterTests
{
    [Theory]
    [InlineData("Umbraco.RichText", "<p>A longer <strong>description</strong> &amp; details.</p>")]
    [InlineData("Umbraco.TinyMCE", "<p>Legacy description</p>")]
    [InlineData("Umbraco.RichText", "")]
    [InlineData("Umbraco.TinyMCE", "")]
    [InlineData("Umbraco.RichText", null)]
    [InlineData("Umbraco.TinyMCE", null)]
    public void DefaultRegistrationExportsPublishedHtml(string editorAlias, string? html)
    {
        using ServiceProvider services = CreateServices();
        var property = new PublishedProperty(editorAlias, html is null ? null : new HtmlEncodedString(html));

        var result = services.GetRequiredService<IRelewisePropertyConverter>().Convert([property], ["en-US"]);

        Assert.Equal(html, result["description"]?.Value);
        Assert.Null(property.RequestedCulture);
    }

    [Theory]
    [InlineData("Umbraco.RichText")]
    [InlineData("Umbraco.TinyMCE")]
    public void UsesHtmlInterfaceAndRequestedCulture(string editorAlias)
    {
        using ServiceProvider services = CreateServices();
        var property = new PublishedProperty(editorAlias, new PublishedHtml(), ContentVariation.Culture);

        var result = services.GetRequiredService<IRelewisePropertyConverter>().Convert([property], ["da-DK"]);

        Assert.Equal("<p>Dansk beskrivelse</p>", result["description"]?.Value);
        Assert.Equal("da-DK", property.RequestedCulture);
    }

    [Fact]
    public void DoesNotHandleUnrelatedEditors()
    {
        using ServiceProvider services = CreateServices();
        var property = new PublishedProperty("Custom.Editor", new HtmlEncodedString("<p>Other content</p>"));

        var result = services.GetRequiredService<IRelewisePropertyConverter>().Convert([property], ["en-US"]);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitRichTextRegistrationDoesNotDuplicateDefaultConverter(bool registerBeforeDefaults)
    {
        using ServiceProvider services = CreateServices(registerExplicitly: true, registerBeforeDefaults);

        Assert.Single(services.GetServices<IRelewisePropertyValueConverter>().OfType<RichTextEditorPropertyValueConverter>());

        var property = new PublishedProperty("Umbraco.RichText", new HtmlEncodedString("<p>Description</p>"));
        var result = services.GetRequiredService<IRelewisePropertyConverter>().Convert([property], ["en-US"]);
        Assert.Equal("<p>Description</p>", result["description"]?.Value);
    }

    private static ServiceProvider CreateServices(bool registerExplicitly = false, bool registerBeforeDefaults = true)
    {
        var services = new ServiceCollection();
        if (registerExplicitly && registerBeforeDefaults)
            services.AddValueConverter<RichTextEditorPropertyValueConverter>();

        var builder = new TestUmbracoBuilder(services);
        builder.AddRelewise();
        if (registerExplicitly && !registerBeforeDefaults)
            services.AddValueConverter<RichTextEditorPropertyValueConverter>();

        return services.BuildServiceProvider();
    }

    private sealed class TestUmbracoBuilder(IServiceCollection services) : IUmbracoBuilder
    {
        private readonly Dictionary<Type, ICollectionBuilder> _collections = new();
        public IServiceCollection Services => services;
        public IConfiguration Config => throw new NotSupportedException();
        public TypeLoader TypeLoader => throw new NotSupportedException();
        public ILoggerFactory BuilderLoggerFactory => throw new NotSupportedException();
        public IProfiler Profiler => throw new NotSupportedException();
        public AppCaches AppCaches => throw new NotSupportedException();

        public TBuilder WithCollectionBuilder<TBuilder>() where TBuilder : ICollectionBuilder
        {
            if (!_collections.TryGetValue(typeof(TBuilder), out var builder))
                _collections.Add(typeof(TBuilder), builder = Activator.CreateInstance<TBuilder>());

            return (TBuilder)builder;
        }

        public void Build() => throw new NotSupportedException();
    }

    private sealed class PublishedHtml : IHtmlEncodedString
    {
        public string ToHtmlString() => "<p>Dansk beskrivelse</p>";
    }

    private sealed class PublishedProperty(string editorAlias, IHtmlEncodedString? value,
        ContentVariation variations = ContentVariation.Nothing) : IPublishedProperty
    {
        public IPublishedPropertyType PropertyType { get; } = new PublishedPropertyType(editorAlias, variations);
        public string Alias => "description";
        public string? RequestedCulture { get; private set; }
        public bool HasValue(string? culture = null, string? segment = null) => value is not null;
        public object? GetValue(string? culture = null, string? segment = null)
        {
            RequestedCulture = culture;
            return value;
        }
        public object? GetSourceValue(string? culture = null, string? segment = null) => throw new NotSupportedException();
        public object? GetDeliveryApiValue(bool expanding, string? culture = null, string? segment = null) => throw new NotSupportedException();
    }

    private sealed class PublishedPropertyType(string editorAlias, ContentVariation variations) : IPublishedPropertyType
    {
        public IPublishedContentType? ContentType => null;
        public PublishedDataType DataType => throw new NotSupportedException();
        public string Alias => "description";
        public string EditorAlias => editorAlias;
        public string EditorUiAlias => "Umb.PropertyEditorUi.Tiptap";
        public bool IsUserProperty => true;
        public ContentVariation Variations => variations;
        public PropertyCacheLevel CacheLevel => PropertyCacheLevel.Element;
        public PropertyCacheLevel DeliveryApiCacheLevel => PropertyCacheLevel.Element;
        public Type ModelClrType => typeof(IHtmlEncodedString);
        public Type ClrType => ModelClrType;
        public bool? IsValue(object? value, PropertyValueLevel level) => value is not null;
        public object? ConvertSourceToInter(IPublishedElement owner, object? source, bool preview) => throw new NotSupportedException();
        public object? ConvertInterToObject(IPublishedElement owner, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview) => throw new NotSupportedException();
        public object? ConvertInterToDeliveryApiObject(IPublishedElement owner, PropertyCacheLevel referenceCacheLevel, object? inter, bool preview, bool expanding) => throw new NotSupportedException();
    }
}
