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
    .AddRelewise(options =>
    {
        options.MapContent("homePage", mapping => mapping.AutoMap());
        options.MapContent("contentPage", mapping => mapping.AutoMap());
        if (builder.Configuration.GetValue<bool>("Relewise:Umbraco:Search:Enabled"))
        {
            options.SearchProviders.AddContent("site");
            options.SearchProviders.DefaultPublishedContentProviderAlias = "site";
        }
    });

umbracoBuilder.Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();

        u.TrackEntityViews();
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
