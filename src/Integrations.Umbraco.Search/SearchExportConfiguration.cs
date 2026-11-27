using System;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class SearchExportConfiguration : IExportSystemConfiguration
{
    public SearchProviderOptions Options { get; } = new();

    public IExportSystem Resolve(IServiceProvider services)
    {
        return services.GetService<UmbracoSearchExportSystem>() ?? throw new InvalidOperationException(
            "Relewise Search services are not registered. Keep AddComposers() in the Umbraco bootstrap chain.");
    }

    public void Validate(MappingCollection mappings) => Options.Validate(mappings);
}
