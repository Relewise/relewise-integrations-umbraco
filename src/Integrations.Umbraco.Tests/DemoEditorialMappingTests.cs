using Moq;
using Relewise.Client.DataTypes;
using Relewise.DemoStorefront.Features.Editorial;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Integration.Mapping;
using Relewise.Integrations.Umbraco.Mapping;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests;

public class DemoEditorialMappingTests
{
    [Fact]
    public void ReadingInvariantBlocksSetsTheAmbientCultureAndRestoresIt()
    {
        IVariationContextAccessor variations = Variations();
        var previous = new VariationContext("en-US");

        variations.VariationContext = previous;

        IPublishedProperty property = Property(
            "contentRows",
            _ =>
                variations.VariationContext?.Culture == "da-DK"
                    ? Blocks(Property("text", _ => "Dansk blok"))
                    : throw new InvalidOperationException("Localized blocks require an ambient culture."));

        Assert.Equal("Dansk blok", EditorialBlockText.ExtractProperty(property, "da-DK", variations));
        Assert.Same(previous, variations.VariationContext);
    }

    [Fact]
    public void AFailedBlockReadRestoresTheAmbientCulture()
    {
        IVariationContextAccessor variations = Variations();
        var previous = new VariationContext("en-US");

        variations.VariationContext = previous;

        IPublishedProperty property =
            Property("contentRows", _ => throw new InvalidOperationException("Conversion failed."));

        Assert.Throws<InvalidOperationException>(() =>
            EditorialBlockText.ExtractProperty(property, "da-DK", variations));
        Assert.Same(previous, variations.VariationContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalizedBlocksAndConvertedBodyStayInTheirOwnCultures(bool outerPropertyVaries)
    {
        BlockListModel blocks =
            Blocks(Property("text", culture => culture == "da-DK" ? "Dansk blok" : "English block", true));
        IPublishedProperty property = Property("contentRows", _ => blocks, outerPropertyVaries, "Umbraco.BlockList");
        IPublishedContent source = Source(property);
        string[] cultures = ["en-US", "da-DK"];
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);
        var converter = new RelewisePropertyConverter([]);
        PropertyConversion converted = await converter.ConvertPropertyAsync(
            source,
            source,
            property,
            cultures,
            operation,
            new EditorialBlocksConverter(Variations()));
        var entity = new Content("article")
        {
            DisplayName = new Multilingual(new Multilingual.Value("en-US", "Title")),
            Data = new Dictionary<string, DataValue?>
            {
                ["bodyText"] = new Multilingual(
                    new Multilingual.Value("en-US", "English body"),
                    new Multilingual.Value("da-DK", "Dansk brødtekst")),
                ["unrelated"] = "Keep me"
            }
        };

        foreach (KeyValuePair<string, DataValue?> field in converted.Data)
        {
            entity.Data.Add(field.Key, field.Value);
        }

        Multilingual displayName = entity.DisplayName;

        await new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(source, entity, cultures, operation),
            default);

        List<Multilingual.Value> values = entity.Data["searchText"]!.ValueAs<Multilingual>().Values!;

        Assert.Equal(
            "English body\nEnglish block",
            values.Single(value => string.Equals(value.Language.Value, "en-US", StringComparison.OrdinalIgnoreCase))
                .Text);
        Assert.Equal(
            "Dansk brødtekst\nDansk blok",
            values.Single(value => string.Equals(value.Language.Value, "da-DK", StringComparison.OrdinalIgnoreCase))
                .Text);
        Assert.Equal("article", entity.Id);
        Assert.Same(displayName, entity.DisplayName);
        Assert.Equal("Keep me", entity.Data["unrelated"]!.ValueAs<string>());
    }

    [Fact]
    public async Task APartialCultureExportDoesNotReadOrEmitAnotherCulture()
    {
        IPublishedProperty text = Property(
            "text",
            culture => culture == "da-DK" ? "Dansk blok" : throw new InvalidOperationException("Unrequested culture"),
            true);
        IPublishedProperty property = Property("contentRows", _ => Blocks(text));
        IPublishedContent source = Source(property);
        var entity = new Content("article")
        { Data = new Dictionary<string, DataValue?> { ["bodyText"] = "Dansk brødtekst" } };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(source, entity, ["da-DK"], operation),
            default);

