using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaActivityEvidencePolicyTests
{
    [Fact]
    public void SparseCompleteWindowIsActivityNotDataCoverage()
    {
        var rate = VendorSalesNivelacijaActivityEvidencePolicy.ProjectActivityRatePct(2m / 30m, observationWindowMature: true);

        Assert.Equal(6.67m, rate);
        Assert.Equal(2, VendorSalesNivelacijaActivityEvidencePolicy.ProjectActiveDays(rate));
        Assert.Equal("unavailable", VendorSalesNivelacijaActivityEvidencePolicy.DataCoverageStatus);
    }

    [Fact]
    public void DenseWindowProjectsFullActivity()
    {
        var rate = VendorSalesNivelacijaActivityEvidencePolicy.ProjectActivityRatePct(1m, observationWindowMature: true);

        Assert.Equal(100m, rate);
        Assert.Equal(30, VendorSalesNivelacijaActivityEvidencePolicy.ProjectActiveDays(rate));
    }

    [Fact]
    public void MatureZeroSalesIsDistinctFromImmatureUnknown()
    {
        Assert.Equal(0m, VendorSalesNivelacijaActivityEvidencePolicy.ProjectActivityRatePct(null, observationWindowMature: true));
        Assert.Null(VendorSalesNivelacijaActivityEvidencePolicy.ProjectActivityRatePct(null, observationWindowMature: false));
        Assert.Null(VendorSalesNivelacijaActivityEvidencePolicy.ProjectActiveDays(null));
    }
}
