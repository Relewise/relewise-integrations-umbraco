using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

internal static class DemoSeed
{
    internal static async Task<bool> InitializeAsync(IServiceProvider root)
    {
        await using var scope = root.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        if (services.GetRequiredService<IContentTypeService>().Get("product") != null)
        {
            return false;
        }

        await Prepare(services.GetRequiredService<IContentTypeService>(), services.GetRequiredService<IDataTypeService>(),
            services.GetRequiredService<ITemplateService>(), services.GetRequiredService<IContentService>(), services.GetRequiredService<IShortStringHelper>(),
            services.GetRequiredService<IWebHostEnvironment>(), services.GetRequiredService<PropertyEditorCollection>(),
            services.GetRequiredService<IConfigurationEditorJsonSerializer>(), services.GetRequiredService<ILanguageService>());
        await Run(services.GetRequiredService<IContentService>(), services.GetRequiredService<IContentTypeService>(),
            services.GetRequiredService<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>(), services.GetRequiredService<IDomainService>());
        return true;
    }

    internal static async Task<IResult> Prepare(IContentTypeService types, IDataTypeService dataTypes, ITemplateService templates,
        IContentService content, IShortStringHelper strings, IWebHostEnvironment environment,
        PropertyEditorCollection editors, IConfigurationEditorJsonSerializer serializer, ILanguageService languages)
    {
        var available = (await dataTypes.GetAllAsync()).ToArray();
        var text = available.First(type => type.Name == "Textstring");
        var picker = available.First(type => type.EditorAlias == "Umbraco.ContentPicker");
        var numeric = available.First(type => type.Name == "Numeric");
        if (await languages.GetAsync("da-DK") == null)
        {
            await languages.CreateAsync(new Language("da-DK", "Danish"), Constants.Security.SuperUserKey);
        }

        var categories = available.FirstOrDefault(type => type.Name == "RFC category references");
        if (categories == null)
        {
            categories = new DataType(editors.Single(editor => editor.Alias == "Umbraco.DocumentPicker.Multiple"), serializer) { Name = "RFC category references" };
            await dataTypes.CreateAsync(categories, Constants.Security.SuperUserKey);
        }
        var elementType = types.Get("editorialText");
        if (elementType == null)
        {
            elementType = new ContentType(strings, -1) { Alias = "editorialText", Name = "Editorial text", IsElement = true };
            elementType.AddPropertyType(new PropertyType(strings, text, "heading") { Name = "Heading" });
            elementType.AddPropertyType(new PropertyType(strings, text, "text") { Name = "Text" });
            await types.CreateAsync(elementType, Constants.Security.SuperUserKey);
        }
        var blocks = available.FirstOrDefault(type => type.Name == "Demo editorial blocks");
        if (blocks == null)
        {
            blocks = new DataType(editors.Single(editor => editor.Alias == "Umbraco.BlockList"), serializer)
            {
                Name = "Demo editorial blocks",
                ConfigurationData = new Dictionary<string, object> { ["blocks"] = new[] { new { contentElementTypeKey = elementType.Key } } }
            };
            await dataTypes.CreateAsync(blocks, Constants.Security.SuperUserKey);
        }
        var template = await templates.GetAsync("storefront");
        if (template == null)
        {
            template = new Template(strings, "Storefront", "storefront") { Content = await System.IO.File.ReadAllTextAsync(Path.Combine(environment.ContentRootPath, "Views", "storefront.cshtml")) };
            await templates.CreateAsync(template, Constants.Security.SuperUserKey);
        }
        foreach (string alias in new[] { "home", "article", "guide", "folder", "brand", "productCategory", "product", "productVariant" })
        {
            var type = types.Get(alias);
            if (type == null)
            {
                type = new ContentType(strings, -1) { Alias = alias, Name = alias, AllowedAsRoot = true };
                foreach (string property in new[] { "heading", "bodyText", "heroImage", "sku", "categoryId", "brandId", "listPrice", "salesPrice", "color" })
                {
                    type.AddPropertyType(new PropertyType(strings, text, property) { Name = property });
                }

                type.AllowedTemplates = [template];
                type.SetDefaultTemplate(template);
                await types.CreateAsync(type, Constants.Security.SuperUserKey);
            }
            if (alias == "article" && !type.PropertyTypes.Any(property => property.Alias == "contentRows"))
            {
                type.AddPropertyType(new PropertyType(strings, blocks, "contentRows") { Name = "Editorial blocks" });
                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }
            if (alias == "product")
            {
                if (!type.PropertyTypes.Any(property => property.Alias == "brand"))
                {
                    type.AddPropertyType(new PropertyType(strings, picker, "brand") { Name = "Brand" });
                }

                if (!type.PropertyTypes.Any(property => property.Alias == "extraCategories"))
                {
                    type.AddPropertyType(new PropertyType(strings, categories, "extraCategories") { Name = "Additional categories" });
                }

                if (!type.PropertyTypes.Any(property => property.Alias == "stock"))
                {
                    type.AddPropertyType(new PropertyType(strings, numeric, "stock") { Name = "Stock" });
                }

                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }
        }
        if (types.Get("localizedArticle") == null)
        {
            var localized = new ContentType(strings, -1) { Alias = "localizedArticle", Name = "Localized article", AllowedAsRoot = true, Variations = ContentVariation.Culture };
            foreach (string alias in new[] { "heading", "bodyText", "score" })
            {
                localized.AddPropertyType(new PropertyType(strings, alias == "score" ? numeric : text, alias) { Name = alias, Variations = ContentVariation.Culture });
            }

            localized.AllowedTemplates = [template];
            localized.SetDefaultTemplate(template);
            await types.CreateAsync(localized, Constants.Security.SuperUserKey);
        }
        return Results.Ok(new
        {
            schemaReady = true
        });
    }
    internal static async Task<IResult> Run(IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes, IDomainService domains)
    {
        int templateId = types.Get("article")!.AllowedTemplates!.Single().Id;
        IContent Node(string name, int parent, string type, Dictionary<string, string>? properties = null)
        {
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var all = content.GetRootContent().SelectMany(root => new[] { root }.Concat(content.GetPagedDescendants(root.Id, 0, 1000, out _)));
            var node = all.FirstOrDefault(node => node.Name == name && node.ContentType.Alias == type) ?? content.Create(name, parent, type);
            node.TemplateId = templateId;
            node.SetValue("heading", name);
            node.SetValue("heroImage", "/images/" + (name.Contains("flask") ? "flask" : name.Contains("shelter") ? "shelter" : "daypack") + ".svg");
            foreach (var field in properties ?? [])
            {
                node.SetValue(field.Key, field.Value);
            }

            if (type == "product")
            {
                var brand = content.GetRootContent().Single(node => node.ContentType.Alias == "brand");
                node.SetValue("brand", Udi.Create(Constants.UdiEntityType.Document, brand.Key).ToString());
                node.SetValue("stock", name.Contains("flask") ? 20 : name.Contains("shelter") ? 30 : 10);
            }
            if (type == "article")
            {
                Guid blockKey = Guid.NewGuid();
                node.SetValue("contentRows", System.Text.Json.JsonSerializer.Serialize(new
                {
                    layout = new Dictionary<string, object> { ["Umbraco.BlockList"] = new[] { new { contentKey = blockKey } } },
                    contentData = new[] { new { key = blockKey, contentTypeKey = types.Get("editorialText")!.Key, values = new[] {
                        new { editorAlias = "Umbraco.TextBox", alias = "heading", value = "Ready for the outdoors", culture = (string?)null, segment = (string?)null },
                        new { editorAlias = "Umbraco.TextBox", alias = "text", value = "Bring a reusable flask and enjoy the quiet outdoors.", culture = (string?)null, segment = (string?)null }
                    } } },
                    settingsData = Array.Empty<object>(),
                    expose = new[] { new { contentKey = blockKey, culture = (string?)null, segment = (string?)null } }
                }));
            }
            content.Save(node);
            var published = content.Publish(node, ["*"]);
            if (!published.Success)
            {
                throw new InvalidOperationException($"Failed publishing {name}: {published.Result}");
            }

            scope.Complete();
            return node;
        }
        Node("Fieldwork storefront", -1, "home");
        var brand = Node("Fieldwork", -1, "brand", new()
        {
            ["brandId"] = "U19-SMOKE-BRAND"
        });
        var adventures = Node("Fieldwork adventures", -1, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-ADVENTURES"
        });
        var folder = Node("Trail routes", adventures.Id, "folder");
        var hikes = Node("Day hikes", folder.Id, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-DAYHIKES"
        });
        var category = Node("Fieldwork equipment", -1, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-EQUIPMENT"
        });
        var pack = Node("Trail daypack", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-PACK",
            ["listPrice"] = "899",
            ["salesPrice"] = "699",
            ["bodyText"] = "A lightweight daypack for trails, city commutes and everything between."
        });
        using (var scope = scopes.CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            pack.SetValue("extraCategories", System.Text.Json.JsonSerializer.Serialize(new[] { category.Key, hikes.Key, hikes.Key }));
            content.Save(pack);
            if (!content.Publish(pack, ["*"]).Success)
            {
                throw new InvalidOperationException("Failed publishing category references.");
            }

            scope.Complete();
        }
        Node("Trail daypack Forest", pack.Id, "productVariant", new()
        {
            ["sku"] = "U19-SMOKE-PACK-FOREST",
            ["color"] = "Forest",
            ["bodyText"] = "The forest green edition of our everyday daypack."
        });
        Node("Trail daypack Sand", pack.Id, "productVariant", new()
        {
            ["sku"] = "U19-SMOKE-PACK-SAND",
            ["color"] = "Sand",
            ["bodyText"] = "A warm sand finish, made for all seasons."
        });
        Node("Trail flask", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-FLASK",
            ["listPrice"] = "299",
            ["salesPrice"] = "249",
            ["bodyText"] = "Keep your coffee warm and your water cool, from first light to late evening."
        });
        Node("Camp shelter", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-SHELTER",
            ["listPrice"] = "1999",
            ["salesPrice"] = "1599",
            ["bodyText"] = "Simple shelter, thoughtful details. A place to pause on your next adventure."
        });
        var article = Node("Finding your trail", -1, "article", new()
        {
            ["bodyText"] = "Our field guide to getting outdoors. Choose your route, pack lightly and leave no trace."
        });
        Node("A slower kind of weekend", -1, "article", new()
        {
            ["bodyText"] = "Good coffee, a quiet campsite and time to explore. The essentials for a weekend outside."
        });
        Node("Packing for your first camp", article.Id, "guide", new()
        {
            ["bodyText"] = "A practical packing checklist from the Fieldwork team."
        });
        using (var scope = scopes.CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var localized = content.GetRootContent().FirstOrDefault(node => node.ContentType.Alias == "localizedArticle") ?? content.Create("Across the seasons", -1, "localizedArticle");
            localized.TemplateId = templateId;
            localized.SetCultureName("Across the seasons", "en-US");
            localized.SetCultureName("Gennem årstiderne", "da-DK");
            localized.SetValue("heading", "Across the seasons", "en-US");
            localized.SetValue("heading", "Gennem årstiderne", "da-DK");
            localized.SetValue("bodyText", "Find your seasonal adventure.", "en-US");
            localized.SetValue("bodyText", "Find dit næste eventyr.", "da-DK");
            localized.SetValue("score", 10, "en-US");
            localized.SetValue("score", 20, "da-DK");
            content.Save(localized);
            if (!content.Publish(localized, ["en-US", "da-DK"]).Success)
            {
                throw new InvalidOperationException("Failed publishing both cultures.");
            }

            scope.Complete();
        }
        var localizedRoot = content.GetRootContent().Single(node => node.ContentType.Alias == "localizedArticle");
        var domainResult = await domains.UpdateDomainsAsync(localizedRoot.Key, new Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel
        {
            DefaultIsoCode = "en-US",
            Domains = [new() { DomainName = "http://localhost:5521/en", IsoCode = "en-US" }, new() { DomainName = "http://localhost:5521/da", IsoCode = "da-DK" }]
        });
        if (!domainResult.Success)
        {
            throw new InvalidOperationException($"Failed configuring localized storefront routes: {domainResult.Status}.");
        }

        return Results.Ok(new
        {
            seeded = true
        });
    }
}
