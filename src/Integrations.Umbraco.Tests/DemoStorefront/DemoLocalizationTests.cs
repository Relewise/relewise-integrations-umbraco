using Moq;
using Relewise.DemoStorefront;
using Relewise.DemoStorefront.Seeding;
using Umbraco.Cms.Core.Models;

namespace Relewise.Integrations.Umbraco.Tests.DemoStorefront;

public class DemoLocalizationTests
{
    [Fact]
    public void AdditionalLocalizedArticlesDoNotChangeWhichSeedIsSelected()
    {
        IContent authored = Article(Guid.NewGuid(), "Another story");
        IContent seed = Article(DemoConstants.ContentKeys.LocalizedArticle, "Renamed seed");
        IContent duplicateTitle = Article(Guid.NewGuid(), "Across the seasons");

        Assert.Same(seed, DemoLocalization.FindSeededArticle([authored, duplicateTitle, seed]));
    }

    [Fact]
    public void OriginalSeedIsRecognizedOnExistingInstallations()
    {
        IContent seed = Article(Guid.NewGuid(), "Across the seasons");

        Assert.Same(seed, DemoLocalization.FindSeededArticle([Article(Guid.NewGuid(), "Another story"), seed]));
    }

    [Fact]
    public void MissingSeedDoesNotSelectAnAuthoredArticleOrThrow()
    {
        Assert.Null(DemoLocalization.FindSeededArticle([Article(Guid.NewGuid(), "My own story")]));
        Assert.Null(DemoLocalization.FindSeededArticle([]));
    }

    private static IContent Article(Guid key, string englishName)
    {
        var article = new Mock<IContent>();
        article.SetupGet(value => value.Key).Returns(key);
        article.SetupGet(value => value.ContentType.Alias).Returns("localizedArticle");
        article.Setup(value => value.GetCultureName("en-US")).Returns(englishName);
        return article.Object;
    }
}
