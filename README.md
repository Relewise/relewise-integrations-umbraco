# Relewise.Integrations.Umbraco [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE) [![NuGet version](https://img.shields.io/nuget/v/Relewise.Integrations.Umbraco)](https://www.nuget.org/packages/Relewise.Integrations.Umbraco) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://https://github.com/Relewise/relewise-sdk-csharp-extensions/pulls)

### Installing Relewise.Integrations.Umbraco 

First make sure to have [Umbraco installed](https://docs.umbraco.com/umbraco-cms/fundamentals/setup/install/install-umbraco-with-templates#install-the-template):
> dotnet new umbraco

Then you can install the `Relewise.Integrations.Umbraco` Package through the .NET CLI by running this command:
> dotnet add package Relewise.Integrations.Umbraco

... or from the NuGet Package Manager Console by running this command:
> Install-Package Relewise.Integrations.Umbraco

### Using Relewise.Integrations.Umbraco

Open `Program.cs` and add Relewise to the `IServiceCollection`-instance: 

```csharp
builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration));
```

... where the above configuration, requires Relewise configuration in `appsettings.json`:

```json
"Relewise": {
  "DatasetId": "insert-dataset-id-here",
  "ApiKey": "insert-api-key-here",
  "ServerUrl": "insert-server-url-here"
}
```

Find more details about this here: https://github.com/Relewise/relewise-sdk-csharp-extensions

To integrate with Umbraco, you need to add Relewise to the UmbracoBuilder (`.AddUmbraco(...)`), and optionally specify which ContentTypes, that you would like exported into Relewise for content search and recommendations. 

In the example below we are exporting four content types into Relewise:
```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddRelewise(options => options
        .AddContentType("landingPage", contentType => contentType.AutoMap())
        .AddContentType("blogList", contentType => contentType.UseMapper(new BlogMapper()))
        .AddContentType("contentPage", contentType => contentType.AutoMap())
        .AddContentType("blogEntry", contentType => contentType.AutoMap()))
    .Build();
```

If you'd also like these content types to be automatically tracked, you can add our middleware to the UmbracoBuilder (`.UseUmbraco(...)`):
```csharp
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackContentViews();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });
```

### Sample site for v9 and v10 - Get it up and running.

The sample site requires .NET 6 and NPM. There is a sample site for both Umbraco v9 and Umbraco v10. 
The sample site share the same frontend client, which is located in the Umbraco v10 sample site folder.
The sample site has demo content, so once it's up and running, you can have a look around. 

 If you wish to setup and run, then just follow the steps below:

1. Start by cloning the repository.
2. Setting up the frontend.
    
    1. Navigate to `samples/UmbracoV10/client` (also if intend to run the v9 sample - see above)
    2. Run `npm install` via a command prompt
    3. Run `npm run serve` via a command prompt

3. Spinning up the site.

    1. Navigate to `samples/UmbracoV9` or `samples/UmbracoV10`, depending on whether you want to sample v9 or v10.
    2. Run `dotnet run` via a command prompt.
    3. Access the website from your browser

4. Installing Umbraco - When visiting the site for the first time, you are asked to configure Umbraco. Just follow the instructions.
5. Publish the content of the site. The site comes with content already created. You just need to login and publish the content

## 14-day Free Trial

You can get access to a [14-day free trial of Relewise](https://www.relewise.com/free-trial) to get you started with Relewise.

## Resources

Find more information on the Umbraco Marketplace: https://umbraco.com/marketplace/relewise/

Documentation can be found at https://docs.relewise.com.

**Please don't hesitate to reach out to us - www.relewise.com - if you'd like to know more, including how to gain access to our API.**

## Contributing

Pull requests are always welcome.  
Please fork this repository and make a PR when you are ready with your contribution.  

Otherwise you are welcome to open an Issue in our [issue tracker](https://github.com/Relewise/relewise-integrations-umbraco/issues).

## License

Relewise.Integrations.Umbraco is [MIT licensed](./LICENSE).
