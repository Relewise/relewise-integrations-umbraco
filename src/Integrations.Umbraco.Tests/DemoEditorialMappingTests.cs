using Moq;
using Relewise.Client.DataTypes;
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
        var variations = Variations();
        var previous = new VariationContext("en-US");
        variations.VariationContext = previous;
        var property = Property("contentRows", _ =>
            variations.VariationContext?.Culture == "da-DK"
                ? Blocks(Property("text", _ => "Dansk blok"))
                : throw new InvalidOperationException("Localized blocks require an ambient culture."));

        Assert.Equal("Dansk blok", EditorialBlockText.ExtractProperty(property, "da-DK", variations));
        Assert.Same(previous, variations.VariationContext);
    }

    [Fact]
    public void AFailedBlockReadRestoresTheAmbientCulture()
    {
        var variations = Variations();
        var previous = new VariationContext("en-US");
        variations.VariationContext = previous;
        var property = Property("contentRows", _ => throw new InvalidOperationException("Conversion failed."));

        Assert.Throws<InvalidOperationException>(() => EditorialBlockText.ExtractProperty(property, "da-DK", variations));
        Assert.Same(previous, variations.VariationContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalizedBlocksAndConvertedBodyStayInTheirOwnCultures(bool outerPropertyVaries)
    {
        var blocks = Blocks(Property("text", culture => culture == "da-DK" ? "Dansk blok" : "English block", true));
        var property = Property("contentRows", _ => blocks, outerPropertyVaries, "Umbraco.BlockList");
        var source = Source(property);
        string[] cultures = ["en-US", "da-DK"];
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);
        var converter = new RelewisePropertyConverter([]);
        var converted = await converter.ConvertPropertyAsync(
            source, source, property, cultures, operation, new EditorialBlocksConverter(Variations()));
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
        foreach (var field in converted.Data)
        {
            entity.Data.Add(field.Key, field.Value);
        }

        var displayName = entity.DisplayName;
        await new EditorialMapper(Variations()).MapAsync(new EntityMappingContext<Content>(source, entity, cultures, operation), default);

        var values = entity.Data["searchText"]!.ValueAs<Multilingual>().Values!;
        Assert.Equal("English body\nEnglish block", values.Single(value => string.Equals(value.Language.Value, "en-US", StringComparison.OrdinalIgnoreCase)).Text);
        Assert.Equal("Dansk brødtekst\nDansk blok", values.Single(value => string.Equals(value.Language.Value, "da-DK", StringComparison.OrdinalIgnoreCase)).Text);
        Assert.Equal("article", entity.Id);
        Assert.Same(displayName, entity.DisplayName);
        Assert.Equal("Keep me", entity.Data["unrelated"]!.ValueAs<string>());
    }

    [Fact]
    public async Task APartialCultureExportDoesNotReadOrEmitAnotherCulture()
    {
        var text = Property("text", culture => culture == "da-DK" ? "Dansk blok" : throw new InvalidOperationException("Unrequested culture"), true);
        var property = Property("contentRows", _ => Blocks(text));
        var source = Source(property);
        var entity = new Content("article") { Data = new() { ["bodyText"] = "Dansk brødtekst" } };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(new EntityMappingContext<Content>(source, entity, ["da-DK"], operation), default);

        Assert.Equal("Dansk brødtekst\nDansk blok", entity.Data["searchText"]!.ValueAs<string>());
    }

    [Fact]
    public async Task RemovingAllEditorialTextOverwritesTheEarlierSearchText()
    {
        var source = Source(Property("contentRows", _ => new BlockListModel([])));
        var entity = new Content("article") { Data = new() { ["bodyText"] = "", ["contentRows"] = "", ["searchText"] = "Old text" } };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(new EntityMappingContext<Content>(source, entity, ["en-US", "da-DK"], operation), default);

        Assert.All(entity.Data["searchText"]!.ValueAs<Multilingual>().Values!, value => Assert.Equal("", value.Text));
    }

    [Fact]
    public void NestedBlocksExcludeHiddenContentAndStripNonEditorialHtml()
    {
        var nested = Blocks(Property("text", _ => new HtmlEncodedString("<p>Nested text</p><script>secret()</script>")));
        var outer = Element(Property("heading", _ => "Heading"), Property("items", _ => nested));
        var hidden = Element(Property("hide", _ => true));
        var blocks = new BlockListModel([
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
        var source = Source(Property("pageGrid", _ => new BlockGridModel([parent], 12)));
        var entity = new Content("article") { Data = new() { ["bodyText"] = "Body" } };
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), default);

        await new EditorialMapper(Variations()).MapAsync(new EntityMappingContext<Content>(source, entity, ["en-US"], operation), default);

        Assert.Equal("Body\nGrid heading\nArea child", entity.Data["searchText"]!.ValueAs<string>());
    }

    [Fact]
    public async Task ConverterAndMapperRespectCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var property = Property("contentRows", _ => null, editor: "Umbraco.BlockList");
        var context = new RelewisePropertyConverterContext(property, "en-US", new());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new EditorialBlocksConverter(Variations()).ConvertAsync(context, cancellation.Token));
        await using var operation = new MappingOperation(Mock.Of<IServiceProvider>(), cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorialMapper(Variations()).MapAsync(
            new EntityMappingContext<Content>(Source(property), new Content("article"), ["en-US"], operation), cancellation.Token));
    }

    private static IVariationContextAccessor Variations()
    {
        var accessor = new Mock<IVariationContextAccessor>();
        accessor.SetupProperty(value => value.VariationContext);
        return accessor.Object;
    }

    private static BlockListModel Blocks(params IPublishedProperty[] properties)
        => new([new BlockListItem(Guid.NewGuid(), Element(properties), null, null)]);

    private static IPublishedElement Element(params IPublishedProperty[] properties)
    {
        var element = new Mock<IPublishedElement>();
        element.Setup(value => value.GetProperty(It.IsAny<string>()))
            .Returns((string alias) => properties.FirstOrDefault(property => property.Alias == alias));
        return element.Object;
    }

    private static IPublishedContent Source(params IPublishedProperty[] properties)
    {
        var source = new Mock<IPublishedContent>();
        source.Setup(value => value.GetProperty(It.IsAny<string>()))
            .Returns((string alias) => properties.FirstOrDefault(property => property.Alias == alias));
        return source.Object;
    }

    private static IPublishedProperty Property(string alias, Func<string?, object?> value, bool varies = false, string editor = "Umbraco.TextBox")
    {
        var type = new Mock<IPublishedPropertyType>();
        type.SetupGet(property => property.EditorAlias).Returns(editor);
        type.SetupGet(property => property.Variations).Returns(varies ? ContentVariation.Culture : ContentVariation.Nothing);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(property => property.Alias).Returns(alias);
        property.SetupGet(property => property.PropertyType).Returns(type.Object);
        property.Setup(property => property.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns((string? culture, string? _) => value(culture));
        return property.Object;
    }
}
