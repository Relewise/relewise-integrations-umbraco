using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class AdministrativeActionPolicyTests
{
    [Fact]
    public void SupplementPreservesUnspecifiedFieldsAndCannotClearAndReplace()
    {
        var product = Assert.IsType<Product>(EntityMapper.Create(typeof(Product), "sku"));

        Assert.Null(product.Assortments);
        Assert.Null(product.CategoryPaths);
        Assert.Null(product.ListPrice);

        var mapped = new MappedEntity(Guid.NewGuid(), "product", "sku", product, false);
        var update = Assert.IsType<ProductUpdate>(mapped.Update());

        Assert.False(update.ReplaceExistingVariants);
        Assert.Throws<InvalidOperationException>(() => mapped.Update(ContentUpdate.UpdateKind.ClearAndReplace));
    }
}
