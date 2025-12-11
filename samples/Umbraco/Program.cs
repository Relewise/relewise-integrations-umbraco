using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);


builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("homePage", contentType => contentType.AutoMap())
        .AddContentType("contentPage", contentType => contentType.AutoMap()))
    .Build();

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