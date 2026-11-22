using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using static Relewise.Integrations.Umbraco.Tests.Mapping.ContentIdentityTestSupport;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class ContentIdentityTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void EnvironmentConfigurationSelectsIdentityForPublishedAndUnpublishedContent(string? setting, bool useKeys)
    {
        RelewiseUmbracoConfiguration configuration = Configuration(setting);
        IPublishedContent published = PublishedContent(1234, SharedKey);
        IContent editable = EditableContent(1234, SharedKey);
        string expected = useKeys ? SharedKey.ToString("D") : "1234";

        Assert.Equal(useKeys, configuration.UseContentKeysAsIds);
        Assert.Equal(expected, configuration.GetContentId(published));
        Assert.Equal(expected, configuration.GetContentId(editable));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegistrationCanOverrideEnvironmentConfiguration(bool useKeys)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?>
                    { ["Relewise:Umbraco:UseContentKeysAsIds"] = (!useKeys).ToString() })
                .Build());
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
            options.UseContentKeysAsIds = useKeys);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(useKeys, new RelewiseUmbracoConfiguration(provider).UseContentKeysAsIds);
    }

    [Fact]
    public void SharedKeyMatchesAcrossDifferentLocalIdsOnlyWhenOptedIn()
    {
        IPublishedContent first = PublishedContent(1234, SharedKey);
        IPublishedContent second = PublishedContent(9876, SharedKey);
        RelewiseUmbracoConfiguration keys = Configuration("true");
        RelewiseUmbracoConfiguration numeric = Configuration(null);

        Assert.Equal(keys.GetContentId(first), keys.GetContentId(second));
        Assert.NotEqual(numeric.GetContentId(first), numeric.GetContentId(second));
        Assert.NotEqual(keys.GetContentId(first), keys.GetContentId(PublishedContent(1234, Guid.NewGuid())));
    }

    [Fact]
    public void CanonicalIdOverrideIsSharedAndEmptyValuesFail()
    {
        using ServiceProvider services = MappingTestServices.Create(
            options => options.MapBrand("brand", mapping => mapping.IdFromProperty("brandId")));
        var config = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        var property = new Mock<IPublishedProperty>();

        property.SetupGet(p => p.PropertyType).Returns(Mock.Of<IPublishedPropertyType>());
        property.Setup(p => p.GetValue(null, null)).Returns("erp-brand");

        var node = new Mock<IPublishedContent>();

        node.SetupGet(p => p.ContentType.Alias).Returns("brand");
        node.Setup(p => p.GetProperty("brandId")).Returns(property.Object);
        Assert.Equal("erp-brand", config.GetContentId(node.Object));
        property.Setup(p => p.GetValue(null, null)).Returns(" ");
        Assert.Throws<InvalidOperationException>(() => config.GetContentId(node.Object));
    }
}
