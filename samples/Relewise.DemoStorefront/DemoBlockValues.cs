using System.Text.Json;
using Umbraco.Cms.Core.Services;

// Build the native editor values so the published models exercise the actual Umbraco converters.
internal static class DemoBlockValues
{
    internal static string Prices(Guid priceType, params (string Currency, decimal Amount)[] prices)
    {
        Guid[] keys = prices.Select(_ => Guid.NewGuid()).ToArray();
        return Serialize(
            "Umbraco.BlockList",
            keys.Select(key => (object)new { contentKey = key }).ToArray(),
            prices.Select((price, index) => Element(
                keys[index],
                priceType,
                Value("currency", "Umbraco.DropDown.Single", JsonSerializer.Serialize(new[] { price.Currency })),
                Value("amount", "Umbraco.Decimal", price.Amount))).ToArray(),
            [],
            keys.Select(key => (object)new { contentKey = key, culture = (string?)null, segment = (string?)null }).ToArray());
    }

    internal static string EditorialList(IContentTypeService types, bool localized)
    {
        Guid textType = types.Get("editorialText")!.Key;
        Guid nestedType = types.Get(localized ? "localizedEditorialText" : "editorialText")!.Key;
        Guid groupType = types.Get(localized ? "localizedEditorialGroup" : "editorialGroup")!.Key;
        Guid settingType = types.Get("editorialVisibility")!.Key;
        Guid nestedKey = Guid.NewGuid();
        object[] nestedValues = localized
            ? [Value("heading", "Umbraco.TextBox", "English nested heading", "en-US"),
               Value("text", "Umbraco.RichText", RichText("<p>English <strong>woodland</strong> route &amp; picnic.</p>"), "en-US"),
               Value("heading", "Umbraco.TextBox", "Dansk indlejret overskrift", "da-DK"),
               Value("text", "Umbraco.RichText", RichText("<p>Dansk <strong>skovtur</strong> med kaffe &amp; madpakke.</p>"), "da-DK")]
            : [Value("heading", "Umbraco.TextBox", "Nested trail advice"),
               Value("text", "Umbraco.RichText", RichText("<p>Follow the <strong>woodland</strong> path &amp; refill your flask.</p>"))];
        object[] nestedExposure = (localized ? new[] { "en-US", "da-DK" } : new string?[] { null })
            .Select(culture => (object)new { contentKey = nestedKey, culture, segment = (string?)null }).ToArray();
        string nested = Serialize(
            "Umbraco.BlockList",
            [new { contentKey = nestedKey }],
            [Element(nestedKey, nestedType, nestedValues)],
            [],
            nestedExposure);
        Guid textKey = Guid.NewGuid();
        Guid groupKey = Guid.NewGuid();
        Guid hiddenKey = Guid.NewGuid();
        Guid settingsKey = Guid.NewGuid();
        object[] groupValues = localized
            ? [Value("heading", "Umbraco.TextBox", "Nested editorial content", "en-US"),
               Value("heading", "Umbraco.TextBox", "Indlejret redaktionelt indhold", "da-DK"),
               Value("items", "Umbraco.BlockList", JsonSerializer.Deserialize<JsonElement>(nested))]
            : [Value("heading", "Umbraco.TextBox", "Nested editorial content"),
               Value("items", "Umbraco.BlockList", JsonSerializer.Deserialize<JsonElement>(nested))];
        object[] exposure = new[] { textKey, hiddenKey }.Select(key => (object)new
        {
            contentKey = key,
            culture = (string?)null,
            segment = (string?)null
        }).Concat((localized ? new[] { "en-US", "da-DK" } : new string?[] { null })
            .Select(culture => (object)new { contentKey = groupKey, culture, segment = (string?)null })).ToArray();
        return Serialize(
            "Umbraco.BlockList",
            [new { contentKey = textKey }, new { contentKey = groupKey }, new { contentKey = hiddenKey, settingsKey }],
            [Element(textKey, textType,
                Value("heading", "Umbraco.TextBox", "Ready for the outdoors"),
                Value("text", "Umbraco.RichText", RichText("<p>Bring a <strong>reusable flask</strong> and enjoy the quiet outdoors.</p>"))),
             Element(groupKey, groupType, groupValues),
             Element(hiddenKey, textType,
                Value("heading", "Umbraco.TextBox", "Hidden block example"),
                Value("text", "Umbraco.RichText", RichText("<p>Hidden editorial marker: secret-list-note.</p>")))],
            [Element(settingsKey, settingType, Value("hide", "Umbraco.TrueFalse", true))],
            exposure);
    }

    internal static string Grid(IContentTypeService types)
    {
        Guid groupType = types.Get("editorialGroup")!.Key;
        Guid textType = types.Get("editorialText")!.Key;
        Guid parent = Guid.NewGuid();
        Guid child = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();
        Guid hiddenChild = Guid.NewGuid();
        Guid settings = Guid.NewGuid();
        return Serialize(
            "Umbraco.BlockGrid",
            [GridItem(parent, child), GridItem(hidden, hiddenChild, settings)],
            [Element(parent, groupType, Value("heading", "Umbraco.TextBox", "Grid adventure planner")),
             Element(child, textType,
                Value("heading", "Umbraco.TextBox", "Inside the Grid area"),
                Value("text", "Umbraco.RichText", RichText("<p>Pack a <em>lightweight picnic</em> for the ridge trail.</p>"))),
             Element(hidden, groupType, Value("heading", "Umbraco.TextBox", "Hidden Grid container")),
             Element(hiddenChild, textType, Value("text", "Umbraco.RichText", RichText("<p>Hidden child marker: secret-grid-note.</p>")))],
            [Element(settings, types.Get("editorialVisibility")!.Key, Value("hide", "Umbraco.TrueFalse", true))],
            new[] { parent, child, hidden, hiddenChild }.Select(key => (object)new
            {
                contentKey = key,
                culture = (string?)null,
                segment = (string?)null
            }).ToArray());
    }

    private static object GridItem(Guid key, Guid? child = null, Guid? settingsKey = null) => new
    {
        contentKey = key,
        settingsKey,
        columnSpan = 12,
        rowSpan = 1,
        areas = child.HasValue
            ? new object[] { new { key = DemoEditorialSchema.GridAreaKey, items = new[] { GridItem(child.Value) } } }
            : []
    };

    private static object RichText(string markup) => new
    {
        markup,
        blocks = new
        {
            layout = new { },
            contentData = Array.Empty<object>(),
            settingsData = Array.Empty<object>(),
            expose = Array.Empty<object>()
        }
    };

    private static object Element(Guid key, Guid type, params object[] values) => new { key, contentTypeKey = type, values };
    private static object Value(string alias, string editorAlias, object? value, string? culture = null)
        => new { alias, editorAlias, value, culture, segment = (string?)null };
    private static string Serialize(string editor, object[] layout, object[] content, object[] settings, object[] expose)
        => JsonSerializer.Serialize(new
        {
            layout = new Dictionary<string, object> { [editor] = layout },
            contentData = content,
            settingsData = settings,
            expose
        });
}
