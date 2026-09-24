using Domain.Model.Prodaja;
using Xunit;

namespace Api.Tests;

public sealed class SaleDimensionAttributionContractTests
{
    [Fact]
    public void SaleSnapshot_IsCapturedOnce_AndSurvivesLaterMasterChanges()
    {
        var line = new ProdajaStavka { IdArtikal = 10 };

        SaleDimensionAttribution.CaptureSaleSnapshot(line, supplierId: 7, shoeTypeId: 3);
        SaleDimensionAttribution.CaptureSaleSnapshot(line, supplierId: 99, shoeTypeId: 88);

        Assert.Equal(7, line.SupplierIdAtSale);
        Assert.Equal(3, line.ShoeTypeIdAtSale);
        Assert.Equal(SaleDimensionAttribution.SaleSnapshot, line.AttributionBasis);
    }

    [Fact]
    public void LegacyBackfill_IsExplicitlyEstimated_AndDoesNotOverwriteExistingBasis()
    {
        var line = new ProdajaStavka { IdArtikal = 10 };

        SaleDimensionAttribution.FreezeCurrentMasterBackfill(line, supplierId: 7, shoeTypeId: null);
        SaleDimensionAttribution.FreezeCurrentMasterBackfill(line, supplierId: 99, shoeTypeId: 4);

        Assert.Equal(7, line.SupplierIdAtSale);
        Assert.Null(line.ShoeTypeIdAtSale);
        Assert.Equal(SaleDimensionAttribution.FrozenCurrentMasterBackfill, line.AttributionBasis);
        Assert.False(SaleDimensionAttribution.IsAuthoritative(line.AttributionBasis));
    }

    [Fact]
    public void UnknownDimensionsRemainExplicitlyUnavailable()
    {
        var line = new ProdajaStavka { IdArtikal = 10 };

        SaleDimensionAttribution.FreezeCurrentMasterBackfill(line, supplierId: null, shoeTypeId: null);

        Assert.Null(line.SupplierIdAtSale);
        Assert.Null(line.ShoeTypeIdAtSale);
        Assert.Equal(SaleDimensionAttribution.FrozenCurrentMasterBackfill, line.AttributionBasis);
        Assert.False(SaleDimensionAttribution.IsAuthoritative(line.AttributionBasis));
    }
}
