namespace Relewise.Integrations.Umbraco.Tests.Compatibility;

public class RuntimeCompatibilityTests
{
    [Fact]
    public void IntegrationAssemblyLoadsAllTypesWithSupportedUmbracoVersion()
    {
        Type[] integrationTypes = typeof(RelewiseDashboardApiComposer).Assembly.GetTypes();

        Assert.NotEmpty(integrationTypes);
    }
}
