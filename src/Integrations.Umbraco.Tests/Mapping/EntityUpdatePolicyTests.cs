using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;
using Content = Relewise.Client.DataTypes.Content;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class EntityUpdatePolicyTests
{
    public static TheoryData<Type, bool, bool, bool> EntityOperations
    {
        get
        {
            var cases = new TheoryData<Type, bool, bool, bool>();

            Type[] types =
                [typeof(Content), typeof(Product), typeof(Brand), typeof(ProductCategory), typeof(ContentCategory)];

            foreach (Type type in types)
            {
                foreach (bool delta in new[] { false, true })
                {
                    foreach (bool rebuild in new[] { false, true })
                    {
                        foreach (bool full in new[] { false, true })
                        {
                            cases.Add(type, delta, rebuild, full);
                        }
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(EntityOperations))]
    public void ConfiguredOperationsSelectTheSdkUpdateKind(Type type, bool delta, bool rebuild, bool full)
    {
        EntityUpdatePolicy policy = delta || rebuild
            ? EntityUpdatePolicy.ClearAndReplace(options =>
            {
                options.DeltaUpdates = delta;
                options.FullRebuilds = rebuild;
            })
            : EntityUpdatePolicy.ReplaceProvidedProperties();
        var entity = new MappedEntity(Guid.NewGuid(), "source", "id", EntityMapper.Create(type, "id"), true)
        { ClearAndReplace = policy.Clears(full) };

        object kind = entity.Update() switch
        {
            ContentUpdate update => update.Kind,
            ProductUpdate update => update.ProductUpdateKind,
            BrandUpdate update => update.Kind,
            ProductCategoryUpdate update => update.Kind,
            ContentCategoryUpdate update => update.Kind,
            _ => throw new InvalidOperationException("Unexpected update type.")
        };

        Assert.Equal((full ? rebuild : delta) ? "ClearAndReplace" : "ReplaceProvidedProperties", kind.ToString());
    }

    [Fact]
    public void DefaultMappingPreservesBothFlowsAndFactoryOptionsAreSnapshotted()
    {
        using ServiceProvider services = MappingTestServices.Create(options => options.MapProduct("product"));
        EntityUpdatePolicy policy = services.GetRequiredService<RelewiseUmbracoConfiguration>()
            .Mappings["product"].UpdatePolicy;
        Assert.False(policy.Clears(false));
        Assert.False(policy.Clears(true));
        Assert.True(EntityUpdatePolicy.ClearAndReplace().Clears(false));
        Assert.True(EntityUpdatePolicy.ClearAndReplace().Clears(true));

        EntityUpdatePolicyOptions? retained = null;
        EntityUpdatePolicy snapshot = EntityUpdatePolicy.ClearAndReplace(options => retained = options);
        retained!.DeltaUpdates = false;
        retained.FullRebuilds = false;
        Assert.True(snapshot.Clears(false));
        Assert.True(snapshot.Clears(true));
    }

    [Fact]
    public void EmptyClearPolicyAndNullMappingPolicyAreRejected()
    {
        Assert.Throws<ArgumentException>(() => EntityUpdatePolicy.ClearAndReplace(options =>
        {
            options.DeltaUpdates = false;
            options.FullRebuilds = false;
        }));
        Assert.Throws<ArgumentNullException>(() =>
            new RelewiseUmbracoOptionsBuilder().MapProduct("product", mapping => mapping.UpdatePolicy = null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VariantFieldPolicyIsIndependentOfProductPolicy(bool clearProduct)
    {
        var product = new MappedEntity(Guid.NewGuid(), "product", "id", new Product("id"), true)
        { ClearAndReplace = clearProduct, ReplaceExistingVariants = true };
        product.Variants.Add(new MappedEntity(Guid.NewGuid(), "variant", "v1", new ProductVariant("v1"), true)
        { ClearAndReplace = !clearProduct });

        var update = Assert.IsType<ProductUpdate>(product.Update());

        Assert.Equal(clearProduct ? ProductUpdate.UpdateKind.ClearAndReplace :
            ProductUpdate.UpdateKind.ReplaceProvidedProperties, update.ProductUpdateKind);
        Assert.Equal(clearProduct ? ProductUpdate.UpdateKind.ReplaceProvidedProperties :
            ProductUpdate.UpdateKind.ClearAndReplace, update.VariantUpdateKind);
        Assert.True(update.ReplaceExistingVariants);
        Assert.Equal(BrandUpdate.UpdateKind.None, update.BrandUpdateKind);
    }

    [Theory]
    [InlineData(false, ContentUpdate.UpdateKind.UpdateAndAppend)]
    [InlineData(false, ContentUpdate.UpdateKind.ReplaceProvidedProperties)]
    [InlineData(false, ContentUpdate.UpdateKind.ClearAndReplace)]
    [InlineData(true, ContentUpdate.UpdateKind.UpdateAndAppend)]
    [InlineData(true, ContentUpdate.UpdateKind.ReplaceProvidedProperties)]
    [InlineData(true, ContentUpdate.UpdateKind.ClearAndReplace)]
    public void ExplicitContentKindsAndLegacyMapperKindsRetainPrecedence(
        bool clearPolicy,
        ContentUpdate.UpdateKind legacyKind)
    {
        var mapped = new MappedEntity(Guid.NewGuid(), "article", "id", new Content("id"), true)
        { ClearAndReplace = clearPolicy, ContentUpdateKind = legacyKind };

        Assert.Equal(legacyKind, Assert.IsType<ContentUpdate>(mapped.Update()).Kind);
        Assert.Equal(ContentUpdate.UpdateKind.ReplaceProvidedProperties,
            Assert.IsType<ContentUpdate>(mapped.Update(ContentUpdate.UpdateKind.ReplaceProvidedProperties)).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearPolicyCannotChangeExternallyControlledEntitiesOrVariants(bool variant)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ServiceProvider _ = MappingTestServices.Create(options => options.MapProduct("product", mapping =>
            {
                mapping.AdministrativeActions = AdministrativeActionPolicy.None();

                if (variant)
                {
                    mapping.VariantsFromChildren(
                        "variant",
                        child => child.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace());
                }
                else
                {
                    mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace(policy => policy.DeltaUpdates = false);
                }
            }));
        });
    }

    [Fact]
    public void RuntimeGuardAlsoProtectsExternallyControlledVariants()
    {
        var product = new MappedEntity(Guid.NewGuid(), "product", "id", new Product("id"), true);
        product.Variants.Add(new MappedEntity(Guid.NewGuid(), "variant", "v1", new ProductVariant("v1"), false)
        { ClearAndReplace = true });

        Assert.Throws<InvalidOperationException>(() => product.Update());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void VariantLifecycleControlsCollectionRemovalEvenWhenNoPublishedChildrenRemain(
        bool clearProduct,
        bool synchronizeVariants,
        bool hasPublishedChild)
    {
        using ServiceProvider services = MappingTestServices.Create(options => options.MapProduct("product", mapping =>
        {
            if (clearProduct)
            {
                mapping.UpdatePolicy = EntityUpdatePolicy.ClearAndReplace();
            }

            mapping.VariantsFromChildren("variant", child => child.AdministrativeActions = synchronizeVariants
                ? AdministrativeActionPolicy.Synchronize()
                : AdministrativeActionPolicy.None());
        }));
        MappingDefinition definition = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["product"];
        var product = new MappedEntity(Guid.NewGuid(), "product", "id", new Product("id"), true)
        {
            ClearAndReplace = definition.UpdatePolicy.Clears(false),
            ReplaceExistingVariants = definition.ReplaceExistingVariants
        };

        if (hasPublishedChild)
        {
            product.Variants.Add(new MappedEntity(Guid.NewGuid(), "variant", "v1", new ProductVariant("v1"),
                synchronizeVariants));
        }

        var update = Assert.IsType<ProductUpdate>(product.Update());

        Assert.Equal(synchronizeVariants, update.ReplaceExistingVariants);
        Assert.Equal(ProductUpdate.UpdateKind.ReplaceProvidedProperties, update.VariantUpdateKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentNonePreservesExternalVariantCollectionDuringBothExportFlows(bool fullRebuild)
    {
        using ServiceProvider services = MappingTestServices.Create(options => options.MapProduct("product", mapping =>
        {
            mapping.AdministrativeActions = AdministrativeActionPolicy.None();
            mapping.VariantsFromChildren("variant");
        }));
        MappingDefinition definition = services.GetRequiredService<RelewiseUmbracoConfiguration>().Mappings["product"];
        var product = new MappedEntity(Guid.NewGuid(), "product", "id", new Product("id"), false)
        {
            ClearAndReplace = definition.UpdatePolicy.Clears(fullRebuild),
            ReplaceExistingVariants = definition.ReplaceExistingVariants
        };

        var update = Assert.IsType<ProductUpdate>(product.Update());

        Assert.False(update.ReplaceExistingVariants);
        Assert.Equal(ProductUpdate.UpdateKind.ReplaceProvidedProperties, update.ProductUpdateKind);
    }
}
