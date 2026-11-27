using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Dashboard;

internal sealed class DashboardContentMapper : IEntityMapper<Content>
{
    public Task MapAsync(EntityMappingContext<Content> context, CancellationToken token)
    {
        throw new InvalidOperationException("Dashboard inspection must not execute entity mappers.");
    }
}
