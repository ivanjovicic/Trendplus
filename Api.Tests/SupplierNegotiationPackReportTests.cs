using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class SupplierNegotiationPackReportTests
{
    [Fact]
    public void SupplierReport_WithSupplierFilter_IncludesNegotiationPackSection()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = CreateFilters(fromUtc, toUtc, supplierId: 11);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(11, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));
        Assert.True(section.RowCount > 0);
        Assert.Contains(section.Rows, row => string.Equals(Convert.ToString(row.GetValueOrDefault("item")), "Prihod", StringComparison.Ordinal));
    }

    [Fact]
    public void SupplierReport_WhenFallbackUsed_ContainsFallbackWarningInNegotiationPack()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = CreateFilters(fromUtc, toUtc);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(11, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("item")), "Korišćen fallback dataset", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal));
    }

    [Fact]
    public void SupplierReport_WhenRecommendationNotAllowed_BlocksFinalAdviceInNegotiationPack()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = CreateFilters(fromUtc, toUtc);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(11, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));
        var finalAdvice = Assert.Single(section.Rows.Where(row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("item")), "Finalni savet", StringComparison.Ordinal)));

        Assert.Contains("Pomoćni signal", Convert.ToString(finalAdvice.GetValueOrDefault("value")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupplierReport_WhenCoverageIsNotGood_AddsMissingCostWarningInNegotiationPack()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = CreateFilters(fromUtc, toUtc);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(11, "Alpha", "PRICE_NEGOTIATE", 42m, 43m, 120000m, 310m, fromUtc, toUtc, reasonCodes: ["missing_cost"])],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("item")), "Visok missing cost", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal));
    }

    [Fact]
    public void SupplierReport_WhenDataQualityIsCritical_AddsCriticalWarningInNegotiationPack()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = CreateFilters(fromUtc, toUtc, supplierId: 11);
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(11, "Alpha", "HOLD", 41m, 42m, 180000m, 280m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        summary = summary with
        {
            TrustMetadata = summary.TrustMetadata! with
            {
                DataCoverageStatus = "critical",
                RecommendationAllowed = false
            }
        };
        summary.Meta!.DataQualityStatus = "critical";

        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Kvalitet podataka nije idealan", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("value")), "critical", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("note")), "Preporuke proveriti kroz Data Quality ekran.", StringComparison.Ordinal));
    }

    private static SupplierDecisionHubEndpoints.SupplierDecisionHubFilters CreateFilters(
        DateTime fromUtc,
        DateTime toUtc,
        int? supplierId = null)
    {
        return new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            supplierId,
            null,
            "all");
    }

    private static SupplierDecisionHubEndpoints.SupplierScoreRow CreateSupplierRow(
        int supplierId,
        string supplierName,
        string recommendationCode,
        decimal confidenceScore,
        decimal reliabilityPct,
        decimal revenue,
        decimal units,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        bool supplierNameMissing = false,
        string dataQualityStatus = "good",
        IReadOnlyList<string>? reasonCodes = null)
    {
        var fromUtc = periodFrom ?? new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = periodTo ?? new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);

        return new SupplierDecisionHubEndpoints.SupplierScoreRow(
            supplierId,
            supplierName,
            fromUtc,
            toUtc,
            revenue,
            units,
            0.64m,
            0.57m,
            0.36m,
            0.31m,
            0.18m,
            145000m,
            0.44m,
            28m,
            24m,
            0.05m,
            62m,
            78m,
            "Stabilan signal dobavljača.",
            "Marža",
            "Sell-through",
            "Markdown",
            81m,
            recommendationCode,
            confidenceScore,
            supplierNameMissing,
            reliabilityPct,
            dataQualityStatus,
            "Signal podržava prikazanu preporuku.",
            reasonCodes ?? ["stable_margin", "repeat_winner"]);
    }
}

