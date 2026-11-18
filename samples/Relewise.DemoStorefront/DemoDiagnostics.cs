using Relewise.Client.DataTypes;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

internal static class DemoDiagnostics
{
    internal static void MapDiagnostics(this WebApplication app)
    {
        app.MapPost("/smoke/cycle/{key:guid}/{cycle}", (Guid key, string cycle, IContentService content, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            if (cycle is not ("unpublish-publish" or "unpublish-delete" or "unpublish-publish-delete")) return Results.BadRequest();
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var node = content.GetById(key) ?? throw new InvalidOperationException("Unknown fixture.");
            if (node.ContentType.Alias != "article" || node.Name?.StartsWith("Ordering fixture ", StringComparison.Ordinal) != true) return Results.BadRequest();
            content.Unpublish(node);
            if (node.Published) throw new InvalidOperationException("Fixture unpublication failed.");
            if (cycle.Contains("-publish", StringComparison.Ordinal) && !content.Publish(node, ["*"]).Success)
                throw new InvalidOperationException("Fixture republication failed.");
            if (cycle.EndsWith("-delete", StringComparison.Ordinal)) content.Delete(node);
            scope.Complete();
            return Results.Ok(new { node.Key, cycle });
        });
        app.MapPost("/smoke/remove-fixtures", (Guid[] keys, IContentService content, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            if (keys.Length > 200) return Results.BadRequest();
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            foreach (Guid key in keys)
            {
                var node = content.GetById(key);
                if (node == null) continue;
                if (node.Name?.StartsWith("Batch fixture ", StringComparison.Ordinal) != true) throw new InvalidOperationException("Only batch fixtures may be removed here.");
                content.Delete(node);
            }
            scope.Complete();
            return Results.Ok(new { removed = keys.Length });
        });
        app.MapPost("/smoke/variant/{parentKey:guid}", (Guid parentKey, IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var parent = content.GetById(parentKey) ?? throw new InvalidOperationException("Unknown Product.");
            if (parent.ContentType.Alias != "product") return Results.BadRequest();
            string id = "U19-SMOKE-VARIANT-" + Guid.NewGuid().ToString("N");
            var node = content.Create("Variant fixture", parent.Id, "productVariant");
            node.TemplateId = types.Get("productVariant")!.AllowedTemplates!.Single().Id;
            node.SetValue("sku", id);
            node.SetValue("heading", "Variant fixture");
            node.SetValue("color", "Copper");
            content.Save(node);
            if (!content.Publish(node, ["*"]).Success) throw new InvalidOperationException("Variant fixture publication failed.");
            scope.Complete();
            return Results.Ok(new { node.Key, id });
        });
        app.MapPost("/smoke/articles/{count:int}", (int count, IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            if (count is < 1 or > 200) return Results.BadRequest();
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            string batch = Guid.NewGuid().ToString("N");
            var keys = new List<Guid>();
            for (int i = 0; i < count; i++)
            {
                var node = content.Create($"Batch fixture {batch} {i}", -1, "article");
                node.TemplateId = types.Get("article")!.AllowedTemplates!.Single().Id;
                node.SetValue("heading", $"Batch fixture {i}");
                node.SetValue("bodyText", "Batch publication and rebuild measurement.");
                content.Save(node);
                if (!content.Publish(node, ["*"]).Success) throw new InvalidOperationException("Batch fixture publication failed.");
                keys.Add(node.Key);
            }
            scope.Complete();
            return Results.Ok(new { batch, keys });
        });
        app.MapPost("/smoke/article", (ArticleFixture fixture, IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var node = content.Create(fixture.Name, -1, "article");
            node.TemplateId = types.Get("article")!.AllowedTemplates!.Single().Id;
            node.SetValue("heading", fixture.Name);
            node.SetValue("bodyText", fixture.Text);
            content.Save(node);
            if (!content.Publish(node, ["*"]).Success) throw new InvalidOperationException("Fixture publication failed.");
            scope.Complete();
            return Results.Ok(new { node.Key });
        });
        app.MapPost("/smoke/edit/{key:guid}", (Guid key, PropertyEdit edit, IContentService content, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var node = content.GetById(key) ?? throw new InvalidOperationException("Unknown fixture.");
            node.SetValue(edit.Alias, edit.Value, edit.Culture);
            content.Save(node);
            if (edit.Publish && !content.Publish(node, [edit.Culture ?? "*"]).Success) throw new InvalidOperationException("Fixture publication failed.");
            scope.Complete();
            return Results.Ok(new { node.Key });
        });
        app.MapGet("/smoke/relewise-query", async (string? query, Relewise.Client.Extensions.IRelewiseClientFactory clients, IRelewiseUserLocator users,
            Microsoft.Extensions.Options.IOptions<Umbraco.Cms.Core.Configuration.Models.GlobalSettings> settings) =>
        {
            var request = new Relewise.Client.Requests.Search.ContentSearchRequest(new Relewise.Client.DataTypes.Language("en-US"), null,
                await users.GetUser(), "Demo direct Content search", query, 0, 50);
            request.Filters = new(new Relewise.Client.Requests.Filters.ContentDataFilter("rwUmbracoSite", new Relewise.Client.Requests.Conditions.EqualsCondition(settings.Value.Id)),
                new Relewise.Client.Requests.Filters.ContentDataFilter("rwUmbracoPublished", new Relewise.Client.Requests.Conditions.EqualsCondition(true)));
            var result = await clients.GetClient<Relewise.Client.Search.ISearcher>().SearchAsync(request);
            return Results.Ok(new { result.Hits, ids = result.Results?.Select(content => content.ContentId) });
        });
        app.MapGet("/smoke/examine-query", async (string? query, bool? draft, Umbraco.Cms.Search.Provider.Examine.Services.IExamineSearcher searcher) =>
        {
            string alias = draft == true ? Constants.Search.IndexAliases.DraftContent : Constants.Search.IndexAliases.PublishedContent;
            var result = await searcher.SearchAsync(alias, query);
            return Results.Ok(new { result.Total, documents = result.Documents.Select(document => document.Id) });
        });
        app.MapGet("/smoke/runtime", () => Results.Ok(new
        {
            workingSetBytes = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,
            peakWorkingSetBytes = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64,
            packageVersion = typeof(RelewiseUmbracoConfiguration).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion
        }));
        app.MapGet("/smoke/persistence", (IKeyValueService values) => Results.Ok(new { integrationRecords = values.FindByKeyPrefix("Relewise:")?.Count ?? 0 }));
        app.MapGet("/smoke/pipelines", (Microsoft.Extensions.Options.IOptions<global::Umbraco.Cms.Core.Search.Configuration.IndexOptions> options) =>
            Results.Ok(options.Value.GetContentIndexRegistrations().Select(index => new { index.IndexAlias, indexer = index.Indexer.Name, searcher = index.Searcher.Name, strategy = index.ContentChangeStrategy.Name })));

        app.MapPost("/smoke/admin/{id}/{action}", StorefrontAdmin.Run);
        app.MapPost("/smoke/rebuild", async (IExportContentService exports) => { await exports.ExportAll(new ExportAllContent(false), default); return Results.Ok(new { rebuilt = true }); });
        app.MapPost("/smoke/prepare", DemoSeed.Prepare);
        app.MapPost("/smoke/seed", DemoSeed.Run);
        app.MapPost("/smoke/checks", StorefrontChecks.Run);
        app.MapPost("/smoke/probe", (IContentService content, IContentTypeService types, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            using var scope = scopes.CreateScope(); scope.EagerWriteLock(Constants.Locks.ContentTree);
            var node = content.Create("Lifecycle probe", -1, "product");
            string id = "U19-SMOKE-LIFECYCLE-" + Guid.NewGuid().ToString("N");
            node.TemplateId = types.Get("product")!.AllowedTemplates!.Single().Id;
            node.SetValue("heading", "Lifecycle probe"); node.SetValue("sku", id);
            node.SetValue("listPrice", "100"); node.SetValue("salesPrice", "80"); node.SetValue("stock", 5);
            node.SetValue("bodyText", "Disposable publication lifecycle fixture without category relationships.");
            content.Save(node);
            if (!content.Publish(node, ["*"]).Success) throw new InvalidOperationException("Probe publication failed.");
            scope.Complete(); return Results.Ok(new { node.Key, id });
        });
        app.MapPost("/smoke/id/{key:guid}/{id}", (Guid key, string id, IContentService content, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            if (!id.StartsWith("U19-SMOKE-", StringComparison.Ordinal)) return Results.BadRequest();
            using var scope = scopes.CreateScope(); scope.EagerWriteLock(Constants.Locks.ContentTree);
            var node = content.GetById(key) ?? throw new InvalidOperationException("Unknown smoke node.");
            string property = node.ContentType.Alias switch { "product" or "productVariant" => "sku", "brand" => "brandId", "productCategory" => "categoryId", _ => throw new InvalidOperationException("Unsupported smoke identity.") };
            if (node.GetValue<string>(property)?.StartsWith("U19-SMOKE-", StringComparison.Ordinal) != true) return Results.BadRequest();
            node.SetValue(property, id); content.Save(node);
            if (!content.Publish(node, ["*"]).Success) throw new InvalidOperationException("Smoke identity publication failed.");
            scope.Complete(); return Results.Ok(new { node.Key, id });
        });
        app.MapGet("/smoke/query", async (string provider, string? query, string? culture, string? segment, int? maxSuggestions, ISearcherResolver searchers) =>
        {
            var searcher = searchers.GetSearcher(provider);
            if (searcher == null) return Results.NotFound();
            var result = await searcher.SearchAsync(provider, query, culture: culture, segment: segment, maxSuggestions: maxSuggestions ?? 0);
            return Results.Ok(new { result.Total, documents = result.Documents.Select(document => document.Id) });
        });
        app.MapGet("/smoke/schema", async (IDataTypeService types) => Results.Ok((await types.GetAllAsync()).Select(type => new { type.Name, type.EditorAlias })));
        app.MapGet("/smoke/product/{id}", async (string id, Relewise.Client.Extensions.IRelewiseClientFactory clients) =>
        {
            if (!id.StartsWith("U19-SMOKE-", StringComparison.Ordinal)) return Results.BadRequest();
            var query = new Relewise.Client.Requests.Queries.ProductQuery(id, new Relewise.Client.DataTypes.Language("en-US"), new Currency("DKK"))
            { IncludeDisabledProducts = true, IncludeDisabledVariants = true };
            var response = await clients.GetClient<Relewise.Client.IDataAccessor>().QueryAsync(query);
            return Results.Text(Newtonsoft.Json.JsonConvert.SerializeObject(response), "application/json");
        });
        app.MapGet("/smoke/content/{key:guid}", async (Guid key, Relewise.Client.Extensions.IRelewiseClientFactory clients) =>
        {
            var response = await clients.GetClient<Relewise.Client.IDataAccessor>().QueryAsync(
                new Relewise.Client.Requests.Queries.ContentQuery(key.ToString("D")) { IncludeDisabledContents = true });
            return Results.Text(Newtonsoft.Json.JsonConvert.SerializeObject(response), "application/json");
        });
        app.MapPost("/smoke/state/{key:guid}/{action}", (Guid key, string action, string? culture, IContentService content, Umbraco.Cms.Infrastructure.Scoping.IScopeProvider scopes) =>
        {
            using var scope = scopes.CreateScope();
            scope.EagerWriteLock(Constants.Locks.ContentTree);
            var item = content.GetById(key) ?? throw new InvalidOperationException("Unknown node");
            switch (action)
            {
                case "publish": content.Publish(item, [culture ?? "*"]); break;
                case "unpublish": content.Unpublish(item, culture ?? "*"); break;
                case "trash": content.MoveToRecycleBin(item); break;
                case "delete": content.Delete(item); break;
                default: throw new InvalidOperationException("Unknown smoke action");
            }
            scope.Complete();
            return Results.Ok(new { item.Key, item.Published, item.Trashed });
        });
        app.MapGet("/smoke/identities", (IContentService content, RelewiseUmbracoConfiguration config) =>
            Results.Ok(content.GetRootContent().SelectMany(root => new[] { root }.Concat(content.GetPagedDescendants(root.Id, 0, 1000, out _)))
                .Where(item => new[] { "article", "guide", "localizedArticle", "product", "productVariant", "productCategory", "brand" }.Contains(item.ContentType.Alias))
                .Select(item => new { item.Key, item.Id, item.Name, type = item.ContentType.Alias, canonicalId = config.GetContentId(item), item.Published })));
    }
}

internal sealed record ArticleFixture(string Name, string Text);
internal sealed record PropertyEdit(string Alias, string? Value, bool Publish = true, string? Culture = null);
