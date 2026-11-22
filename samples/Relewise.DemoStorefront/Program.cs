using Relewise.Client.Extensions.DependencyInjection;
using Relewise.DemoStorefront.Features.Browsing;
using Relewise.DemoStorefront.Features.Cart;
using Relewise.DemoStorefront.Features.Recommendations;
using Relewise.DemoStorefront.Integration;
using Relewise.DemoStorefront.Integration.Converters;
using Relewise.DemoStorefront.Integration.Indexing;
using Relewise.DemoStorefront.Integration.Tracking;
using Relewise.DemoStorefront.Seeding;
using Relewise.DemoStorefront.Setup;
using Relewise.DemoStorefront.Site;
using Relewise.Integrations.Umbraco;

namespace Relewise.DemoStorefront;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile("local.settings.json", true, false);
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

        var connected = DemoSetup.HasRelewiseConfiguration(builder.Configuration);

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
        builder.Services.AddScoped(services =>
            DemoMarket.Current(services.GetRequiredService<IHttpContextAccessor>().HttpContext));
        builder.Services.AddScoped<IRelewiseUserLocator, StorefrontUserLocator>();

        IUmbracoBuilder umbraco = builder
            .CreateUmbracoBuilder()
            .AddBackOffice()
            .AddWebsite()
            .AddDeliveryApi()
            .AddComposers();

        if (connected)
        {
            builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));

            builder.Services.AddAsyncValueConverter<ImageUrlConverter>();

            umbraco.AddRelewise(DemoConfiguration.Configure);
        }

        builder.Services.AddTransient<Umbraco.Cms.Core.Search.Indexing.IContentIndexer, StorefrontProductIndexer>();

        umbraco.Build();

        WebApplication app = builder.Build();

        await app.BootUmbracoAsync();
        await DemoSeed.InitializeAsync(app.Services);

        if (connected)
        {
            // Export the completed tree after seeding and migrations, including on a fresh installation.
            app.Lifetime.ApplicationStarted.Register(() => app.Services
                .GetRequiredService<Umbraco.Cms.Core.HostedServices.IBackgroundTaskQueue>()
                .QueueBackgroundWorkItem(async token => await app.Services
                    .GetRequiredService<Relewise.Integrations.Umbraco.Services.IExportContentService>()
                    .ExportAll(new Relewise.Integrations.Umbraco.Services.ExportAllContent(false), token)));
        }

        app.UseSession();

        app.Use(async (context, next) =>
        {
            if (connected && !context.Request.Cookies.ContainsKey("storefront-visitor"))
            {
                var id = Guid.NewGuid()
                    .ToString("N");

                context.Items["storefront-visitor"] = id;
                context.Response.Cookies.Append(
                    "storefront-visitor",
                    id,
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax }
                );
            }

            await next(context);
        });

        app.UseUmbraco()
            .WithMiddleware(context =>
            {
                context.UseBackOffice();
                context.UseWebsite();

                app.Use(DemoConnection.InvokeAsync);

                if (connected)
                {
                    context.TrackEntityViews();
                }
            })
            .WithEndpoints(context =>
            {
                context.UseBackOfficeEndpoints();
                context.UseWebsiteEndpoints();
            });

        app.MapGet("/", (HttpContext http) => Results.Redirect(DemoMarket.Current(http).HomeUrl));
        app.MapMarket();
        app.MapBrowsing();
        app.MapCart();
        app.MapRecommendations();

        await app.RunAsync();
    }
}
