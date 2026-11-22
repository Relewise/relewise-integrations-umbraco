#pragma warning disable CS0618 // Verify compatibility with existing converter implementations.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests;

public class AsyncConversionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutoMapRetainsSingleAndMultipleCultureShapes(
        bool asynchronous,
        bool text)
    {
        IPublishedProperty property = Property(
            "value",
            "Custom.Demo",
            culture => text ? culture + " text" : culture == "en-US" ? 10 : 20);
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

        await using ServiceProvider root =
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var converter = root.GetRequiredService<IRelewisePropertyConverter>();
        IReadOnlyDictionary<string, DataValue?> single = await converter.ConvertAsync([property], ["en-US"]);

        Assert.Equal(text ? DataValue.DataValueTypes.String : DataValue.DataValueTypes.Double, single["value"]!.Type);

        IReadOnlyDictionary<string, DataValue?> multiple = await converter.ConvertAsync([property], ["en-US", "da-DK"]);

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
    [InlineData("en-US")]
    [InlineData("da-DK")]
    public async Task DecimalConversionDoesNotDependOnCurrentCulture(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            IPublishedProperty property = Property("price", "Umbraco.Decimal", _ => 1234.56m);
            var services = new ServiceCollection();

            services.AddAsyncValueConverter<DecimalPropertyValueConverter>();
            services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();

            await using ServiceProvider provider = services.BuildServiceProvider();
            IReadOnlyDictionary<string, DataValue?> result = await provider
                .GetRequiredService<IRelewisePropertyConverter>()
                .ConvertAsync([property], ["en-US"]);

            Assert.Equal(1234.56d, result["price"]!.ValueAs<double>());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
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

        await using ServiceProvider root =
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IReadOnlyDictionary<string, DataValue?> result = await root.GetRequiredService<IRelewisePropertyConverter>()
            .ConvertAsync([Property("value", "Umbraco.TextBox", _ => "original")], ["en-US"]);

        Assert.Equal("override", result["value"]!.ValueAs<string>());
        Assert.NotNull(observed);
        Assert.True(observed.Disposed);

        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            root.GetRequiredService<IRelewisePropertyConverter>()
                .ConvertAsync([Property("value", "Umbraco.TextBox", _ => "original")], ["en-US"], cancellation.Token));
    }

    [Fact]
    public void DeclarativeFieldsAndCustomMapperCanBeCombinedInEitherRegistrationOrder()
    {
        foreach (bool mapperFirst in new[] { true, false })
        {
            var options = new RelewiseUmbracoOptionsBuilder();

            options.MapContent(
                "article",
                mapping =>
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

        new RelewiseUmbracoOptionsBuilder().MapContent(
            "article",
            mapping =>
            {
                mapping.AutoMap();
                mapping.UseMapper<CustomMapper>();
            });
    }

    private static IPublishedProperty Property(
        string alias,
        string editor,
        Func<string?, object> value)
    {
        var type = new Mock<IPublishedPropertyType>();

        type.SetupGet(publishedProperty => publishedProperty.EditorAlias).Returns(editor);
        type.SetupGet(publishedProperty => publishedProperty.Variations)
            .Returns(ContentVariation.Culture);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(publishedProperty => publishedProperty.Alias).Returns(alias);
        property.SetupGet(publishedProperty => publishedProperty.PropertyType).Returns(type.Object);
        property.Setup(publishedProperty => publishedProperty.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => value(culture));

        return property.Object;
    }

    public sealed class LegacyConverter : IRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public void Convert(RelewisePropertyConverterContext context)
        {
            Add(context);
        }
    }

    public sealed class AsyncConverter : IAsyncRelewisePropertyValueConverter
    {
        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public async ValueTask ConvertAsync(
            RelewisePropertyConverterContext context,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Add(context);
        }
    }

    private static void Add(RelewisePropertyConverterContext context)
    {
        object? value = context.Property.GetValue(context.Culture);

        context.Add(
            context.Property.Alias,
            value is string text
                ? new DataValue(text)
                : new DataValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
    }

    public sealed class ScopedConverter : IAsyncRelewisePropertyValueConverter, IDisposable
    {
        public bool Disposed { get; private set; }

        public bool CanHandle(RelewisePropertyConverterContext context)
        {
            return true;
        }

        public async ValueTask ConvertAsync(
            RelewisePropertyConverterContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            Assert.NotNull(context.Services);
            Assert.False(Disposed);
            context.Add(context.Property.Alias, "override");
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public sealed class CustomMapper : IEntityMapper<Content>
    {
        public Task MapAsync(EntityMappingContext<Content> context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
