using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Relewise.Client.DataTypes;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Infrastructure.Extensions;
using Relewise.UmbracoV9.Application;
using Relewise.UmbracoV9.Application.Api;
using Relewise.UmbracoV9.Application.Infrastructure.CookieConsent;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Relewise.UmbracoV9
{
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
            services.AddRelewise(options => options.ReadFromConfiguration(_config));

            services.AddHttpContextAccessor();
            services.AddSingleton<CookieConsent>();
            services.AddSingleton<IRelewiseUserLocator, RelewiseUserLocator>();

#pragma warning disable IDE0022 // Use expression body for methods
            services.AddUmbraco(_env, _config)
                .AddBackOffice()
                .AddWebsite()
                .AddComposers()
                .AddRelewise(options => options
                    .Add("LandingPage", typeX => typeX.AutoMap())
                    .Add("Site")
                    .Add("Blog", blog => blog.UseMapper(new BlogMapper()))
                    .Add("ContentPage", typeZ => typeZ.AutoMap())
                    .Add("BlogEntry", typeZ => typeZ.AutoMap()))
                .Build();
#pragma warning restore IDE0022 // Use expression body for methods
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
                .MapNewsletterRoutes());

            app.UseUmbraco()
                .WithMiddleware(u =>
                {
                    u.UseBackOffice();
                    u.UseWebsite();
                    // Enables tracking of all pageviews to Relewise
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

    public class BlogMapper : IContentTypeMapping
    {
        public Task<ContentUpdate> Map(ContentMappingContext context)
        {
            context.ContentUpdate.Content.Data["Title"] = context.PublishedContent.GetProperty("title").GetValue<string>(context.CulturesToPublish.First());

            return Task.FromResult(context.ContentUpdate);
        }
    }
}