using Api.Models;
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
        Assert.Contains("historical_stock_unavailable", ReadRepoFile("Api/Models/VendorSalesNivelacijaModels.cs"));
        Assert.Contains("emptyWindowIsZero", source);
        Assert.Contains("new PeriodMetrics(null, null, null, null)", source);
        Assert.Contains("allCovered ? margin : null", source);
        Assert.Contains("AnalyticsMarginPolicy.ResolveUnitCostWithSnapshot(", source);
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
