#pragma warning disable CS0618 // Verify compatibility with existing converter implementations.
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests;

public class AsyncConversionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutoMapRetainsSingleAndMultipleCultureShapes(bool asynchronous, bool text)
    {
        var property = Property("value", "Custom.Demo", culture => text ? culture + " text" : culture == "en-US" ? 10 : 20);
        var services = new ServiceCollection();
        if (asynchronous)
        {
            services.AddAsyncValueConverter<AsyncConverter>();
        }
        else
        {
            services.AddValueConverter<LegacyConverter>();
        }

        services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var converter = root.GetRequiredService<IRelewisePropertyConverter>();
        var single = await converter.ConvertAsync([property], ["en-US"]);
        Assert.Equal(text ? DataValue.DataValueTypes.String : DataValue.DataValueTypes.Double, single["value"]!.Type);
        var multiple = await converter.ConvertAsync([property], ["en-US", "da-DK"]);
        if (text)
        {
            Assert.Equal(2, multiple["value"]!.ValueAs<Multilingual>().Values!.Count);
        }
        else
        {
            Assert.False(multiple.ContainsKey("value"));
            Assert.Equal(10d, multiple["value_en-US"]!.ValueAs<double>());
            Assert.Equal(20d, multiple["value_da-DK"]!.ValueAs<double>());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalAsyncOverrideUsesScopedServicesAndIsDisposed(bool beforeDefaults)
    {
        ScopedConverter? observed = null;
        var services = new ServiceCollection();
        if (!beforeDefaults)
        {
            services.AddAsyncValueConverter<TextFieldPropertyValueConverter>();
        }

        services.AddScoped<IAsyncRelewisePropertyValueConverter>(_ => observed = new ScopedConverter());
        if (beforeDefaults)
        {
            services.AddAsyncValueConverter<TextFieldPropertyValueConverter>();
        }

        services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var result = await root.GetRequiredService<IRelewisePropertyConverter>().ConvertAsync([Property("value", "Umbraco.TextBox", _ => "original")], ["en-US"]);
        Assert.Equal("override", result["value"]!.ValueAs<string>());
        Assert.NotNull(observed);
        Assert.True(observed.Disposed);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => root.GetRequiredService<IRelewisePropertyConverter>().ConvertAsync([Property("value", "Umbraco.TextBox", _ => "original")], ["en-US"], cancellation.Token));
    }

    [Fact]
    public void DeclarativeFieldsAndCustomMapperCanBeCombinedInEitherRegistrationOrder()
    {
        foreach (bool mapperFirst in new[] { true, false })
        {
            var options = new RelewiseUmbracoOptionsBuilder();
            options.MapContent("article", mapping =>
            {
                if (mapperFirst)
                {
                    mapping.UseMapper<CustomMapper>();
                }

                mapping.Map(entity => entity.DisplayName, "heading");
                mapping.MapData("body");
                if (!mapperFirst)
                {
                    mapping.UseMapper<CustomMapper>();
                }
            });
        }
        new RelewiseUmbracoOptionsBuilder().MapContent("article", mapping => { mapping.AutoMap(); mapping.UseMapper<CustomMapper>(); });
    }

    private static IPublishedProperty Property(string alias, string editor, Func<string?, object> value)
    {
        var type = new Mock<IPublishedPropertyType>();
        type.SetupGet(property => property.EditorAlias).Returns(editor);
        type.SetupGet(property => property.Variations).Returns(global::Umbraco.Cms.Core.Models.ContentVariation.Culture);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(property => property.Alias).Returns(alias);
        property.SetupGet(property => property.PropertyType).Returns(type.Object);
        property.Setup(property => property.GetValue(It.IsAny<string?>(), It.IsAny<string?>())).Returns((string? culture, string? _) => value(culture));
        return property.Object;
    }

    public sealed class LegacyConverter : IRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context) => true;
        public void Convert(RelewisePropertyConverterContext context) => Add(context);
    }

    public sealed class AsyncConverter : IAsyncRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context) => true;
        public async ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Add(context);
        }
    }

    private static void Add(RelewisePropertyConverterContext context)
    {
        object? value = context.Property.GetValue(context.Culture);
        context.Add(context.Property.Alias, value is string text ? new DataValue(text) : new DataValue(System.Convert.ToDouble(value)));
    }

    public sealed class ScopedConverter : IAsyncRelewisePropertyValueConverter, IDisposable
    {
        public bool Disposed { get; private set; }
        public bool CanHandle(RelewisePropertyConverterContext context) => true;
        public async ValueTask ConvertAsync(RelewisePropertyConverterContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            Assert.NotNull(context.Services);
            Assert.False(Disposed);
            context.Add(context.Property.Alias, "override");
        }
        public void Dispose() => Disposed = true;
    }

    public sealed class CustomMapper : IEntityMapper<Content>
    {
        public Task MapAsync(EntityMappingContext<Content> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
