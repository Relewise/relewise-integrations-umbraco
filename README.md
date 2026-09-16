# Relewise.Integrations.Umbraco [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE) [![NuGet version](https://img.shields.io/nuget/v/Relewise.Integrations.Umbraco)](https://www.nuget.org/packages/Relewise.Integrations.Umbraco) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://https://github.com/Relewise/relewise-sdk-csharp-extensions/pulls)

## Documentation

Start with [the documentation overview](docs/README.md) to choose between automatic mapping, a custom content mapper, and a property converter.

- [Custom content mapping](docs/custom-content-mapping.md): the mapping pipeline, `UseMapper(...)`, registration, field ownership, and cultures.
- [Block List, Block Grid, and nested content](docs/block-editors.md): default support, traversal, rich text, visibility, and referenced content.
- [Complete mapper example](docs/examples/AdvancedContentMapping/README.md): buildable C# code you can adapt to your content model.
- [Validation and troubleshooting](docs/validation-and-troubleshooting.md): export checks, missing fields, duplicate keys, and stale data.


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

`BlogMapper` above represents your own `IContentTypeMapping` implementation; it is not included in the package. See [how to write and register a mapper](docs/custom-content-mapping.md#write-a-minimal-mapper), or use the [complete Block List and Block Grid example](docs/examples/AdvancedContentMapping/README.md).

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

## 14-day Free Trial

You can get access to a [14-day free trial of Relewise](https://www.relewise.com/free-trial) to get you started with Relewise.

## Resources

Find more information on the [Umbraco Marketplace](https://marketplace.umbraco.com/package/relewise.integrations.umbraco).

Documentation can be found at https://docs.relewise.com.

**Please don't hesitate to reach out to us - www.relewise.com - if you'd like to know more, including how to gain access to our API.**

## Contributing

Pull requests are always welcome.  
Please fork this repository and make a PR when you are ready with your contribution.  

Otherwise you are welcome to open an Issue in our [issue tracker](https://github.com/Relewise/relewise-integrations-umbraco/issues).

## License

Relewise.Integrations.Umbraco is [MIT licensed](./LICENSE).
