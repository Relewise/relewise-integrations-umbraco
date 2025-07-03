using Relewise.Client.DataTypes;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
builder.Services.AddSingleton<IRelewiseUserLocator, RelewiseUserLocator>();
// Add services to the container.
builder.Services.AddUmbraco(builder.Environment, builder.Configuration)
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("contentPage", contentType => contentType.AutoMap()))
    .Build();

var app = builder.Build();

await app.BootUmbracoAsync();

// Configure the HTTP request pipeline.
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

app.Run();


public class RelewiseUserLocator : IRelewiseUserLocator
{
    public Task<User> GetUser()
    {
        return Task.FromResult(User.Anonymous());
    }
}