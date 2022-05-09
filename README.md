# Relewise.Integrations.Umbraco

### Installing Relewise.Integrations.Umbraco

You should install Relewise.Integrations.Umbraco with NuGet:

> Install-Package Relewise.Integrations.Umbraco

Run this command from the NuGet Package Manager Console to install the NuGet package.

### Using Relewise.Integrations.Umbraco

Add Relewise to the UmbracoBuilder, and specify the ContentTypes, that you would like exported into Relewise for content search and recommendation.
In this example, we are exporting all LandingPages and ContentPages to Relewise.
```csharp
services.AddUmbraco(_env, _config)
                .AddRelewise(options => options.UseMapping(map => map.AutoMapping("LandingPage", "ContentPage")))
```

## Contributing

Pull requests are always welcome.  
Please fork this repository and make a PR when you are ready with your contribution.  

Otherwise you are welcome to open an Issue in our [issue tracker](https://github.com/Relewise/relewise-integrations-umbraco/issues).

## License

Relewise.Integrations.Umbraco is under the [MIT licensed](./LICENSE)
