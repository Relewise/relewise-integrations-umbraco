using Moq;
using Relewise.Integrations.Umbraco.Indexing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;

namespace Relewise.Integrations.Umbraco.Tests.Indexing;

public class PublishedCultureResolverTests
{
    [Fact]
    public void IntersectsEveryVaryingAncestorAndReusesPublishedSnapshotsAcrossSiblings()
    {
        var parent = Guid.NewGuid();
        var root = Guid.NewGuid();
        IEnumerable<Guid> ancestors = [parent, root];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetAncestorsKeys(It.IsAny<Guid>(), out ancestors)).Returns(true);
        var cache = new Mock<IPublishedContentCache>(MockBehavior.Strict);
        cache.Setup(value => value.GetById(false, parent)).Returns(Node(parent, "en-US", "da-DK"));
        cache.Setup(value => value.GetById(false, root)).Returns(Node(root, "EN-us"));
        var resolver = new PublishedCultureResolver(navigation.Object, cache.Object);

        Assert.Equal(new[] { "en-US" }, resolver.Resolve(Node(Guid.NewGuid(), "en-US", "da-DK")));
        Assert.Equal(new[] { "en-US" }, resolver.Resolve(Node(Guid.NewGuid(), "en-US", "da-DK")));

        cache.Verify(value => value.GetById(false, parent), Times.Once);
        cache.Verify(value => value.GetById(false, root), Times.Once);
    }

    [Fact]
    public void MissingPublishedAncestorExcludesAllCulturesAndIsNotReadRepeatedly()
    {
        var parent = Guid.NewGuid();
        IEnumerable<Guid> ancestors = [parent];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetAncestorsKeys(It.IsAny<Guid>(), out ancestors)).Returns(true);
        var cache = new Mock<IPublishedContentCache>();
        var resolver = new PublishedCultureResolver(navigation.Object, cache.Object);

        Assert.Empty(resolver.Resolve(Node(Guid.NewGuid(), "en-US")));
        Assert.Empty(resolver.Resolve(Node(Guid.NewGuid(), "en-US")));
        cache.Verify(value => value.GetById(false, parent), Times.Once);
    }

    [Fact]
    public void InvariantAncestorDoesNotRestrictPublishedCultures()
    {
        var parent = Guid.NewGuid();
        IEnumerable<Guid> ancestors = [parent];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetAncestorsKeys(It.IsAny<Guid>(), out ancestors)).Returns(true);
        var cache = new Mock<IPublishedContentCache>();
        cache.Setup(value => value.GetById(false, parent)).Returns(Node(parent));

        var resolver = new PublishedCultureResolver(navigation.Object, cache.Object);
        Assert.Equal(new[] { "en-US", "da-DK" }, resolver.Resolve(Node(Guid.NewGuid(), "en-US", "da-DK")));
    }

    [Fact]
    public void RootRetainsItsCulturesAndInvariantContentRetainsItsInvariantMarker()
    {
        IEnumerable<Guid> ancestors = [];
        var navigation = new Mock<IDocumentNavigationQueryService>();
        navigation.Setup(value => value.TryGetAncestorsKeys(It.IsAny<Guid>(), out ancestors)).Returns(true);
        var resolver = new PublishedCultureResolver(navigation.Object, cache: null);

        Assert.Equal(new[] { "en-US" }, resolver.Resolve(Node(Guid.NewGuid(), "en-US")));
        Assert.Equal(new string?[] { null }, resolver.Resolve(Node(Guid.NewGuid())));
    }

    [Fact]
    public void UnknownNavigationDoesNotExportVaryingContent()
    {
        var resolver = new PublishedCultureResolver(Mock.Of<IDocumentNavigationQueryService>(), cache: null);

        Assert.Empty(resolver.Resolve(Node(Guid.NewGuid(), "en-US")));
    }

    private static IPublishedContent Node(Guid key, params string[] cultures)
    {
        var node = new Mock<IPublishedContent>();
        node.SetupGet(value => value.Key).Returns(key);
        node.SetupGet(value => value.ContentType.Variations)
            .Returns(cultures.Length == 0 ? ContentVariation.Nothing : ContentVariation.Culture);
        node.SetupGet(value => value.Cultures).Returns(cultures.ToDictionary(
            culture => culture,
            culture => new PublishedCultureInfo(culture, culture, culture, DateTime.UnixEpoch)));
        return node.Object;
    }
}
