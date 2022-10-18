using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.PropertyValueConverters;
using Relewise.Umbraco.Application;
using Relewise.Umbraco.Application.Api;
using Relewise.Umbraco.Application.Infrastructure.CookieConsent;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Relewise.UmbracoV9;

public class Startup
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup" /> class.
    /// </summary>
    /// <param name="webHostEnvironment">The web hosting environment.</param>
    /// <param name="config">The configuration.</param>
    /// <remarks>
    /// Only a few services are possible to be injected here https://github.com/dotnet/aspnetcore/issues/9337
    /// </remarks>
    public Startup(IWebHostEnvironment webHostEnvironment, IConfiguration config)
    {
        _env = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));

        var builder = new ConfigurationBuilder()
            .SetBasePath(webHostEnvironment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{webHostEnvironment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        _config = builder.Build();
    }

    /// <summary>
    /// Configures the services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <remarks>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    /// </remarks>
    public void ConfigureServices(IServiceCollection services)
    {
        // This setups the needed configuration for you to be able to interact with our API.
        // You need to add you own dataset id and api-key in the appsettings before recommendations and search works
        //services.AddRelewise(options => options.ReadFromConfiguration(_config));

        services.AddHttpContextAccessor();
        services.AddSingleton<CookieConsent>();
        services.AddSingleton<IRelewiseUserLocator, RelewiseUserLocator>();

        services.AddValueConverter<RichTextEditorPropertyValueConverter>();
        services.AddValueConverter<ImageCropperValueConverter>();
        services.AddValueConverter<MediaPickerValueConverter>();
        services.AddValueConverter<TextAreaPropertyValueConverter>();

        services.AddUmbraco(_env, _config)
            .AddBackOffice()
            .AddWebsite()
            .AddComposers()
            .AddRelewise(options => options
                .AddContentType("landingPage", contentType => contentType.AutoMap())
                .AddContentType("blogList", contentType => contentType.UseMapper(new BlogMapper()))
                .AddContentType("contentPage", contentType => contentType.AutoMap())
                .AddContentType("blogEntry", contentType => contentType.AutoMap()))
            .Build();
    }

    /// <summary>
    /// Configures the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="env">The web hosting environment.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseEndpoints(c => c
            .MapContentRoutes()
            .MapCatalogRoutes()
            .MapSearchRoutes()
            .MapNewsletterRoutes());

        app.UseRewriter(new RewriteOptions().AddRewrite(
            "product/(\\d*)$",
            "product?productId=$1",
            skipRemainingRules: true));

        app.UseUmbraco()
            .WithMiddleware(u =>
            {
                u.UseBackOffice();
                u.UseWebsite();
                // Enables tracking of all page-views to Relewise
                u.TrackContentViews();
            })
            .WithEndpoints(u =>
            {
                u.UseInstallerEndpoints();
                u.UseBackOfficeEndpoints();
                u.UseWebsiteEndpoints();
            });
    }
}