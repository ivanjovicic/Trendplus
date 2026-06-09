using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsReportsContractTests
{
    [Fact]
    public void SupplierDecisionReport_Success_ReturnsSuccessMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [
                CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m)
            ],
            0,
            0,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.False(report.UsedFallback);
        Assert.NotEmpty(report.Kpis);
    }

    [Fact]
    public void SupplierDecisionReport_Fallback_ReturnsWarningMetaAndUsedFallback()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [
                CreateSupplierRow(1, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc)
            ],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.UsedFallback);
        Assert.False(report.RecommendationAllowed);
        Assert.True(report.Meta?.IsPartial);
        Assert.Equal("FALLBACK_DATASET_USED", report.Meta?.WarningCode);
    }

    [Fact]
    public void SupplierDecisionReport_EmptyDataset_ReturnsEmptyMetaWithoutFakeZero()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset([], 0, 0, toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_data_in_period", report.Meta?.EmptyReason);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void SupplierDecisionReport_Error_ReturnsErrorMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");

        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionErrorReportResponse(filters, "SQL_TIMEOUT", "Timed out.", "corr-123");

        Assert.False(report.Meta?.Success);
        Assert.Equal("SQL_TIMEOUT", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void PilotIntakeReport_ReadyDataset_ReturnsSuccessMeta()
    {
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success("good"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.True(report.RecommendationAllowed);
        Assert.Contains(report.Kpis, k => k.Key == "readinessScore");
    }

    [Fact]
    public void PilotIntakeReport_NoImport_ReturnsEmptyMetaAndRecommendedActions()
    {
        var intake = CreatePilotIntakeReport(
            25,
            AnalyticsResponseMetaFactory.Empty("no_import", "Pilot intake izvestaj nema import batch u periodu.", "insufficient_data"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_import", report.Meta?.EmptyReason);
        Assert.False(report.RecommendationAllowed);
        Assert.NotEmpty(report.RecommendedActions);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void PilotIntakeReport_Error_ReturnsErrorMeta()
    {
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeErrorReportResponse(period, null, null, "all", "corr-456");

        Assert.False(report.Meta?.Success);
        Assert.Equal("pilot_intake_report_error", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void SupplierDecisionReportBuilder_ProducesRichSuccessReportForValidDataset()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            new[]
            {
                CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m),
                CreateSupplierRow(2, "Beta", "EXPAND_SELECTIVELY", 74m, 76m, 410000m, 1100m),
                CreateSupplierRow(3, "Gamma", "PRICE_NEGOTIATE", 63m, 68m, 280000m, 980m)
            },
            0,
            0,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.False(report.UsedFallback);
        Assert.True(report.RecommendationAllowed);
        Assert.NotEmpty(report.Kpis);
        Assert.NotEmpty(report.Sections);
        Assert.NotEmpty(report.Rows);
        Assert.Equal("supplier_decision", report.Type);
        Assert.Equal("Trendplus izveštaj dobavljača", report.Title);
        Assert.StartsWith("/analytics/supplier/report?", report.StableQueryUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void SupplierDecisionReportBuilder_ProducesFallbackWarningFor30dRequest()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            new[]
            {
                CreateSupplierRow(1, "Alpha", "EXPAND", 80m, 82m, 200000m, 400m, fromUtc, toUtc),
                CreateSupplierRow(2, "Beta", "HOLD", 70m, 71m, 150000m, 320m, fromUtc, toUtc),
                CreateSupplierRow(3, "Gamma", "PRICE_NEGOTIATE", 66m, 69m, 110000m, 250m, fromUtc, toUtc)
            },
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.UsedFallback);
        Assert.False(report.RecommendationAllowed);
        Assert.True(report.Meta?.IsPartial);
        Assert.Equal("FALLBACK_DATASET_USED", report.Meta?.WarningCode);
    }

    [Fact]
    public void SupplierDecisionReportBuilder_UsesEmptyMetaWithoutFakeZeroKpisWhenDatasetIsEmpty()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(Array.Empty<SupplierDecisionHubEndpoints.SupplierScoreRow>(), 0, 0, toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_data_in_period", report.Meta?.EmptyReason);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void SupplierDecisionErrorReport_UsesExplicitErrorMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");

        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionErrorReportResponse(filters, "SQL_TIMEOUT", "Timed out.", "corr-123");

        Assert.False(report.Meta?.Success);
        Assert.Equal("SQL_TIMEOUT", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
    }

    [Fact]
    public void PilotIntakeReportBuilder_ProducesRichSuccessReportForReadyDataset()
    {
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success("good"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.True(report.RecommendationAllowed);
        Assert.NotEmpty(report.Kpis);
        Assert.NotEmpty(report.Sections);
        Assert.Equal("pilot_intake", report.Type);
        Assert.StartsWith("/analytics/reports/pilot-intake?", report.StableQueryUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void PilotIntakeReportBuilder_UsesEmptyMetaWithoutFakeZeroKpisWhenNoImportExists()
    {
        var intake = CreatePilotIntakeReport(
            25,
            AnalyticsResponseMetaFactory.Empty("no_import", "Pilot intake izvestaj nema import batch u periodu.", "insufficient_data"));
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.Meta?.Success);
        Assert.Equal("no_import", report.Meta?.EmptyReason);
        Assert.False(report.RecommendationAllowed);
        Assert.Empty(report.Kpis);
    }

    [Fact]
    public void PilotIntakeErrorReport_UsesExplicitErrorMeta()
    {
        var period = (new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeErrorReportResponse(period, null, null, "all", "corr-456");

        Assert.False(report.Meta?.Success);
        Assert.Equal("pilot_intake_report_error", report.Meta?.ErrorCode);
        Assert.Empty(report.Kpis);
    }

    // ──────────────────────────────────────────────────────────────────
    // No-fake-zero regression: pilot intake readiness threshold
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void PilotIntakeReport_ReadinessAboveThreshold_AllowsRecommendationAndPopulatesKpis()
    {
        // readinessScore=85 >= 70 threshold + valid data → recommendation allowed, KPIs shown
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success());
        var period = (
            FromUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            ToExclusiveUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.True(report.RecommendationAllowed);
        Assert.NotEmpty(report.Kpis);
        Assert.Contains(report.Kpis, k => k.Key == "readinessScore");
    }

    [Fact]
    public void PilotIntakeReport_ReadinessBelowThreshold_DisablesRecommendationWithKpisPresent()
    {
        // readinessScore=40 < 70 threshold but data exists → recommendation disabled, KPIs still shown (no fake-zero suppression)
        var intake = CreatePilotIntakeReport(40, AnalyticsResponseMetaFactory.Success());
        var period = (
            FromUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            ToExclusiveUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var report = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        Assert.False(report.RecommendationAllowed);
        // KPIs must still be populated — low readiness disables recommendation but does not suppress data display
        Assert.NotEmpty(report.Kpis);
        Assert.Contains(report.Kpis, k => k.Key == "readinessScore");
    }

    // ──────────────────────────────────────────────────────────────────
    // No-fake-zero regression: scorecard 30d empty → no silent 180d fallback
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Scorecard_Explicit30dFilters_EmptyDataset_InsufficientDataNotFakeZeroAndPeriodIsNot180d()
    {
        // 30d explicit range with no supplier rows → insufficient_data meta, period stays 30d (not silently expanded to 180d)
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29); // 30 day window
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc, toUtc, true, null, null, null, null, false, false, null, null, "all");
        var emptyDataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [], 0, 0, new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(emptyDataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, emptyDataset, filters);

        Assert.Equal("insufficient_data", report.DataQualityStatus);
        Assert.Empty(report.Kpis);
        Assert.False(report.RecommendationAllowed);
        // Requested dataset must be "30d", not silently expanded to "180d"
        Assert.Equal("30d", report.Period.RequestedDataset);
    }

    [Fact]
    public void SupplierScorecard_Explicit30dNoRows_DoesNotFallbackTo180d()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc, toUtc, true, null, null, null, null, false, false, null, null, "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset([], 0, 0, toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.Equal("30d", report.Period.RequestedDataset);
        Assert.NotEqual("180d", report.Period.RequestedDataset);
        Assert.Equal("insufficient_data", report.DataQualityStatus);
    }

    [Fact]
    public void SupplierScorecard_Fallback_GatesRecommendation()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc, toUtc, true, null, null, null, null, false, false, null, null, "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        Assert.True(report.UsedFallback);
        Assert.False(report.RecommendationAllowed);
        Assert.False(string.IsNullOrWhiteSpace(report.FallbackReason));
    }

    [Fact]
    public void SupplierDecisionReport_IncludesSupplierNegotiationPackSection()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = CreateDefaultFilters(fromUtc, toUtc, supplierId: 1, reportSection: "supplier_negotiation_pack");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        var section = Assert.Single(report.Sections.Where(section => section.Key == "supplier_negotiation_pack"));
        Assert.Equal("Paket za razgovor sa dobavljačem", section.Title);
        Assert.True(section.RowCount > 0);
        Assert.Contains(section.Rows, row => string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Finalni savet", StringComparison.Ordinal));
    }

    [Fact]
    public void SupplierDecisionReport_NegotiationPack_IncludesFallbackWarningWhenUsedFallbackIsTrue()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            1,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Korišćen fallback dataset", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal)
            && Convert.ToString(row.GetValueOrDefault("note"))!.Contains("period:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SupplierDecisionReport_NegotiationPack_BlocksFinalAdviceWhenRecommendationNotAllowed()
    {
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var fromUtc = toUtc.AddDays(-29);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            1,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m, fromUtc, toUtc)],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));
        var finalAdvice = Assert.Single(section.Rows.Where(row => string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Finalni savet", StringComparison.Ordinal)));

        var value = Convert.ToString(finalAdvice.GetValueOrDefault("value")) ?? string.Empty;
        Assert.Contains("Pomoćni signal", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pojačaj saradnju", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pregovaraj bolje uslove", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Smanji narednu narudžbinu", value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupplierDecisionReport_NegotiationPack_IncludesMissingCostWarningWhenSignalIsHigh()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            1,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 88m, 90m, 620000m, 1700m, fromUtc, toUtc, reasonCodes: ["missing_cost", "high_confidence"])],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Visok missing cost", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal)
            && Convert.ToString(row.GetValueOrDefault("note"))!.Contains("maržnog doprinosa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SupplierDecisionReport_NegotiationPack_IncludesCriticalDataQualityWarning()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            1,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "HOLD", 42m, 45m, 180000m, 280m, fromUtc, toUtc, dataQualityStatus: "critical")],
            0,
            0,
            toUtc);

        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        summary.Meta!.DataQualityStatus = "critical";
        var report = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);
        var section = Assert.Single(report.Sections.Where(x => x.Key == "supplier_negotiation_pack"));

        Assert.Contains(section.Rows, row =>
            string.Equals(Convert.ToString(row.GetValueOrDefault("topic")), "Kvalitet podataka nije idealan", StringComparison.Ordinal)
            && string.Equals(Convert.ToString(row.GetValueOrDefault("group")), "Upozorenja", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportCacheKeysDifferentiateCriticalDimensions()
    {
        var fromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var supplierBase = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, null, null, "all");
        var supplierScoped = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, 42, 7, "imported");
        var pilotBase = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, null, null, "all");
        var pilotScoped = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, 7, 42, "imported");

        Assert.Contains("analytics-report:supplier-decision:v1:", supplierBase);
        Assert.Contains("analytics-report:pilot-intake:v1:", pilotBase);
        Assert.NotEqual(supplierBase, supplierScoped);
        Assert.NotEqual(pilotBase, pilotScoped);
        Assert.Contains(":rv:1:", supplierBase);
        Assert.Contains(":rv:1:", pilotBase);

        var supplierCategory = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, "patike", null, null, null, false, false, null, null, "all");
        var supplierGender = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, "women", null, null, false, false, null, null, "all");
        var supplierSeason = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, 3, null, false, false, null, null, "all");
        var supplierRevenue = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, 1000m, false, false, null, null, "all");
        var supplierConfidence = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, true, false, null, null, "all");
        var supplierOos = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, true, null, null, "all");
        var supplierNegotiationSection = CreateSupplierDecisionReportKeyWithSection(fromUtc, toUtc, "all", "supplier_negotiation_pack");

        Assert.NotEqual(supplierBase, supplierCategory);
        Assert.NotEqual(supplierBase, supplierGender);
        Assert.NotEqual(supplierBase, supplierSeason);
        Assert.NotEqual(supplierBase, supplierRevenue);
        Assert.NotEqual(supplierBase, supplierConfidence);
        Assert.NotEqual(supplierBase, supplierOos);
        Assert.NotEqual(supplierBase, supplierNegotiationSection);

        var supplierVersion2 = AnalyticsCacheKeys.SupplierDecisionReport(fromUtc, toUtc, null, null, null, null, false, false, null, null, "all", reportCacheVersion: 2);
        var pilotVersion2 = AnalyticsCacheKeys.PilotIntakeReport(fromUtc, toUtc, null, null, "all", reportCacheVersion: 2);
        Assert.NotEqual(supplierBase, supplierVersion2);
        Assert.NotEqual(pilotBase, pilotVersion2);
    }

    [Fact]
    public void ReportCacheKeyFingerprint_IsStableAndDoesNotExposeOriginalKey()
    {
        const string keyA = "analytics:analytics-report:supplier-decision:v1:rv:2:from:202601010000:to:202603310000:supplier:42:store:7:scope:imported";
        const string keyB = "analytics:analytics-report:supplier-decision:v1:rv:2:from:202601010000:to:202603310000:supplier:43:store:7:scope:imported";

        var hashA1 = AnalyticsCacheKeys.SafeKeyFingerprint(keyA);
        var hashA2 = AnalyticsCacheKeys.SafeKeyFingerprint(keyA);
        var hashB = AnalyticsCacheKeys.SafeKeyFingerprint(keyB);

        Assert.Equal(hashA1, hashA2);
        Assert.NotEqual(hashA1, hashB);
        Assert.DoesNotContain(keyA, hashA1, StringComparison.Ordinal);
        Assert.DoesNotContain("analytics-report", hashA1, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SupplierDecisionReport_CacheHit_ReturnsCachedReportAndCorrelationMeta")]
    public async Task SupplierDecisionReport_CacheHit_ReturnsCachedReportAndCorrelationMeta()
    {
        var fromUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
        var filters = new SupplierDecisionHubEndpoints.SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            true,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            null,
            "all");
        var dataset = new SupplierDecisionHubEndpoints.SupplierRowsDataset(
            [CreateSupplierRow(1, "Alpha", "EXPAND", 82m, 84m, 520000m, 1400m)],
            0,
            0,
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));
        var summary = SupplierDecisionHubEndpoints.BuildSummaryResponse(dataset, filters);
        var cachedReport = SupplierDecisionHubEndpoints.BuildSupplierDecisionReportResponse(summary, dataset, filters);

        var cache = new StubAnalyticsCacheService { CachedValue = cachedReport };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var configuration = CreateTestConfiguration();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-supplier-report-cache-hit";

        var result = await SupplierDecisionHubEndpoints.HandleSupplierDecisionReportAsync(
            httpContext,
            configuration,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            refreshStatusService: null!,
            fromDate: fromUtc,
            toDate: toUtc,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.True(report.Meta?.Success);
        Assert.Equal("corr-supplier-report-cache-hit", report.Meta?.CorrelationId);
        Assert.Equal("supplier_decision", report.Type);
    }

    [Fact(DisplayName = "PilotIntakeReport_CacheHit_ReturnsCachedReportAndCorrelationMeta")]
    public async Task PilotIntakeReport_CacheHit_ReturnsCachedReportAndCorrelationMeta()
    {
        var period = (
            FromUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            ToExclusiveUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var intake = CreatePilotIntakeReport(85, AnalyticsResponseMetaFactory.Success("good"));
        var cachedReport = DataQualityEndpoints.BuildPilotIntakeReportResponse(intake, period, null, null, "all");

        var cache = new StubAnalyticsCacheService { CachedValue = cachedReport };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-pilot-report-cache-hit";

        var result = await DataQualityEndpoints.HandlePilotIntakeReportAsync(
            httpContext,
            trendDb: null!,
            analyticsDb: null!,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            healthService: null!,
            refreshStatusService: null!,
            fromDate: period.FromUtc.ToString("yyyy-MM-dd"),
            toDate: period.ToUtc.ToString("yyyy-MM-dd"),
            storeId: null,
            supplierId: null,
            scope: "all",
            dataScope: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.True(report.Meta?.Success);
        Assert.Equal("corr-pilot-report-cache-hit", report.Meta?.CorrelationId);
        Assert.Equal("pilot_intake", report.Type);
    }

    [Fact(DisplayName = "PilotIntakeReport_CacheFailure_ReturnsErrorMetaWithoutFakeZeroPayload")]
    public async Task PilotIntakeReport_CacheFailure_ReturnsErrorMetaWithoutFakeZeroPayload()
    {
        var cache = new StubAnalyticsCacheService { GetException = new InvalidOperationException("cache unavailable") };
        var cacheAdmin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-pilot-cache-fail";

        var result = await DataQualityEndpoints.HandlePilotIntakeReportAsync(
            httpContext,
            trendDb: null!,
            analyticsDb: null!,
            cache,
            cacheAdmin,
            NullLoggerFactory.Instance,
            healthService: null!,
            refreshStatusService: null!,
            fromDate: "2026-06-01",
            toDate: "2026-06-30",
            storeId: null,
            supplierId: null,
            scope: "all",
            dataScope: null,
            ct: CancellationToken.None);

        var ok = Assert.IsType<Ok<AnalyticsReportResponseDto>>(result);
        var report = Assert.IsType<AnalyticsReportResponseDto>(ok.Value);
        Assert.False(report.Meta?.Success);
        Assert.Equal("pilot_intake_report_error", report.Meta?.ErrorCode);
        Assert.Equal("corr-pilot-cache-fail", report.Meta?.CorrelationId);
        Assert.Empty(report.Kpis);
    }

    private static IConfiguration CreateTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["ConnectionStrings:AnalyticsConnection"] = "Host=127.0.0.1;Port=5432;Database=analytics;Username=test;Password=test",
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=5432;Database=defaultdb;Username=test;Password=test"
            })
            .Build();

    private sealed class StubAnalyticsCacheService : IAnalyticsCacheService
    {
        public object? CachedValue { get; set; }
        public Exception? GetException { get; set; }

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

        public void SetRedisEnabled(bool enabled)
        {
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            if (GetException is not null)
            {
                throw GetException;
            }

            return Task.FromResult(CachedValue as T);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            CachedValue = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            if (GetException is not null)
            {
                throw GetException;
            }

            if (CachedValue is T typed)
            {
                return typed;
            }

            var value = await factory();
            CachedValue = value;
            return value;
        }
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

    private static DataQualityEndpoints.PilotDataQualityIntakeReportDto CreatePilotIntakeReport(
        int readinessScore,
        AnalyticsResponseMetaDto meta)
    {
        return new DataQualityEndpoints.PilotDataQualityIntakeReportDto(
            new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            "all",
            null,
            null,
            new DateTime(2026, 6, 29, 22, 0, 0, DateTimeKind.Utc),
            "succeeded",
            new DateTime(2026, 6, 30, 4, 0, 0, DateTimeKind.Utc),
            "fresh",
            readinessScore,
            readinessScore >= 90 ? "excellent" : readinessScore >= 70 ? "good" : readinessScore >= 40 ? "warning" : "critical",
            readinessScore >= 90 ? "Spremno" : readinessScore >= 70 ? "Upotrebljivo uz upozorenja" : "Ograničeno",
            new DataQualityEndpoints.PilotDataQualityIntakeLoadedDataDto(1200, 45000, 9300, 48, 12, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc)),
            new DataQualityEndpoints.PilotDataQualityIntakeIssuesDto(4, 8, 15, 9, 11, 2, 1, 3, 1),
            new DataQualityEndpoints.PilotDataQualityIntakeImpactDto(0.04d, 0.01d, 12, 5, 18),
            ["Povezi dobavljace", "Pokreni osvezavanje analitike"],
            meta);
    }
    private static SupplierDecisionHubEndpoints.SupplierDecisionHubFilters CreateDefaultFilters(
        DateTime fromUtc,
        DateTime toUtc,
        int? supplierId = null,
        int? storeId = null,
        string dataScope = "all",
        string reportSection = "all")
    {
        var filterType = typeof(SupplierDecisionHubEndpoints.SupplierDecisionHubFilters);
        var constructors = filterType.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var ctor = constructors
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .FirstOrDefault();
        Assert.NotNull(ctor);

        var args = ctor!.GetParameters()
            .Select(parameter => string.Equals(parameter.Name, "fromUtc", StringComparison.OrdinalIgnoreCase) ? fromUtc :
                string.Equals(parameter.Name, "toUtc", StringComparison.OrdinalIgnoreCase) ? toUtc :
                string.Equals(parameter.Name, "recommendationAllowed", StringComparison.OrdinalIgnoreCase) ? true :
                string.Equals(parameter.Name, "category", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "gender", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "season", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "revenueMin", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "highConfidenceOnly", StringComparison.OrdinalIgnoreCase) ? false :
                string.Equals(parameter.Name, "oosRiskOnly", StringComparison.OrdinalIgnoreCase) ? false :
                string.Equals(parameter.Name, "supplierId", StringComparison.OrdinalIgnoreCase) ? supplierId :
                string.Equals(parameter.Name, "storeId", StringComparison.OrdinalIgnoreCase) ? storeId :
                string.Equals(parameter.Name, "dataScope", StringComparison.OrdinalIgnoreCase) ? dataScope :
                string.Equals(parameter.Name, "reportSection", StringComparison.OrdinalIgnoreCase) ? reportSection :
                parameter.HasDefaultValue ? parameter.DefaultValue : null)
            .ToArray();

        return (SupplierDecisionHubEndpoints.SupplierDecisionHubFilters)ctor.Invoke(args);
    }

    private static string CreateSupplierDecisionReportKeyWithSection(
        DateTime fromUtc,
        DateTime toUtc,
        string dataScope,
        string reportSection)
    {
        var methods = typeof(AnalyticsCacheKeys)
            .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, nameof(AnalyticsCacheKeys.SupplierDecisionReport), StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(methods);

        var methodWithSection = methods.FirstOrDefault(method => method.GetParameters().Any(parameter => string.Equals(parameter.Name, "reportSection", StringComparison.OrdinalIgnoreCase)));
        var targetMethod = methodWithSection ?? methods.First();
        var args = targetMethod.GetParameters()
            .Select(parameter => string.Equals(parameter.Name, "fromUtc", StringComparison.OrdinalIgnoreCase) ? fromUtc :
                string.Equals(parameter.Name, "toUtc", StringComparison.OrdinalIgnoreCase) ? toUtc :
                string.Equals(parameter.Name, "category", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "gender", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "season", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "revenueMin", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "highConfidenceOnly", StringComparison.OrdinalIgnoreCase) ? false :
                string.Equals(parameter.Name, "oosRiskOnly", StringComparison.OrdinalIgnoreCase) ? false :
                string.Equals(parameter.Name, "supplierId", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "storeId", StringComparison.OrdinalIgnoreCase) ? null :
                string.Equals(parameter.Name, "dataScope", StringComparison.OrdinalIgnoreCase) ? dataScope :
                string.Equals(parameter.Name, "reportSection", StringComparison.OrdinalIgnoreCase) ? reportSection :
                string.Equals(parameter.Name, "reportCacheVersion", StringComparison.OrdinalIgnoreCase) ? 1 :
                parameter.HasDefaultValue ? parameter.DefaultValue : null)
            .ToArray();

        var key = targetMethod.Invoke(null, args);
        return Assert.IsType<string>(key);
    }

}
