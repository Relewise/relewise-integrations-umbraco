namespace Relewise.Integrations.Umbraco.Tests;

public class DemoBrandTests
{
    [Fact]
    public void OriginalSmokeProductsKeepTheirBrandWhileTheCatalogueSpansThreeBrands()
    {
        Assert.Equal("U19-SMOKE-BRAND", DemoCatalogue.BrandIdForSku("U19-SMOKE-PACK"));
        Assert.Equal("U19-SMOKE-BRAND", DemoCatalogue.BrandIdForSku("U19-SMOKE-FLASK"));
        string[] products = ["FIELDWORK-SUMMIT-PACK", "FIELDWORK-RIDGE-PACK", "FIELDWORK-HIP-PACK", "FIELDWORK-CAMP-CHAIR", "FIELDWORK-LANTERN"];
        Assert.Equal(3, products.Select(DemoCatalogue.BrandIdForSku).Distinct().Count());
    }
}
