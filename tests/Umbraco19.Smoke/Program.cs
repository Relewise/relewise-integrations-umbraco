using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

// A disposable local test host. Never deploy this project: its smoke routes deliberately
// expose test operations. The runner binds it to loopback and creates a fresh SQLite database.
var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("This test host requires the Development environment.");
builder.WebHost.UseUrls("http://127.0.0.1:5519");
builder.Configuration.AddJsonFile("local.settings.json", optional: false);
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddDeliveryApi().AddComposers()
    .AddRelewise(options => options.AddContentType("smokePage", type => type.AutoMap()))
    .AddRelewiseSearchProvider(options => options.Scope = "umbraco19-smoke")
    .Build();
var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Smoke:UseJson"))
{
    var factory = app.Services.GetRequiredService<Relewise.Client.Extensions.IRelewiseClientFactory>();
    foreach (string name in factory.ClientNames)
    {
        foreach (var client in new Relewise.Client.IClient[]
        {
            factory.GetClient<Relewise.Client.ITracker>(name),
            factory.GetClient<Relewise.Client.Search.ISearcher>(name)
        })
        {
            ((Relewise.Client.ClientBase)client).UseFastBinarySerializer = false;
            ((Relewise.Client.ClientBase)client).UseJsonCompression = false;
        }
    }
}
await app.BootUmbracoAsync();
app.UseUmbraco().WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); })
    .WithEndpoints(u => { u.UseBackOfficeEndpoints(); u.UseWebsiteEndpoints(); });

app.MapPost("/smoke/seed", async (IContentTypeService types, IContentService content, IShortStringHelper strings) =>
{
    IContentType? type = types.Get("smokePage");
    if (type is null)
    {
        type = new ContentType(strings, -1) { Alias = "smokePage", Name = "Smoke page", AllowedAsRoot = true };
        await types.CreateAsync(type, Constants.Security.SuperUserKey);
    }
    IContent page = content.Create("Alpha smoke", -1, "smokePage");
    content.Save(page);
    var result = content.Publish(page, ["*"]);
    return Results.Ok(new { page.Key, Published = result.Success });
});
app.MapPost("/smoke/provider", async (IIndexerResolver indexers, ISearcherResolver searchers) =>
{
    string alias = Constants.Search.IndexAliases.PublishedContent;
    IIndexer indexer = indexers.GetIndexer(alias) ?? throw new InvalidOperationException("Indexer not resolved");
    ISearcher searcher = searchers.GetSearcher(alias) ?? throw new InvalidOperationException("Searcher not resolved");
    Guid key = Guid.Parse("c74d924e-4385-4b06-bb96-e5e62cd85fa0");
    await indexer.AddOrUpdateAsync(alias, key, UmbracoObjectTypes.Document, [new Variation(null, null)],
        [new IndexField("title", new IndexValue { TextsR1 = ["Alpha provider smoke"] }, null, null)], null);
    var result = await searcher.SearchAsync(alias, "Alpha");
    for (int attempt = 0; result.Total == 0 && attempt < 30; attempt++)
    {
        await Task.Delay(500);
        result = await searcher.SearchAsync(alias, "Alpha");
    }
    var metadata = await indexer.GetMetadataAsync(alias);
    return Results.Ok(new { Provider = indexer.GetType().FullName, result.Total, Documents = result.Documents.Select(d => d.Id), metadata });
});
app.MapGet("/smoke/version", () => Results.Ok(new
{
    Package = typeof(RelewiseUmbracoConfiguration).Assembly.GetName().Version!.ToString(),
    Umbraco = typeof(IContentService).Assembly.GetName().Version!.ToString()
}));
app.MapPost("/smoke/verify", (Relewise.Integrations.Umbraco.Search.RelewiseSearchProvider provider,
    Relewise.Client.Extensions.IRelewiseClientFactory clients) => LiveSearchChecks.Run(provider, clients));
await app.RunAsync();
