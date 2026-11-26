using Microsoft.Extensions.DependencyInjection;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

internal static class MappingTestServices
{
    internal static ServiceProvider Create(Action<RelewiseUmbracoOptionsBuilder> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) => configure(options));
        services.AddSingleton<RelewiseUmbracoConfiguration>();

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });

        _ = provider.GetRequiredService<RelewiseUmbracoConfiguration>();

        return provider;
    }
}
