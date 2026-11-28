using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Seeding;

internal static class DemoEditorialSchema
{
    public static readonly Guid GridAreaKey = Guid.Parse("79296d5c-10c4-4053-bc78-f1ea40602050");

    public static async Task<(IDataType Blocks, IDataType Grid, IDataType Prices)> Create(
        IContentTypeService types,
        IDataTypeService dataTypes,
        IShortStringHelper strings,
        PropertyEditorCollection editors,
        IConfigurationEditorJsonSerializer serializer)
    {
        List<IDataType> available = [.. (await dataTypes.GetAllAsync())];
        IDataType text = available.First(type => type.Name == "Textstring");
        IDataType richText = available.First(type => type.EditorAlias == "Umbraco.RichText");
        IDataType boolean = available.First(type => type.EditorAlias == "Umbraco.TrueFalse");

        async Task<IDataType> Data(
            string name,
            string editor,
            Dictionary<string, object> configuration)
        {
            IDataType? existing = available.FirstOrDefault(type => type.Name == name);

            if (existing != null)
            {
                return existing;
            }

            var created = new DataType(editors.Single(value => value.Alias == editor), serializer)
            {
                Name = name,
                EditorUiAlias = available.FirstOrDefault(type => type.EditorAlias == editor)?.EditorUiAlias ??
                                editor switch
                                {
                                    "Umbraco.BlockList" => "Umb.PropertyEditorUi.BlockList",
                                    "Umbraco.BlockGrid" => "Umb.PropertyEditorUi.BlockGrid",
                                    "Umbraco.Decimal" => "Umb.PropertyEditorUi.Decimal",
                                    _ => throw new InvalidOperationException(
                                        $"No demo UI is configured for '{editor}'.")
                                },
                ConfigurationData = configuration
            };

            await dataTypes.CreateAsync(created, Constants.Security.SuperUserKey);
            available.Add(created);

            return created;
        }

        async Task<IContentType> Element(
            string alias,
            string name,
            IDataType body,
            bool localized = false)
        {
            IContentType? existing = types.Get(alias);

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

            created.AddPropertyType(
                new PropertyType(strings, text, PropertyAliases.Heading)
                { Name = "Heading", Variations = created.Variations });
            created.AddPropertyType(
                new PropertyType(strings, body, PropertyAliases.Text)
                { Name = "Text", Variations = created.Variations });
            await types.CreateAsync(created, Constants.Security.SuperUserKey);

            return created;
        }

        IDataType currencies = await Data(
            "Demo currencies",
            "Umbraco.DropDown.Single",
            new Dictionary<string, object>
            {
                ["items"] = new[] { "DKK", "EUR", "USD" }
            }
        );
        IDataType amount = await Data(
            "Demo price amount",
            Constants.PropertyEditors.Aliases.Decimal,
            new Dictionary<string, object>
            {
                ["step"] = 0.01d
            }
        );
        IContentType? price = types.Get("currencyPrice");

        if (price == null)
        {
            price = new ContentType(strings, -1) { Alias = "currencyPrice", Name = "Currency price", IsElement = true };
            price.AddPropertyType(
                new PropertyType(strings, currencies, PropertyAliases.Currency)
                { Name = "Currency", Mandatory = true });
            price.AddPropertyType(
                new PropertyType(strings, amount, PropertyAliases.Amount) { Name = "Amount", Mandatory = true });
            await types.CreateAsync(price, Constants.Security.SuperUserKey);
        }

        IDataType prices = await Data(
            "Demo multi-currency prices",
            "Umbraco.BlockList",
            new Dictionary<string, object>
            {
                ["blocks"] = new[] { new { contentElementTypeKey = price.Key, label = "{=currency} {=amount}" } }
            }
        );

        IContentType editorial = await Element(DocumentTypeAliases.EditorialText, "Editorial rich text", richText);
        IContentType localizedText = await Element(
            DocumentTypeAliases.LocalizedEditorialText,
            "Localized editorial text",
            richText,
            true
        );
        IContentType group = await Element(DocumentTypeAliases.EditorialGroup, "Editorial group", text);
        IContentType localizedGroup = await Element(
            DocumentTypeAliases.LocalizedEditorialGroup,
            "Localized editorial group",
            text,
            true);
        IContentType? visibility = types.Get(DocumentTypeAliases.EditorialVisibility);

        if (visibility == null)
        {
            visibility = new ContentType(strings, -1)
            { Alias = DocumentTypeAliases.EditorialVisibility, Name = "Editorial visibility", IsElement = true };
            visibility.AddPropertyType(
                new PropertyType(strings, boolean, PropertyAliases.Hide)
                { Name = "Hide from editorial text" });
            await types.CreateAsync(visibility, Constants.Security.SuperUserKey);
        }

        IDataType blocks = await Data(
            "Demo editorial blocks",
            "Umbraco.BlockList",
            new Dictionary<string, object>
            {
                ["blocks"] = new[] { editorial, localizedText, group, localizedGroup }.Select(type => new
                {
                    contentElementTypeKey = type.Key,
                    settingsElementTypeKey = visibility.Key,
                    label = "{=heading}"
                })
                    .ToArray()
            });

        foreach (IContentType container in new[] { group, localizedGroup })
        {
            if (!container.PropertyTypes.Any(property => property.Alias == PropertyAliases.Items))
            {
                container.AddPropertyType(
                    new PropertyType(strings, blocks, PropertyAliases.Items)
                    { Name = "Nested editorial blocks" });
                await types.UpdateAsync(container, Constants.Security.SuperUserKey);
            }
        }

        IDataType grid = await Data(
            "Demo editorial grid",
            "Umbraco.BlockGrid",
            new Dictionary<string, object>
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
                })
                    .ToArray()
            });

        return (blocks, grid, prices);
    }
}
