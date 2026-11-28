using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Integrations.Umbraco.DemoStorefront.Features.Cart;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Tests;

public class DemoCartPricingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ProtectedProductsAndVariantsCannotBeAddedOrRetainedInAVisibleCart(
        bool protectVariant,
        bool availableOnly)
    {
        IPublishedContent product = Published("product", "-1,1");
        IPublishedContent variant = Published("productVariant", "-1,1,2");
        var cache = new Mock<IPublishedContentCache>();
        cache.Setup(value => value.GetById(product.Key)).Returns(product);
        cache.Setup(value => value.GetById(variant.Key)).Returns(variant);

        var access = new Mock<IPublicAccessService>();
        access.Setup(value => value.IsProtected(protectVariant ? variant.Path : product.Path))
            .Returns(Attempt<PublicAccessEntry?>.Succeed(null));
        var services = new ServiceCollection();
        services.AddSingleton(access.Object);
        using ServiceProvider provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        List<CartLine> lines = [new(product.Key, protectVariant ? variant.Key : null, 1)];

        PricedCartLine[]? result = CartPricing.Price(lines, cache.Object, http, availableOnly);

        if (availableOnly)
        {
            Assert.Empty(result!);
        }
        else
        {
            Assert.Null(result);
        }
    }

    private static IPublishedContent Published(string alias, string path)
    {
        var content = new Mock<IPublishedContent>();
        content.SetupGet(value => value.Key).Returns(Guid.NewGuid());
        content.SetupGet(value => value.ContentType.Alias).Returns(alias);
        content.SetupGet(value => value.Path).Returns(path);
        content.SetupGet(value => value.Cultures)
            .Returns(
                new Dictionary<string, PublishedCultureInfo>
                {
                    ["en-US"] = new("en-US", "Example", "example", DateTime.UnixEpoch)
                });

        return content.Object;
    }
}
