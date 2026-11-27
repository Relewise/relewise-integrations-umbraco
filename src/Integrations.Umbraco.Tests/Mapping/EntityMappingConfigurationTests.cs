using Microsoft.Extensions.DependencyInjection;
using Moq;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Umbraco.Cms.Core.Models.PublishedContent;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class EntityMappingConfigurationTests
{
    [Fact]
    public async Task ExportsSourceMetadataWithoutAnInstallationIdentifier()
    {
        using ServiceProvider services = MappingTestServices.Create(options =>
            options.MapBrand("brand", mapping => mapping.AdministrativeActions = AdministrativeActionPolicy.None()));
        var source = new Mock<IPublishedContent>();

        source.SetupGet(value => value.ContentType.Alias).Returns("brand");
        source.SetupGet(value => value.Id).Returns(42);
        source.SetupGet(value => value.Key).Returns(Guid.NewGuid());

        var configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();
        var mapper = new EntityMapper(configuration);

        await using var operation = new MappingOperation(services, CancellationToken.None);
        MappedEntity mapped = await mapper.MapAsync(
            source.Object,
            configuration.Mappings["brand"],
            operation,
            1,
            publishedCultures: ["en-US"]);
        Dictionary<string, DataValue?> data = EntityMapper.Data(mapped.Entity);

        Assert.Equal("brand", data[RelewiseDataKeys.DocumentTypeAlias]!.ValueAs<string>());
        Assert.Equal(source.Object.Key.ToString("D"), data[RelewiseDataKeys.NodeKey]!.ValueAs<string>());
        Assert.DoesNotContain("rwUmbracoSite", data.Keys);
        Assert.DoesNotContain("rwUmbracoExportGeneration", data.Keys);
        Assert.Equal(1d, data["UmbracoVersionId"]!.ValueAs<double>());
    }

    [Fact]
    public void FinalConfigurationRejectsConflictingOrReservedDataKeysAfterRegistration()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                DataMappingOptions? retained = null;

                options.MapProduct("product", mapping => mapping.MapData("field", data => retained = data));
                retained!.DataKey = "rwUmbracoNodeKey";
            });
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options =>
            {
                DataMappingOptions? retained = null;

                options.MapBrand(
                    "brand",
                    mapping =>
                    {
                        mapping.MapData("first");
                        mapping.MapData("second", data => retained = data);
                    });
                retained!.DataKey = "first";
            });
        });
    }

    [Fact]
    public void ContentAllowsDistinctAliasesButOtherEntitiesHaveOneSource()
    {
        var options = new RelewiseUmbracoOptionsBuilder();

        options.MapContent("article");
        options.MapContent("guide");
        Assert.Throws<InvalidOperationException>(() => options.MapContent("ARTICLE"));
        options.MapProduct("product");
        Assert.Throws<InvalidOperationException>(() => options.MapProduct("otherProduct"));
        options.MapBrand("brand");
        Assert.Throws<InvalidOperationException>(() => options.MapBrand("otherBrand"));
        options.MapProductCategory("category");
        Assert.Throws<InvalidOperationException>(() => options.MapProductCategory("otherCategory"));
        options.MapContentCategory("topic");
        Assert.Throws<InvalidOperationException>(() => options.MapContentCategory("otherTopic"));
        Assert.Throws<InvalidOperationException>(() => options.MapContent("product"));
    }

    [Fact]
    public void LegacyRegistrationRetainsMergingAndViewOnlyBehavior()
    {
        using ServiceProvider services = MappingTestServices.Create(options =>
        {
            options.AddContentType("article");
            options.AddContentType("article", type => type.AutoMap());
            options.AddContentType("viewOnly");
        });
        var configuration = services.GetRequiredService<RelewiseUmbracoConfiguration>();

        Assert.True(configuration.CanMap("article"));
        Assert.False(configuration.CanMap("viewOnly"));
        Assert.Throws<ArgumentException>(() =>
            new RelewiseUmbracoOptionsBuilder().AddContentType("article")
                .AddContentType("article", throwIfExists: true));
    }

    [Fact]
    public void DataAliasIsRequiredButDestinationConfigurationIsOptional()
    {
        using ServiceProvider services =
            MappingTestServices.Create(options => options.MapContent("article", mapping => mapping.MapData("hero")));
        MappingDefinition definition = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["article"];

        Assert.Equal("hero", Assert.Single(definition.DataMappings).Options.DataKey);
        Assert.Throws<InvalidOperationException>(() => new RelewiseUmbracoOptionsBuilder().MapContent(
            "article",
            mapping => mapping.MapData("foo", data => data.DataKey = "contentTypeAlias")));
    }

    [Fact]
    public void DuplicateDataChecksUseTheCurrentConfiguration()
    {
        var mapping = new EntityMappingBuilder<Content>();
        DataMappingOptions? first = null;

        mapping.MapData("first", data => first = data);
        first!.DataKey = "second";
        mapping.MapData("first");
        mapping.Definition.Validate();

        Assert.Equal(["second", "first"], mapping.Definition.DataMappings.Select(data => data.Options.DataKey));
        Assert.Throws<InvalidOperationException>(() => mapping.MapData("third", data => data.DataKey = "second"));
    }

    [Fact]
    public void BuiltInMappingRejectsIdentityNestedAndDuplicateDestinations()
    {
        var mapping = new EntityMappingBuilder<Product>();

        Assert.Throws<ArgumentException>(() => mapping.Map(entity => entity.Id, "sku"));
        Assert.Throws<ArgumentException>(() => mapping.Map(entity => entity.Brand!.Id, "brand"));
        mapping.Map(entity => entity.DisplayName, "heading");
        Assert.Throws<InvalidOperationException>(() => mapping.Map(entity => entity.DisplayName, "title"));
    }

    [Fact]
    public void ParentExportAndTrackingSwitchesApplyToChildVariants()
    {
        using ServiceProvider services = MappingTestServices.Create(options => options.MapProduct(
            "product",
            mapping =>
            {
                mapping.Export = false;
                mapping.TrackViews = false;
                mapping.VariantsFromChildren("variant");
            }));
        MappingDefinition variant = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["variant"];

        Assert.False(variant.Export);
        Assert.False(variant.TrackViews);
    }

    [Fact]
    public void DeclarativeFieldsAndCustomMapperCanBeCombinedInEitherRegistrationOrder()
    {
        foreach (bool mapperFirst in new[] { true, false })
        {
            var options = new RelewiseUmbracoOptionsBuilder();

            options.MapContent(
                "article",
                mapping =>
                {
                    if (mapperFirst)
                    {
                        mapping.UseMapper<CustomMapper>();
                    }

                    mapping.Map(entity => entity.DisplayName, "heading");
                    mapping.MapData("body");

                    if (!mapperFirst)
                    {
                        mapping.UseMapper<CustomMapper>();
                    }
                });
        }

        new RelewiseUmbracoOptionsBuilder().MapContent(
            "article",
            mapping =>
            {
                mapping.AutoMap();
                mapping.UseMapper<CustomMapper>();
            });
    }

    public sealed class CustomMapper : IEntityMapper<Content>
    {
        public Task MapAsync(EntityMappingContext<Content> context, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
