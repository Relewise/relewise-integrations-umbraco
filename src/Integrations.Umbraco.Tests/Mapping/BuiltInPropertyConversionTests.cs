using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class BuiltInPropertyConversionTests
{
    [Fact]
    public async Task InvariantNameIsSharedAcrossTheExportLanguages()
    {
        var property = new Mock<IPublishedProperty>();

        property.SetupGet(value => value.PropertyType).Returns(Mock.Of<IPublishedPropertyType>());
        property.Setup(value => value.GetValue(null, null)).Returns("Trail flask");

        var source = Mock.Of<IPublishedContent>();
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var operation = new MappingOperation(services, default);
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US", "da-DK"],
            nameof(Product.DisplayName),
            operation);

        var result = await BuiltInPropertyConversion.ConvertAsync<Multilingual>(context);

        Assert.Collection(
            result.Values!,
            value =>
            {
                Assert.Equal("en-us", value.Language.Value);
                Assert.Equal("Trail flask", value.Text);
            },
            value =>
            {
                Assert.Equal("da-dk", value.Language.Value);
                Assert.Equal("Trail flask", value.Text);
            });
    }

    [Fact]
    public async Task LocalizedNameDoesNotExportAnUnpublishedTranslation()
    {
        var propertyType = new Mock<IPublishedPropertyType>();

        propertyType.SetupGet(value => value.Variations)
            .Returns(ContentVariation.Culture);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(value => value.PropertyType).Returns(propertyType.Object);
        property.Setup(value => value.GetValue("en-US", null)).Returns("Across the seasons");
        property.Setup(value => value.GetValue("da-DK", null)).Returns("Gennem årstiderne");

        var source = Mock.Of<IPublishedContent>();
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var operation = new MappingOperation(services, default);
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US"],
            nameof(Content.DisplayName),
            operation);

        var result = await BuiltInPropertyConversion.ConvertAsync<Multilingual>(context);

        Multilingual.Value name = Assert.Single(result.Values!);

        Assert.Equal("en-us", name.Language.Value);
        Assert.Equal("Across the seasons", name.Text);
    }

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("DA-dk", "da-DK")]
    [InlineData("fr-FR", "da-DK")]
    public async Task LocalizedBrandNameUsesTheDefaultPublishedLanguageDeterministically(
        string defaultCulture,
        string expected)
    {
        var languages = new Mock<ILanguageService>();

        languages.Setup(value => value.GetDefaultIsoCodeAsync()).ReturnsAsync(defaultCulture);

        var services = new ServiceCollection();

        services.AddSingleton(languages.Object);

        await using ServiceProvider root = services.BuildServiceProvider();
        await using var operation = new MappingOperation(root, default);
        var type = new Mock<IPublishedPropertyType>();

        type.SetupGet(value => value.Variations).Returns(ContentVariation.Culture);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(value => value.PropertyType).Returns(type.Object);
        property.Setup(value => value.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => "Name: " + culture);

        var source = Mock.Of<IPublishedContent>();
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US", "da-DK"],
            nameof(Brand.DisplayName),
            operation);

        Assert.Equal("Name: " + expected, await BuiltInPropertyConversion.ConvertAsync<string>(context));
        Assert.Equal("Name: " + expected, await BuiltInPropertyConversion.ConvertAsync<string>(context));
        languages.Verify(value => value.GetDefaultIsoCodeAsync(), Times.Once);
    }

    [Fact]
    public async Task CancellingDuringLanguageResolutionDoesNotReadTheSourceValue()
    {
        var language = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var languages = new Mock<ILanguageService>();

        languages.Setup(value => value.GetDefaultIsoCodeAsync()).Returns(language.Task);

        var services = new ServiceCollection();

        services.AddSingleton(languages.Object);

        await using ServiceProvider root = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        await using var operation = new MappingOperation(root, cancellation.Token);
        var type = new Mock<IPublishedPropertyType>();

        type.SetupGet(value => value.Variations).Returns(ContentVariation.Culture);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(value => value.PropertyType).Returns(type.Object);

        var source = Mock.Of<IPublishedContent>();
        var context = new PropertyMappingContext(
            source,
            source,
            property.Object,
            ["en-US"],
            nameof(Brand.DisplayName),
            operation);

        Task<string> conversion = BuiltInPropertyConversion.ConvertAsync<string>(context).AsTask();

        cancellation.Cancel();
        language.SetResult("en-US");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion);
        property.Verify(value => value.GetValue(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }
}
