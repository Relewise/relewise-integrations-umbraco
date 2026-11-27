using System;
using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Indexing;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Search;

internal sealed class SearchExportConfiguration : IExportSystemConfiguration
{
    public SearchProviderOptions Options { get; } = new();

    public IExportSystem Resolve(IServiceProvider services) => services.GetRequiredService<UmbracoSearchExportSystem>();

    public void Validate(MappingCollection mappings) => Options.Validate(mappings);
}
