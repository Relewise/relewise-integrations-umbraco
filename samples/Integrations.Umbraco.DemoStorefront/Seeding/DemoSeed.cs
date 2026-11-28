using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Seeding;

internal static class DemoSeed
{
    public static async Task InitializeAsync(IServiceProvider root)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        var contentTypes = services.GetRequiredService<IContentTypeService>();

        if (contentTypes.Get(DocumentTypeAliases.Product) != null)
        {
            if (contentTypes.Get("currencyPrice") == null)
            {
                throw new InvalidOperationException(
                    "This demo database uses an earlier schema. Stop the site and move its ignored " +
                    "umbraco/Data directory aside to seed the updated examples.");
            }

            await DemoCatalogue.EnsureAsync(services);
            await DemoLocalization.EnsureAsync(services);
            await DemoProductDetails.EnsureAsync(services);
            await DemoVariantDetails.EnsureAsync(services);
            DemoEditorial.Ensure(services);
            await DemoProductTypes.EnsureAsync(services);

            return;
        }

        await Prepare(
            services.GetRequiredService<IContentTypeService>(),
            services.GetRequiredService<IDataTypeService>(),
            services.GetRequiredService<ITemplateService>(),
            services.GetRequiredService<IShortStringHelper>(),
            services.GetRequiredService<IWebHostEnvironment>(),
            services.GetRequiredService<PropertyEditorCollection>(),
            services.GetRequiredService<IConfigurationEditorJsonSerializer>(),
            services.GetRequiredService<ILanguageService>()
        );
        await Run(
            services.GetRequiredService<IContentService>(),
            services.GetRequiredService<IContentTypeService>(),
            services.GetRequiredService<IScopeProvider>(),
            services.GetRequiredService<IDomainService>(),
            services.GetRequiredService<IConfiguration>()
        );
        await DemoCatalogue.EnsureAsync(services);
        await DemoLocalization.EnsureAsync(services);
        await DemoProductDetails.EnsureAsync(services);
        await DemoVariantDetails.EnsureAsync(services);
        DemoEditorial.Ensure(services);
        await DemoProductTypes.EnsureAsync(services);
    }

    private static async Task Prepare(
        IContentTypeService types,
        IDataTypeService dataTypes,
        ITemplateService templates,
        IShortStringHelper strings,
        IWebHostEnvironment environment,
        PropertyEditorCollection editors,
        IConfigurationEditorJsonSerializer serializer,
        ILanguageService languages)
    {
        IDataType[] available = [.. (await dataTypes.GetAllAsync())];
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
            categories = new DataType(
                editors.Single(editor => editor.Alias == "Umbraco.DocumentPicker.Multiple"),
                serializer)
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
                Content = await System.IO.File.ReadAllTextAsync(
                    Path.Combine(
                        environment.ContentRootPath,
                        "Views",
                        "storefront.cshtml"
                    ))
            };
            await templates.CreateAsync(template, Constants.Security.SuperUserKey);
        }

        foreach (string alias in new[]
                     {
                         DocumentTypeAliases.Home,
                         DocumentTypeAliases.Article,
                         DocumentTypeAliases.Guide,
                         DocumentTypeAliases.Folder,
                         DocumentTypeAliases.Brand,
                         DocumentTypeAliases.ProductCategory,
                         DocumentTypeAliases.Product,
                         DocumentTypeAliases.ProductVariant
                     })
        {
            IContentType? type = types.Get(alias);

            if (type == null)
            {
                type = new ContentType(strings, -1) { Alias = alias, Name = alias, AllowedAsRoot = true };

                foreach (string property in new[]
                             {
                                 PropertyAliases.Heading,
                                 PropertyAliases.BodyText,
                                 PropertyAliases.HeroImage,
                                 PropertyAliases.Sku,
                                 PropertyAliases.CategoryId,
                                 PropertyAliases.BrandId,
                                 PropertyAliases.Color
                             })
                {
                    type.AddPropertyType(new PropertyType(strings, text, property) { Name = property });
                }

                type.AllowedTemplates = [template];
                type.SetDefaultTemplate(template);
                await types.CreateAsync(type, Constants.Security.SuperUserKey);
            }

            if (alias == DocumentTypeAliases.Article
                && !type.PropertyTypes.Any(property => property.Alias == PropertyAliases.ContentRows))
            {
                type.AddPropertyType(
                    new PropertyType(strings, schema.Blocks, PropertyAliases.ContentRows)
                    { Name = "Editorial blocks" });
                type.AddPropertyType(
                    new PropertyType(strings, schema.Grid, PropertyAliases.PageGrid) { Name = "Editorial Grid" });
                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }

            if (alias == DocumentTypeAliases.Product)
            {
                if (!type.PropertyTypes.Any(property => property.Alias == PropertyAliases.ListPrice))
                {
                    type.AddPropertyType(
                        new PropertyType(strings, schema.Prices, PropertyAliases.ListPrice)
                        { Name = "List prices" });
                    type.AddPropertyType(
                        new PropertyType(strings, schema.Prices, PropertyAliases.SalesPrice)
                        { Name = "Sales prices" });
                }

                if (!type.PropertyTypes.Any(property => property.Alias == PropertyAliases.Brand))
                {
                    type.AddPropertyType(new PropertyType(strings, picker, PropertyAliases.Brand) { Name = "Brand" });
                }

                if (!type.PropertyTypes.Any(property => property.Alias == PropertyAliases.ExtraCategories))
                {
                    type.AddPropertyType(
                        new PropertyType(strings, categories, PropertyAliases.ExtraCategories)
                        { Name = "Additional categories" });
                }

                if (!type.PropertyTypes.Any(property => property.Alias == PropertyAliases.Stock))
                {
                    type.AddPropertyType(new PropertyType(strings, numeric, PropertyAliases.Stock) { Name = "Stock" });
                }

                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }
        }

        if (types.Get(DocumentTypeAliases.LocalizedArticle) == null)
        {
            var localized = new ContentType(strings, -1)
            {
                Alias = DocumentTypeAliases.LocalizedArticle,
                Name = "Localized article",
                AllowedAsRoot = true,
                Variations = ContentVariation.Culture
            };

            foreach (string alias in new[] { PropertyAliases.Heading, PropertyAliases.BodyText, PropertyAliases.Score })
            {
                localized.AddPropertyType(
                    new PropertyType(strings, alias == PropertyAliases.Score ? numeric : text, alias)
                    { Name = alias, Variations = ContentVariation.Culture });
            }

            // The outer editors are invariant; localized values live in nested block elements.
            localized.AddPropertyType(
                new PropertyType(strings, schema.Blocks, PropertyAliases.ContentRows)
                { Name = "Localized nested blocks" });
            localized.AddPropertyType(
                new PropertyType(strings, schema.Grid, PropertyAliases.PageGrid) { Name = "Editorial Grid" });

            localized.AllowedTemplates = [template];
            localized.SetDefaultTemplate(template);
            await types.CreateAsync(localized, Constants.Security.SuperUserKey);
        }
    }

    private static async Task Run(
        IContentService content,
        IContentTypeService types,
        IScopeProvider scopes,
        IDomainService domains,
        IConfiguration configuration)
    {
        int templateId = types.Get(DocumentTypeAliases.Article)!.AllowedTemplates!.Single().Id;
        Guid priceType = types.Get("currencyPrice")!.Key;

        IContent Node(
            string name,
            int parent,
            string type,
            Dictionary<string, string>? properties = null)
        {
            using IScope scope = scopes.CreateScope();

            scope.EagerWriteLock(Constants.Locks.ContentTree);

            IEnumerable<IContent> all = content.GetRootContent()
                .SelectMany(root =>
                    new[] { root }.Concat(
                        content.GetPagedDescendants(
                            root.Id,
                            0,
                            1000,
                            out _
                        )));
            IContent node = all.FirstOrDefault(node => node.Name == name && node.ContentType.Alias == type) ??
                            content.Create(
                                name,
                                parent,
                                type
                            );

            node.TemplateId = templateId;
            node.SetValue(PropertyAliases.Heading, name);
            node.SetValue(
                PropertyAliases.HeroImage,
                "/images/" + (name.Contains("flask") ? "flask" : name.Contains("shelter") ? "shelter" : "daypack") +
                ".svg");

            foreach (KeyValuePair<string, string> field in properties ?? [])
            {
                node.SetValue(field.Key, field.Value);
            }

            if (type == DocumentTypeAliases.Product)
            {
                IContent brand = content.GetRootContent().Single(
                    candidate => candidate.ContentType.Alias == DocumentTypeAliases.Brand);

                node.SetValue(
                    PropertyAliases.Brand,
                    Udi.Create(Constants.UdiEntityType.Document, brand.Key).ToString());
                node.SetValue(PropertyAliases.Stock, name.Contains("flask") ? 20 : name.Contains("shelter") ? 30 : 10);
            }

            if (type == DocumentTypeAliases.Article)
            {
                node.SetValue(PropertyAliases.ContentRows, DemoBlockValues.EditorialList(types, false));
                node.SetValue(PropertyAliases.PageGrid, DemoBlockValues.Grid(types));
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

        Node("Fieldwork storefront", -1, DocumentTypeAliases.Home);
        Node(
            "Fieldwork",
            -1,
            DocumentTypeAliases.Brand,
            new Dictionary<string, string>
            {
                [PropertyAliases.BrandId] = "U19-SMOKE-BRAND"
            }
        );

        IContent adventures = Node(
            "Fieldwork adventures",
            -1,
            DocumentTypeAliases.ProductCategory,
            new Dictionary<string, string>
            {
                [PropertyAliases.CategoryId] = "U19-SMOKE-ADVENTURES"
            }
        );
        IContent folder = Node("Trail routes", adventures.Id, DocumentTypeAliases.Folder);
        IContent hikes = Node(
            "Day hikes",
            folder.Id,
            DocumentTypeAliases.ProductCategory,
            new Dictionary<string, string>
            {
                [PropertyAliases.CategoryId] = "U19-SMOKE-DAYHIKES"
            }
        );
        IContent category = Node(
            "Fieldwork equipment",
            -1,
            DocumentTypeAliases.ProductCategory,
            new Dictionary<string, string>
            {
                [PropertyAliases.CategoryId] = "U19-SMOKE-EQUIPMENT"
            }
        );
        IContent pack = Node(
            "Trail daypack",
            category.Id,
            DocumentTypeAliases.Product,
            new Dictionary<string, string>
            {
                [PropertyAliases.Sku] = "U19-SMOKE-PACK",
                [PropertyAliases.ListPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 899m),
                    ("EUR", 119.95m),
                    ("USD", 129.95m)),
                [PropertyAliases.SalesPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 699m),
                    ("EUR", 94.95m),
                    ("USD", 99.95m)),
                [PropertyAliases.BodyText] = "A lightweight daypack for trails, city commutes and everything between."
            }
        );

        using (IScope scope = scopes.CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            pack.SetValue(
                PropertyAliases.ExtraCategories,
                JsonSerializer.Serialize(new[] { category.Key, hikes.Key, hikes.Key }));
            content.Save(pack);

            if (!content.Publish(pack, ["*"]).Success)
            {
                throw new InvalidOperationException("Failed publishing category references.");
            }

            scope.Complete();
        }

        Node(
            "Trail daypack Forest",
            pack.Id,
            DocumentTypeAliases.ProductVariant,
            new Dictionary<string, string>
            {
                [PropertyAliases.Sku] = "U19-SMOKE-PACK-FOREST",
                [PropertyAliases.Color] = "Forest",
                [PropertyAliases.BodyText] = "The forest green edition of our everyday daypack."
            }
        );
        Node(
            "Trail daypack Sand",
            pack.Id,
            DocumentTypeAliases.ProductVariant,
            new Dictionary<string, string>
            {
                [PropertyAliases.Sku] = "U19-SMOKE-PACK-SAND",
                [PropertyAliases.Color] = "Sand",
                [PropertyAliases.BodyText] = "A warm sand finish, made for all seasons."
            }
        );
        Node(
            "Trail flask",
            category.Id,
            DocumentTypeAliases.Product,
            new Dictionary<string, string>
            {
                [PropertyAliases.Sku] = "U19-SMOKE-FLASK",
                [PropertyAliases.ListPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 299m),
                    ("EUR", 39.95m),
                    ("USD", 44.95m)),
                [PropertyAliases.SalesPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 249m),
                    ("EUR", 34.95m),
                    ("USD", 36.95m)),
                [PropertyAliases.BodyText] =
                    "Keep your coffee warm and your water cool, from first light to late evening."
            }
        );
        Node(
            "Camp shelter",
            category.Id,
            DocumentTypeAliases.Product,
            new Dictionary<string, string>
            {
                [PropertyAliases.Sku] = "U19-SMOKE-SHELTER",
                [PropertyAliases.ListPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 1999m),
                    ("EUR", 269.95m),
                    ("USD", 289.95m)),
                [PropertyAliases.SalesPrice] = DemoBlockValues.Prices(
                    priceType,
                    ("DKK", 1599m),
                    ("EUR", 214.95m),
                    ("USD", 229.95m)),
                [PropertyAliases.BodyText] =
                    "Simple shelter, thoughtful details. A place to pause on your next adventure."
            }
        );

        IContent article = Node(
            "Finding your trail",
            -1,
            DocumentTypeAliases.Article,
            new Dictionary<string, string>
            {
                [PropertyAliases.BodyText] =
                    "Our field guide to getting outdoors. Choose your route, pack lightly and leave no trace."
            }
        );

        Node(
            "A slower kind of weekend",
            -1,
            DocumentTypeAliases.Article,
            new Dictionary<string, string>
            {
                [PropertyAliases.BodyText] =
                    "Good coffee, a quiet campsite and time to explore. The essentials for a weekend outside."
            }
        );
        Node(
            "Packing for your first camp",
            article.Id,
            DocumentTypeAliases.Guide,
            new Dictionary<string, string>
            {
                [PropertyAliases.BodyText] = "A practical packing checklist from the Fieldwork team."
            }
        );
        IContent localizedRoot;
        using (IScope scope = scopes.CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);

            IContent localized;
            if (DemoLocalization.FindSeededArticle(content.GetRootContent()) is { } existing)
            {
                localized = existing;
            }
            else
            {
                localized = content.Create(
                    "Across the seasons",
                    -1,
                    DocumentTypeAliases.LocalizedArticle
                );
                localized.Key = ContentKeys.LocalizedArticle;
            }

            localizedRoot = localized;

            localized.TemplateId = templateId;
            localized.SetCultureName("Across the seasons", "en-US");
            localized.SetCultureName("Gennem årstiderne", "da-DK");
            localized.SetValue(PropertyAliases.Heading, "Across the seasons", "en-US");
            localized.SetValue(PropertyAliases.Heading, "Gennem årstiderne", "da-DK");
            localized.SetValue(PropertyAliases.BodyText, "Find your seasonal adventure.", "en-US");
            localized.SetValue(PropertyAliases.BodyText, "Find dit næste eventyr.", "da-DK");
            localized.SetValue(PropertyAliases.Score, 10, "en-US");
            localized.SetValue(PropertyAliases.Score, 20, "da-DK");
            localized.SetValue(PropertyAliases.ContentRows, DemoBlockValues.EditorialList(types, true));
            localized.SetValue(PropertyAliases.PageGrid, DemoBlockValues.Grid(types));
            content.Save(localized);

            if (!content.Publish(localized, ["en-US", "da-DK"]).Success)
            {
                throw new InvalidOperationException("Failed publishing both cultures.");
            }

            scope.Complete();
        }

        Attempt<DomainUpdateResult, DomainOperationStatus> domainResult = await domains.UpdateDomainsAsync(
            localizedRoot.Key,
            new DomainsUpdateModel
            {
                DefaultIsoCode = "en-US",
                Domains =
                [
                    new DomainModel
                    {
                        DomainName =
                            configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!.TrimEnd('/') + "/en",
                        IsoCode = "en-US"
                    },
                    new DomainModel
                    {
                        DomainName =
                            configuration["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!.TrimEnd('/') + "/da",
                        IsoCode = "da-DK"
                    }
                ]
            }
        );

        if (!domainResult.Success)
        {
            throw new InvalidOperationException(
                $"Failed configuring localized storefront routes: {domainResult.Status}.");
        }
    }
}
