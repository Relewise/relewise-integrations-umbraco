using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.DemoStorefront;

internal static class DemoSeed
{
    internal static async Task InitializeAsync(IServiceProvider root)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        IContentTypeService contentTypes = services.GetRequiredService<IContentTypeService>();
        if (contentTypes.Get("product") != null)
        {
            if (contentTypes.Get("currencyPrice") == null)
            {
                throw new InvalidOperationException("This demo database uses an earlier schema. Stop the site and move its ignored umbraco/Data directory aside to seed the updated examples.");
            }

            await DemoCatalogue.EnsureAsync(services);
            await DemoLocalization.EnsureAsync(services);
            await DemoProductDetails.EnsureAsync(services);
            await DemoVariantDetails.EnsureAsync(services);
            DemoEditorial.Ensure(services);
            await DemoProductTypes.EnsureAsync(services);
            return;
        }

        await Prepare(services.GetRequiredService<IContentTypeService>(), services.GetRequiredService<IDataTypeService>(),
            services.GetRequiredService<ITemplateService>(), services.GetRequiredService<IContentService>(), services.GetRequiredService<IShortStringHelper>(),
            services.GetRequiredService<IWebHostEnvironment>(), services.GetRequiredService<PropertyEditorCollection>(),
            services.GetRequiredService<IConfigurationEditorJsonSerializer>(), services.GetRequiredService<ILanguageService>()
        );
        await Run(services.GetRequiredService<IContentService>(), services.GetRequiredService<IContentTypeService>(),
            services.GetRequiredService<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>(), services.GetRequiredService<IDomainService>(),
            services.GetRequiredService<IConfiguration>()
        );
        await DemoCatalogue.EnsureAsync(services);
        await DemoLocalization.EnsureAsync(services);
        await DemoProductDetails.EnsureAsync(services);
        await DemoVariantDetails.EnsureAsync(services);
        DemoEditorial.Ensure(services);
        await DemoProductTypes.EnsureAsync(services);
    }

    private static async Task Prepare(IContentTypeService types, IDataTypeService dataTypes, ITemplateService templates,
        IContentService content, IShortStringHelper strings, IWebHostEnvironment environment,
        PropertyEditorCollection editors, IConfigurationEditorJsonSerializer serializer, ILanguageService languages)
    {
        IDataType[] available = (await dataTypes.GetAllAsync()).ToArray();
        IDataType text = available.First(type => type.Name == "Textstring");
        IDataType picker = available.First(type => type.EditorAlias == "Umbraco.ContentPicker");
        IDataType numeric = available.First(type => type.Name == "Numeric");
        if (await languages.GetAsync("da-DK") == null)
        {
            await languages.CreateAsync(new Language("da-DK", "Danish"), Constants.Security.SuperUserKey);
        }

        IDataType? categories = available.FirstOrDefault(type => type.Name == "RFC category references");
        if (categories == null)
        {
            categories = new DataType(editors.Single(editor => editor.Alias == "Umbraco.DocumentPicker.Multiple"), serializer)
            {
                Name = "RFC category references",
                EditorUiAlias = "Umb.PropertyEditorUi.DocumentPicker.Multiple"
            };
            await dataTypes.CreateAsync(categories, Constants.Security.SuperUserKey);
        }
        (IDataType Blocks, IDataType Grid, IDataType Prices) schema = await DemoEditorialSchema.Create(
            types,
            dataTypes,
            strings,
            editors,
            serializer
        );
        ITemplate? template = await templates.GetAsync("storefront");
        if (template == null)
        {
            template = new Template(strings, "Storefront", "storefront")
            {
                Content = await System.IO.File.ReadAllTextAsync(Path.Combine(
                    environment.ContentRootPath,
                    "Views",
                    "storefront.cshtml"
                ))
            };
            await templates.CreateAsync(template, Constants.Security.SuperUserKey);
        }
        foreach (string alias in new[] { "home", "article", "guide", "folder", "brand", "productCategory", "product", "productVariant" })
        {
            IContentType? type = types.Get(alias);
            if (type == null)
            {
                type = new ContentType(strings, -1) { Alias = alias, Name = alias, AllowedAsRoot = true };
                foreach (string property in new[] { "heading", "bodyText", "heroImage", "sku", "categoryId", "brandId", "color" })
                {
                    type.AddPropertyType(new PropertyType(strings, text, property) { Name = property });
                }

                type.AllowedTemplates = [template];
                type.SetDefaultTemplate(template);
                await types.CreateAsync(type, Constants.Security.SuperUserKey);
            }
            if (alias == "article" && !type.PropertyTypes.Any(property => property.Alias == "contentRows"))
            {
                type.AddPropertyType(new PropertyType(strings, schema.Blocks, "contentRows") { Name = "Editorial blocks" });
                type.AddPropertyType(new PropertyType(strings, schema.Grid, "pageGrid") { Name = "Editorial Grid" });
                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }
            if (alias == "product")
            {
                if (!type.PropertyTypes.Any(property => property.Alias == "listPrice"))
                {
                    type.AddPropertyType(new PropertyType(strings, schema.Prices, "listPrice") { Name = "List prices" });
                    type.AddPropertyType(new PropertyType(strings, schema.Prices, "salesPrice") { Name = "Sales prices" });
                }
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
            // The outer editors are invariant; localized values live in nested block elements.
            localized.AddPropertyType(new PropertyType(strings, schema.Blocks, "contentRows") { Name = "Localized nested blocks" });
            localized.AddPropertyType(new PropertyType(strings, schema.Grid, "pageGrid") { Name = "Editorial Grid" });

            localized.AllowedTemplates = [template];
            localized.SetDefaultTemplate(template);
            await types.CreateAsync(localized, Constants.Security.SuperUserKey);
        }
    }

    private static async Task Run(IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes, IDomainService domains, IConfiguration configuration)
    {
        int templateId = types.Get("article")!.AllowedTemplates!.Single().Id;
        Guid priceType = types.Get("currencyPrice")!.Key;
        IContent Node(string name, int parent, string type, Dictionary<string, string>? properties = null)
        {
            using IScope scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            IEnumerable<IContent> all = content.GetRootContent().SelectMany(root => new[] { root }.Concat(content.GetPagedDescendants(
                root.Id,
                0,
                1000,
                out _
            )));
            IContent node = all.FirstOrDefault(node => node.Name == name && node.ContentType.Alias == type) ?? content.Create(
                name,
                parent,
                type
            );
            node.TemplateId = templateId;
            node.SetValue("heading", name);
            node.SetValue("heroImage", "/images/" + (name.Contains("flask") ? "flask" : name.Contains("shelter") ? "shelter" : "daypack") + ".svg");
            foreach (KeyValuePair<string, string> field in properties ?? [])
            {
                node.SetValue(field.Key, field.Value);
            }

            if (type == "product")
            {
                IContent brand = content.GetRootContent().Single(node => node.ContentType.Alias == "brand");
                node.SetValue("brand", Udi.Create(Constants.UdiEntityType.Document, brand.Key).ToString());
                node.SetValue("stock", name.Contains("flask") ? 20 : name.Contains("shelter") ? 30 : 10);
            }
            if (type == "article")
            {
                node.SetValue("contentRows", DemoBlockValues.EditorialList(types, false));
                node.SetValue("pageGrid", DemoBlockValues.Grid(types));
            }
            content.Save(node);
            PublishResult published = content.Publish(node, ["*"]);
            if (!published.Success)
            {
                throw new InvalidOperationException($"Failed publishing {name}: {published.Result}");
            }

            scope.Complete();
            return node;
        }
        Node("Fieldwork storefront", -1, "home");
        IContent brand = Node("Fieldwork", -1, "brand", new()
        {
            ["brandId"] = "U19-SMOKE-BRAND"
        }
        );
        IContent adventures = Node("Fieldwork adventures", -1, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-ADVENTURES"
        }
        );
        IContent folder = Node("Trail routes", adventures.Id, "folder");
        IContent hikes = Node("Day hikes", folder.Id, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-DAYHIKES"
        }
        );
        IContent category = Node("Fieldwork equipment", -1, "productCategory", new()
        {
            ["categoryId"] = "U19-SMOKE-EQUIPMENT"
        }
        );
        IContent pack = Node("Trail daypack", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-PACK",
            ["listPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 899m), ("EUR", 119.95m), ("USD", 129.95m)),
            ["salesPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 699m), ("EUR", 94.95m), ("USD", 99.95m)),
            ["bodyText"] = "A lightweight daypack for trails, city commutes and everything between."
        }
        );
        using (IScope scope = scopes.CreateScope())
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
        }
        );
        Node("Trail daypack Sand", pack.Id, "productVariant", new()
        {
            ["sku"] = "U19-SMOKE-PACK-SAND",
            ["color"] = "Sand",
            ["bodyText"] = "A warm sand finish, made for all seasons."
        }
        );
        Node("Trail flask", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-FLASK",
            ["listPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 299m), ("EUR", 39.95m), ("USD", 44.95m)),
            ["salesPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 249m), ("EUR", 34.95m), ("USD", 36.95m)),
            ["bodyText"] = "Keep your coffee warm and your water cool, from first light to late evening."
        }
        );
        Node("Camp shelter", category.Id, "product", new()
        {
            ["sku"] = "U19-SMOKE-SHELTER",
            ["listPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 1999m), ("EUR", 269.95m), ("USD", 289.95m)),
            ["salesPrice"] = DemoBlockValues.Prices(priceType, ("DKK", 1599m), ("EUR", 214.95m), ("USD", 229.95m)),
            ["bodyText"] = "Simple shelter, thoughtful details. A place to pause on your next adventure."
        }
        );
        IContent article = Node("Finding your trail", -1, "article", new()
        {
            ["bodyText"] = "Our field guide to getting outdoors. Choose your route, pack lightly and leave no trace."
        }
        );
        Node("A slower kind of weekend", -1, "article", new()
        {
            ["bodyText"] = "Good coffee, a quiet campsite and time to explore. The essentials for a weekend outside."
        }
        );
        Node("Packing for your first camp", article.Id, "guide", new()
        {
            ["bodyText"] = "A practical packing checklist from the Fieldwork team."
        }
        );
        using (IScope scope = scopes.CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            IContent localized = content.GetRootContent().FirstOrDefault(node => node.ContentType.Alias == "localizedArticle") ?? content.Create(
                "Across the seasons",
                -1,
                "localizedArticle"
            );
            localized.TemplateId = templateId;
            localized.SetCultureName("Across the seasons", "en-US");
            localized.SetCultureName("Gennem årstiderne", "da-DK");
            localized.SetValue("heading", "Across the seasons", "en-US");
            localized.SetValue("heading", "Gennem årstiderne", "da-DK");
            localized.SetValue("bodyText", "Find your seasonal adventure.", "en-US");
            localized.SetValue("bodyText", "Find dit næste eventyr.", "da-DK");
            localized.SetValue("score", 10, "en-US");
            localized.SetValue("score", 20, "da-DK");
            localized.SetValue("contentRows", DemoBlockValues.EditorialList(types, true));
            localized.SetValue("pageGrid", DemoBlockValues.Grid(types));
            content.Save(localized);
            if (!content.Publish(localized, ["en-US", "da-DK"]).Success)
            {
                throw new InvalidOperationException("Failed publishing both cultures.");
            }

            scope.Complete();
        }
        IContent localizedRoot = content.GetRootContent().Single(node => node.ContentType.Alias == "localizedArticle");
        Attempt<DomainUpdateResult, DomainOperationStatus> domainResult = await domains.UpdateDomainsAsync(localizedRoot.Key, new Umbraco.Cms.Core.Models.ContentEditing.DomainsUpdateModel
        {
            DefaultIsoCode = "en-US",
            Domains = [new() { DomainName = configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!.TrimEnd('/') + "/en", IsoCode = "en-US" }, new() { DomainName = configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!.TrimEnd('/') + "/da", IsoCode = "da-DK" }]
        }
        );
        if (!domainResult.Success)
        {
            throw new InvalidOperationException($"Failed configuring localized storefront routes: {domainResult.Status}.");
        }
    }
}
