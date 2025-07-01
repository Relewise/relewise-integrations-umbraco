using Relewise.Integrations.Umbraco;

var builder = WebApplication.CreateBuilder(args);

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
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

app.Run();