using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Integrations.Umbraco.DemoStorefront.Site;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Tests;

public class DemoSiteContentTests
{
    [Fact]
    public void HomeUsesPublishedRootsWithoutEditingServicesOrARequestCache()
    {
        Guid draft = Guid.NewGuid(), published = Guid.NewGuid();
        IEnumerable<Guid> roots = [draft, published];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetRootKeysOfType("home", out roots)).Returns(true);

        var home = Mock.Of<IPublishedContent>();
        var cache = new Mock<IPublishedContentCache>();
        cache.Setup(value => value.GetById(published)).Returns(home);
        var context = new Mock<IUmbracoContext>();
        context.SetupGet(value => value.Content).Returns(cache.Object);
        IUmbracoContext? current = context.Object;
        var accessor = new Mock<IUmbracoContextAccessor>();
        accessor.Setup(value => value.TryGetUmbracoContext(out current)).Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(navigation.Object);
        services.AddSingleton(accessor.Object);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(home, SiteContent.Home(provider));
        cache.Verify(value => value.GetById(draft), Times.Once);
        cache.Verify(value => value.GetById(published), Times.Once);
    }
}
