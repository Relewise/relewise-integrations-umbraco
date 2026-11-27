#pragma warning disable CS0618 // Verify compatibility with existing converter implementations.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.Tests.Mapping.ValueConverterTestSupport;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

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
}
