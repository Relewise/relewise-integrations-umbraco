using System;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Indexing;

internal interface IExportSystemConfiguration
{
    IExportSystem Resolve(IServiceProvider services);
    void Validate(MappingCollection mappings);
}
