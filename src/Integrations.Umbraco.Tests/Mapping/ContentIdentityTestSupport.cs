using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

internal static class ContentIdentityTestSupport
{
    internal static readonly Guid SharedKey = Guid.Parse("1a234567-89ab-4cde-8f01-23456789abcd");

    internal static RelewiseUmbracoConfiguration Configuration(string? value)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Relewise:Umbraco:UseContentKeysAsIds"] = value })
                .Build());
        services.AddSingleton<RelewiseUmbracoConfiguration.Configure>((options, _) =>
            options.AddContentType("article", type => type.AutoMap()));

        using ServiceProvider provider = services.BuildServiceProvider();

        return new RelewiseUmbracoConfiguration(provider);
    }

    internal static IPublishedContent PublishedContent(int id, Guid key)
    {
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.ContentType.Alias).Returns("article");

        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Key).Returns(key);

        return content.Object;
    }

    internal static IContent EditableContent(int id, Guid key, string alias = "article")
    {
        var contentType = new Mock<ISimpleContentType>();

        contentType.SetupGet(x => x.Alias).Returns(alias);

        var content = new Mock<IContent>();

        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);

        return content.Object;
    }
}
