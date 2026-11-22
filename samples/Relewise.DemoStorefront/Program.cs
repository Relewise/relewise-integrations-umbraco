using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("This example must run in Development.");
}

bool connected = DemoSetup.HasRelewiseConfiguration(builder.Configuration);
DemoSetup.ConfigureLocalInstallation(builder);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "fieldwork-cart";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddAntiforgery();
builder.Services.AddScoped<PriceConverter>();
builder.Services.AddScoped(services => DemoMarket.Current(services.GetRequiredService<IHttpContextAccessor>().HttpContext));
builder.Services.AddScoped<IRelewiseUserLocator, StorefrontUserLocator>();
var umbraco = builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddDeliveryApi().AddComposers();
if (connected)
{
    builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
    builder.Services.AddAsyncValueConverter<ImageUrlConverter>();
    umbraco.AddRelewise(options => DemoConfiguration.Configure(options, builder.Configuration, true));
}

builder.Services.AddTransient<Umbraco.Cms.Core.Search.Indexing.IContentIndexer, StorefrontProductIndexer>();
umbraco.Build();
var app = builder.Build();
await app.BootUmbracoAsync();
await DemoSeed.InitializeAsync(app.Services);
if (connected)
{
    // Export the completed tree after seeding and migrations, including on a fresh installation.
    app.Lifetime.ApplicationStarted.Register(() => app.Services.GetRequiredService<Umbraco.Cms.Core.HostedServices.IBackgroundTaskQueue>()
        .QueueBackgroundWorkItem(async token => await app.Services.GetRequiredService<Relewise.Integrations.Umbraco.Services.IExportContentService>()
            .ExportAll(new Relewise.Integrations.Umbraco.Services.ExportAllContent(false), token)));
}
app.UseSession();
app.Use(async (context, next) =>
{
    if (connected && !context.Request.Cookies.ContainsKey("storefront-visitor"))
    {
        string id = Guid.NewGuid().ToString("N");
        context.Items["storefront-visitor"] = id;
        context.Response.Cookies.Append("storefront-visitor", id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
    }
    await next(context);
});
app.UseUmbraco().WithMiddleware(umbraco =>
{
    umbraco.UseBackOffice();
    umbraco.UseWebsite();
    app.Use(DemoConnection.InvokeAsync);
    if (connected)
    {
        umbraco.TrackEntityViews();
    }
}).WithEndpoints(umbraco =>
{
    umbraco.UseBackOfficeEndpoints();
    umbraco.UseWebsiteEndpoints();
});
app.MapGet("/", (HttpContext http) => Results.Redirect(DemoMarket.Current(http).HomeUrl));
app.MapStorefront();
app.MapCart();
app.MapRecommendations();
await app.RunAsync();
