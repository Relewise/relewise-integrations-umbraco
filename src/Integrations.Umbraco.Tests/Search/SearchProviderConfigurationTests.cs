using Microsoft.Extensions.DependencyInjection;
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
                    options.SearchProviders.AddProduct(
                        "search",
                        provider => provider.UnsupportedSegmentBehavior = null!);
                }
                else
                {
                    options.MapContent("article");
                    options.SearchProviders.AddContent(
                        "search",
                        provider => provider.UnsupportedSegmentBehavior = null!);
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
                options.SearchProviders.AddProduct("products");
            });
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                options.MapContent("article");
                options.SearchProviders.AddContent("articles", provider => provider.DocumentTypeAliases = ["missing"]);
            });
        });

        using ServiceProvider services = MappingTestServices.Create(options =>
        {
            options.MapContent("article");
            options.SearchProviders.AddContent("articles");
        });

        Assert.Null(
            services.GetRequiredService<RelewiseUmbracoConfiguration>()
                .SearchProviders
                .DefaultPublishedContentProviderAlias);
    }
}
