using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class VendorSalesNivelacijaOutcomeLedgerContractTests
{
    [Fact]
    public void LedgerAcceptsOnlyMatureNonOverlappingMarkdowns()
    {
        var source = ReadRepoFile("Api/Services/VendorSalesNivelacijaOutcomeLedgerService.cs");

        Assert.Contains("e.price_direction = 'markdown'", source);
        Assert.Contains("e.post_window_complete = TRUE", source);
        Assert.Contains("e.overlaps_next_event = FALSE", source);
        Assert.Contains("e.same_day_event_count = 1", source);
        Assert.Contains("@data_scope = 'all'", source);
        Assert.Contains("e.store_id = @store_id", source);
    }

    [Fact]
    public void MissingHistoricalWindowsAndStockRemainUnavailable()
    {
        Assert.Equal(typeof(int?), typeof(VendorSalesNivelacijaOutcomeEventDto).GetProperty(nameof(VendorSalesNivelacijaOutcomeEventDto.StockAtEvent))!.PropertyType);
        Assert.Equal(typeof(decimal?), typeof(VendorSalesNivelacijaOutcomeEventDto).GetProperty(nameof(VendorSalesNivelacijaOutcomeEventDto.SellThroughPct))!.PropertyType);
        Assert.Equal(typeof(decimal?), typeof(VendorSalesNivelacijaOutcomeEventDto).GetProperty(nameof(VendorSalesNivelacijaOutcomeEventDto.PreRevenue))!.PropertyType);

        var source = ReadRepoFile("Api/Services/VendorSalesNivelacijaOutcomeLedgerService.cs");
        var policy = ReadRepoFile("Api/Services/VendorSalesNivelacijaOutcomeEvidencePolicy.cs");
        Assert.Contains("historical_stock_unavailable", ReadRepoFile("Api/Models/VendorSalesNivelacijaModels.cs"));
        Assert.Contains("emptyWindowIsZero", policy);
        Assert.Contains("new VendorSalesNivelacijaOutcomePeriodEvidence(null, null, null, null)", policy);
        Assert.Contains("allCovered ? margin : null", policy);
        Assert.Contains("AnalyticsMarginPolicy.ResolveUnitCostWithSnapshot(", policy);
    }

    [Fact]
    public void MatureMarkdownFixtureMatchesIndependentSaleTimeCostOracle()
    {
        var preLines = new[]
        {
            new VendorSalesNivelacijaOutcomeSaleLineFact(1, 2, 100m, 40m, 70m, 65m),
            new VendorSalesNivelacijaOutcomeSaleLineFact(2, 1, 90m, null, null, null)
        };
        var pre = VendorSalesNivelacijaOutcomeEvidencePolicy.CalculatePeriod(
            preLines,
            new Dictionary<int, decimal?> { [2] = 55m },
            emptyWindowIsZero: false);

        // Independent raw fixture oracle: revenue=290, units=3,
        // cost=(2*40)+(1*55)=135 and contribution=155 RSD.
        Assert.Equal(3m, pre.Units);
        Assert.Equal(290m, pre.Revenue);
        Assert.Equal(100m, pre.CostCoveragePct);
        Assert.Equal(155m, pre.MarginContribution);

        var uncovered = VendorSalesNivelacijaOutcomeEvidencePolicy.CalculatePeriod(
            [new VendorSalesNivelacijaOutcomeSaleLineFact(3, 2, 100m, null, null, null)],
            new Dictionary<int, decimal?>(),
            emptyWindowIsZero: false);
        Assert.Equal(0m, uncovered.CostCoveragePct);
        Assert.Null(uncovered.MarginContribution);

        var missingPreWindow = VendorSalesNivelacijaOutcomeEvidencePolicy.CalculatePeriod(
            [], new Dictionary<int, decimal?>(), emptyWindowIsZero: false);
        var observedEmptyPostWindow = VendorSalesNivelacijaOutcomeEvidencePolicy.CalculatePeriod(
            [], new Dictionary<int, decimal?>(), emptyWindowIsZero: true);
        Assert.Null(missingPreWindow.Revenue);
        Assert.Equal(0m, observedEmptyPostWindow.Revenue);
        Assert.Null(observedEmptyPostWindow.MarginContribution);
    }

    [Fact]
    public void PrePostSurfaceAndExportUseTheSameLedgerRowsAndEvidenceMetadata()
    {
        var page = ReadRepoFile("Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx");
        Assert.Contains("Ishod sniženja", page);
        Assert.Contains("rows={outcomeLedger.events}", page);
        Assert.Contains("stockEvidence", page);
        Assert.Contains("costEvidence", page);
        Assert.Contains("bez kauzalnog zaključka", page);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), relativePath));
    }
}
