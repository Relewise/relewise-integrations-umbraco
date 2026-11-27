using Microsoft.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco.Search;
using Relewise.Integrations.Umbraco.Tests.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Search;

public class SearchProviderConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AProviderMustHaveAnExplicitUnsupportedSegmentPolicy(bool product)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                if (product)
                {
                    options.MapProduct("product");
                    options.ConfigureSearch(search =>
                    {
                        search.AddProductProvider(
                            "search",
                            provider => provider.UnsupportedSegmentBehavior = null!);
                    });
                }
                else
                {
                    options.MapContent("article");
                    options.ConfigureSearch(search =>
                    {
                        search.AddContentProvider(
                            "search",
                            provider => provider.UnsupportedSegmentBehavior = null!);
                    });
                }
            });
        });
    }

    [Fact]
    public void ProvidersValidateEntityTypeAndContentAliasSelection()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddProductProvider("products");
                });
            });
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                options.MapContent("article");
                options.ConfigureSearch(search =>
                {
                    search.AddContentProvider("articles", provider => provider.DocumentTypeAliases = ["missing"]);
                });
            });
        });

        using ServiceProvider services = MappingTestServices.Create(options =>
        {
            options.MapContent("article");
            options.ConfigureSearch(search =>
            {
                search.AddContentProvider("articles");
            });
        });

        Assert.Null(
            services.GetRequiredService<RelewiseUmbracoConfiguration>()
                .GetSearchOptions()
                .DefaultPublishedContentProviderAlias);
    }
}
