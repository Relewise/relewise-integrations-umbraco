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
builder.Services.AddScoped<PriceConverter>();
builder.Services.AddScoped<IRelewiseUserLocator, StorefrontUserLocator>();
var umbraco = builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddDeliveryApi().AddComposers();
if (connected)
{
    builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
    builder.Services.AddAsyncValueConverter<ImageUrlConverter>();
    umbraco.AddRelewise(options => DemoConfiguration.Configure(options, builder.Configuration, true));
}

bool diagnostics = builder.Configuration.GetValue<bool>("Demo:EnableDiagnostics");
if (connected && diagnostics)
{
    builder.Services.AddSingleton<StorefrontTraffic>();
    builder.Services.AddSingleton(services => new HttpClient(services.GetRequiredService<StorefrontTraffic>()) { Timeout = TimeSpan.FromMinutes(3) });
}
umbraco.Build();
var app = builder.Build();
await app.BootUmbracoAsync();
bool seeded = await DemoSeed.InitializeAsync(app.Services);
if (connected && !seeded)
{
    // An existing demo database may be connected to Relewise for the first time.
    app.Lifetime.ApplicationStarted.Register(() => app.Services.GetRequiredService<Umbraco.Cms.Core.HostedServices.IBackgroundTaskQueue>()
        .QueueBackgroundWorkItem(async token => await app.Services.GetRequiredService<Relewise.Integrations.Umbraco.Services.IExportContentService>()
            .ExportAll(new Relewise.Integrations.Umbraco.Services.ExportAllContent(false), token)));
}
app.Use(async (context, next) =>
{
    if (!context.Request.Cookies.ContainsKey("storefront-visitor"))
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
    if (connected)
    {
        umbraco.TrackEntityViews();
    }
}).WithEndpoints(umbraco =>
{
    umbraco.UseBackOfficeEndpoints();
    umbraco.UseWebsiteEndpoints();
});
app.MapGet("/", () => Results.Redirect("/store"));
app.MapStorefront();
if (connected && diagnostics)
{
    StorefrontTraffic.Configure(app);
    app.MapDiagnostics();
}
await app.RunAsync();