        Assert.Equal("Dansk brødtekst\nDansk blok", entity.Data["searchText"]!.ValueAs<string>());
    }

    [Fact]
    public async Task RemovingAllEditorialTextOverwritesTheEarlierSearchText()
    {
        IPublishedContent source = Source(Property("contentRows", _ => new BlockListModel([])));
        var entity = new Content("article")
        {
            Data = new Dictionary<string, DataValue?>
            { ["bodyText"] = "", ["contentRows"] = "", ["searchText"] = "Old text" }
        };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(source, entity, ["en-US", "da-DK"], operation),
            default);

        Assert.All(entity.Data["searchText"]!.ValueAs<Multilingual>().Values!, value => Assert.Equal("", value.Text));
    }

    [Fact]
    public void NestedBlocksExcludeHiddenContentAndStripNonEditorialHtml()
    {
        BlockListModel nested = Blocks(
            Property(
                "text",
                _ => new HtmlEncodedString("<p>Nested text</p><script>secret()</script>")));
        IPublishedElement outer = Element(Property("heading", _ => "Heading"), Property("items", _ => nested));
        IPublishedElement hidden = Element(Property("hide", _ => true));
        var blocks = new BlockListModel(
        [
            new BlockListItem(Guid.NewGuid(), outer, null, null),
            new BlockListItem(Guid.NewGuid(), Element(Property("text", _ => "Hidden text")), Guid.NewGuid(), hidden)
        ]);

        Assert.Equal("Heading\nNested text", EditorialBlockText.Extract(blocks, "en-US"));
    }

    [Fact]
    public async Task GridAreasAndOptionalGridFieldKeepCultureAwareExtraction()
    {
        var child = new BlockGridItem(Guid.NewGuid(), Element(Property("text", _ => "Area child")), null, null);
        var parent = new BlockGridItem(Guid.NewGuid(), Element(Property("heading", _ => "Grid heading")), null, null)
        {
            Areas = [new BlockGridArea([child], "main", 12, 1)]
        };
        IPublishedContent source = Source(Property("pageGrid", _ => new BlockGridModel([parent], 12)));
        var entity = new Content("article") { Data = new Dictionary<string, DataValue?> { ["bodyText"] = "Body" } };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(source, entity, ["en-US"], operation),
            default);

        Assert.Equal("Body\nGrid heading\nArea child", entity.Data["searchText"]!.ValueAs<string>());
    }

    [Fact]
    public async Task ConverterAndMapperRespectCancellation()
    {
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        IPublishedProperty property = Property("contentRows", _ => null, editor: "Umbraco.BlockList");
        var context = new RelewisePropertyConverterContext(property, "en-US", new Dictionary<string, DataValue?>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new EditorialBlocksConverter(Variations()).ConvertAsync(context, cancellation.Token));

        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(Source(property), new Content("article"), ["en-US"], operation),
            cancellation.Token));
    }

    [Fact]
    public void StorefrontRenderingKeepsRichTextAndHidesWholeContainers()
    {
        IPublishedElement visible = Element(
            Property("heading", _ => "A <quiet> place"),
            Property("text", _ => new HtmlEncodedString("<p>Take a <strong>break</strong>.</p>")));
        IPublishedElement hidden = Element(Property("hide", _ => true));
        var child = new BlockGridItem(
            Guid.NewGuid(),
            Element(Property("text", _ => new HtmlEncodedString("Secret child"))),
            null,
            null);
        var container = new BlockGridItem(Guid.NewGuid(), visible, Guid.NewGuid(), hidden)
        {
            Areas = [new BlockGridArea([child], "main", 12, 1)]
        };
        var blocks = new BlockListModel([new BlockListItem(Guid.NewGuid(), visible, null, null)]);

        EditorialSection section = Assert.Single(StorefrontEditorial.Sections(blocks, "en-US"));

        Assert.Equal("A <quiet> place", section.Heading);
        Assert.Equal("<p>Take a <strong>break</strong>.</p>", section.Text!.ToHtmlString());
        Assert.Empty(StorefrontEditorial.Sections(new BlockGridModel([container], 12), "en-US"));
    }

    [Theory]
    [InlineData("en-US", "English story")]
    [InlineData("da-DK", "Dansk fortælling")]
    public void StorefrontRenderingUsesTheRequestedBlockLanguage(string culture, string expected)
    {
        BlockListModel blocks = Blocks(
            Property(
                "text",
                language => new HtmlEncodedString(
                    language == "da-DK" ? "<p>Dansk fortælling</p>" : "<p>English story</p>"),
                true));

        Assert.Equal(
            "<p>" + expected + "</p>",
            Assert.Single(StorefrontEditorial.Sections(blocks, culture)).Text!.ToHtmlString());
    }

    private static IVariationContextAccessor Variations()
    {
        var accessor = new Mock<IVariationContextAccessor>();

        accessor.SetupProperty(value => value.VariationContext);

        return accessor.Object;
    }

    private static BlockListModel Blocks(params IPublishedProperty[] properties)
    {
        return new BlockListModel([new BlockListItem(Guid.NewGuid(), Element(properties), null, null)]);
    }

    private static IPublishedElement Element(params IPublishedProperty[] properties)
    {
        var element = new Mock<IPublishedElement>();

        element.Setup(value => value.GetProperty(It.IsAny<string>()))
            .Returns((string alias) =>
                properties.FirstOrDefault(publishedProperty => publishedProperty.Alias == alias));

        return element.Object;
    }

    private static IPublishedContent Source(params IPublishedProperty[] properties)
    {
        var source = new Mock<IPublishedContent>();

        source.Setup(value => value.GetProperty(It.IsAny<string>()))
            .Returns((string alias) =>
                properties.FirstOrDefault(publishedProperty => publishedProperty.Alias == alias));

        return source.Object;
    }

    private static IPublishedProperty Property(
        string alias,
        Func<string?, object?> value,
        bool varies = false,
        string editor = "Umbraco.TextBox")
    {
        var type = new Mock<IPublishedPropertyType>();

        type.SetupGet(publishedProperty => publishedProperty.EditorAlias).Returns(editor);
        type.SetupGet(publishedProperty => publishedProperty.Variations)
            .Returns(varies ? ContentVariation.Culture : ContentVariation.Nothing);

        var property = new Mock<IPublishedProperty>();

        property.SetupGet(publishedProperty => publishedProperty.Alias).Returns(alias);
        property.SetupGet(publishedProperty => publishedProperty.PropertyType).Returns(type.Object);
        property.Setup(publishedProperty => publishedProperty.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => value(culture));

        return property.Object;
    }
}
