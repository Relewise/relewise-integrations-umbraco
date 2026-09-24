namespace Relewise.Integrations.Umbraco.Tests;

public class RuntimeCompatibilityTests
{
    [Fact]
    public void IntegrationAssemblyLoadsAllTypesWithSupportedUmbracoVersion()
    {
        Type[] integrationTypes = typeof(RelewiseDashboardApiComposer).Assembly.GetTypes();

        Assert.NotEmpty(integrationTypes);
    }
}
