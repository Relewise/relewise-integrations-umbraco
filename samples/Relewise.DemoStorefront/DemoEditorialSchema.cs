using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

internal static class DemoEditorialSchema
{
    internal static readonly Guid GridAreaKey = Guid.Parse("79296d5c-10c4-4053-bc78-f1ea40602050");

    internal static async Task<(IDataType Blocks, IDataType Grid, IDataType Prices)> Create(
        IContentTypeService types,
        IDataTypeService dataTypes,
        IShortStringHelper strings,
        PropertyEditorCollection editors,
        IConfigurationEditorJsonSerializer serializer)
    {
        var available = (await dataTypes.GetAllAsync()).ToList();
        IDataType text = available.First(type => type.Name == "Textstring");
        IDataType richText = available.First(type => type.EditorAlias == "Umbraco.RichText");
        IDataType boolean = available.First(type => type.EditorAlias == "Umbraco.TrueFalse");

        async Task<IDataType> Data(string name, string editor, Dictionary<string, object> configuration)
        {
            var existing = available.FirstOrDefault(type => type.Name == name);
            if (existing != null)
            {
                return existing;
            }

            var created = new DataType(editors.Single(value => value.Alias == editor), serializer)
            {
                Name = name,
                EditorUiAlias = available.FirstOrDefault(type => type.EditorAlias == editor)?.EditorUiAlias ?? editor switch
                {
                    "Umbraco.BlockList" => "Umb.PropertyEditorUi.BlockList",
                    "Umbraco.BlockGrid" => "Umb.PropertyEditorUi.BlockGrid",
                    "Umbraco.Decimal" => "Umb.PropertyEditorUi.Decimal",
                    _ => throw new InvalidOperationException($"No demo UI is configured for '{editor}'.")
                },
                ConfigurationData = configuration
            };
            await dataTypes.CreateAsync(created, Constants.Security.SuperUserKey);
            available.Add(created);
            return created;
        }

        async Task<IContentType> Element(string alias, string name, IDataType body, bool localized = false)
        {
            var existing = types.Get(alias);
            if (existing != null)
            {
                return existing;
            }

            var created = new ContentType(strings, -1)
            {
                Alias = alias,
                Name = name,
                IsElement = true,
                Variations = localized ? ContentVariation.Culture : ContentVariation.Nothing
            };
            created.AddPropertyType(new PropertyType(strings, text, "heading") { Name = "Heading", Variations = created.Variations });
            created.AddPropertyType(new PropertyType(strings, body, "text") { Name = "Text", Variations = created.Variations });
            await types.CreateAsync(created, Constants.Security.SuperUserKey);
            return created;
        }

        IDataType currencies = await Data(
            "Demo currencies",
            "Umbraco.DropDown.Single",
            new() { ["items"] = new[] { "DKK", "EUR", "USD" } });
        IDataType amount = await Data(
            "Demo price amount",
            Constants.PropertyEditors.Aliases.Decimal,
            new() { ["step"] = 0.01d });
        var price = types.Get("currencyPrice");
        if (price == null)
        {
            price = new ContentType(strings, -1) { Alias = "currencyPrice", Name = "Currency price", IsElement = true };
            price.AddPropertyType(new PropertyType(strings, currencies, "currency") { Name = "Currency", Mandatory = true });
            price.AddPropertyType(new PropertyType(strings, amount, "amount") { Name = "Amount", Mandatory = true });
            await types.CreateAsync(price, Constants.Security.SuperUserKey);
        }

        IDataType prices = await Data("Demo multi-currency prices", "Umbraco.BlockList", new()
        {
            ["blocks"] = new[] { new { contentElementTypeKey = price.Key, label = "{=currency} {=amount}" } }
        });

        IContentType editorial = await Element("editorialText", "Editorial rich text", richText);
        IContentType localizedText = await Element("localizedEditorialText", "Localized editorial text", richText, true);
        IContentType group = await Element("editorialGroup", "Editorial group", text);
        IContentType localizedGroup = await Element("localizedEditorialGroup", "Localized editorial group", text, true);
        var visibility = types.Get("editorialVisibility");
        if (visibility == null)
        {
            visibility = new ContentType(strings, -1) { Alias = "editorialVisibility", Name = "Editorial visibility", IsElement = true };
            visibility.AddPropertyType(new PropertyType(strings, boolean, "hide") { Name = "Hide from editorial text" });
            await types.CreateAsync(visibility, Constants.Security.SuperUserKey);
        }

        IDataType blocks = await Data("Demo editorial blocks", "Umbraco.BlockList", new()
        {
            ["blocks"] = new[] { editorial, localizedText, group, localizedGroup }.Select(type => new
            {
                contentElementTypeKey = type.Key,
                settingsElementTypeKey = visibility.Key,
                label = "{=heading}"
            }).ToArray()
        });

        foreach (var container in new[] { group, localizedGroup })
        {
            if (!container.PropertyTypes.Any(property => property.Alias == "items"))
            {
                container.AddPropertyType(new PropertyType(strings, blocks, "items") { Name = "Nested editorial blocks" });
                await types.UpdateAsync(container, Constants.Security.SuperUserKey);
            }
        }

        IDataType grid = await Data("Demo editorial grid", "Umbraco.BlockGrid", new()
        {
            ["gridColumns"] = 12,
            ["blocks"] = new[] { editorial, group }.Select(type => new
            {
                contentElementTypeKey = type.Key,
                settingsElementTypeKey = visibility.Key,
                allowAtRoot = true,
                allowInAreas = true,
                areaGridColumns = 12,
                areas = type.Key == group.Key
                    ? new[] { new { key = GridAreaKey, alias = "main", columnSpan = 12, rowSpan = 1 } }
                    : [],
                label = "{=heading}"
            }).ToArray()
        });

        return (blocks, grid, prices);
    }
}
