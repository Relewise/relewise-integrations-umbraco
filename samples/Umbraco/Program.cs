using Relewise.Client.DataTypes;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));

var umbracoBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("homePage", contentType => contentType.AutoMap())
        .AddContentType("contentPage", contentType => contentType.AutoMap()));

if (builder.Configuration.GetValue<bool>("Relewise:Umbraco:Search:Enabled"))
{
    umbracoBuilder.AddRelewiseSearchProvider(options =>
    {
        options.Scope = builder.Configuration["Relewise:Umbraco:Search:Scope"]
            ?? throw new InvalidOperationException("Configure Relewise:Umbraco:Search:Scope when enabling core search.");
        options.Language = builder.Configuration["Relewise:Umbraco:Search:Language"] ?? "en";
    });
}

umbracoBuilder.Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();

        u.TrackContentViews();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();


public class RelewiseUserLocator : IRelewiseUserLocator
{
    public Task<User> GetUser()
    {
        return Task.FromResult(User.Anonymous());
    }
}
