#pragma warning disable CS0618 // Verify compatibility with existing converter implementations.

using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Services;
using static Relewise.Integrations.Umbraco.Tests.Mapping.ValueConverterTestSupport;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class ValueConverterRegistrationTests
{
    [Fact]
    public async Task RegistrationPreservesLegacySingletonsAndScopesAsyncConvertersWithoutDuplicates()
    {
        var services = new ServiceCollection();

        services.AddValueConverter<LegacyConverter>();
        services.AddValueConverter<LegacyConverter>();
        services.AddAsyncValueConverter<ScopedConverter>();
        services.AddAsyncValueConverter<ScopedConverter>();

        Assert.Equal(2, services.Count);
        Assert.Equal(ServiceLifetime.Singleton, services[0].Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, services[1].Lifetime);

        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        ScopedConverter first;

        await using (AsyncServiceScope scope = root.CreateAsyncScope())
        {
            first = Assert.IsType<ScopedConverter>(
                scope.ServiceProvider.GetRequiredService<IAsyncRelewisePropertyValueConverter>());

            Assert.Same(first, scope.ServiceProvider.GetRequiredService<IAsyncRelewisePropertyValueConverter>());
            Assert.Same(
                root.GetRequiredService<IRelewisePropertyValueConverter>(),
                scope.ServiceProvider.GetRequiredService<IRelewisePropertyValueConverter>());
            Assert.False(first.Disposed);
        }

        Assert.True(first.Disposed);

        await using AsyncServiceScope next = root.CreateAsyncScope();

        Assert.NotSame(first, next.ServiceProvider.GetRequiredService<IAsyncRelewisePropertyValueConverter>());
    }

    [Fact]
    public async Task ConverterImplementingBothContractsRunsOnlyItsAsyncMethod()
    {
        var services = new ServiceCollection();

        services.AddAsyncValueConverter<DualConverter>();
        services.AddSingleton<IRelewisePropertyConverter, RelewisePropertyConverter>();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IRelewisePropertyValueConverter));

        await using ServiceProvider root = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        IReadOnlyDictionary<string, DataValue?> result = await root.GetRequiredService<IRelewisePropertyConverter>()
            .ConvertAsync([Property("value", "Custom.Demo", _ => "original")], ["en-US"]);

        Assert.Equal("async", result["value"]!.ValueAs<string>());
    }

    [Fact]
    public void RegistrationsRejectMissingServices()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddAsyncValueConverter<AsyncConverter>(null!));
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddValueConverter<LegacyConverter>(null!));
    }
}
