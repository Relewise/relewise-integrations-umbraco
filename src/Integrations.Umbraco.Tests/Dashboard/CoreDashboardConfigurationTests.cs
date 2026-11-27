using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Controllers;
using Relewise.Integrations.Umbraco.Controllers.Messages;

namespace Relewise.Integrations.Umbraco.Tests.Dashboard;

public class CoreDashboardConfigurationTests
{
    [Fact]
    public void CoreDescribesMappingsWithoutRequiringSearchServices()
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
        {
            options.UseContentKeysAsIds = true;
            options.MapContent("article", mapping => mapping.MapData("body"));
            options.MapProduct("product", mapping => mapping.IdFromProperty("sku"));
        });
        registrations.AddSingleton<RelewiseUmbracoConfiguration>();
        using ServiceProvider services = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        ConfigurationViewModel snapshot = DashboardConfigurationSnapshot.Create(
            services.GetRequiredService<RelewiseUmbracoConfiguration>(),
            [],
            middlewareEnabled: true);

        Assert.False(snapshot.SearchConfigured);
        Assert.Empty(snapshot.SearchProviders);
        Assert.Null(snapshot.DefaultPublishedContentProviderAlias);
        Assert.True(snapshot.ContentMiddlewareEnabled);
        DashboardMappingViewModel article = Assert.Single(
            snapshot.Mappings,
            mapping => mapping.DocumentTypeAlias == "article");
        Assert.Equal("Node key", article.IdSource);
        DashboardMappingViewModel product = Assert.Single(
            snapshot.Mappings,
            mapping => mapping.DocumentTypeAlias == "product");
        Assert.Equal("Property: sku", product.IdSource);
    }
}
