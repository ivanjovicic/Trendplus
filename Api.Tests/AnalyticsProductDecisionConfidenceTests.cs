using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsProductDecisionConfidenceTests
{
    [Fact]
    public void HighConfidence_ProfileIncludesDriversImpactAndFreshness()
    {
        var row = new ProductDecisionCenterRowDto
        {
            ProductId = 101,
            Sku = "SKU-101",
            ProductName = "Model X",
            SupplierId = 77,
            SupplierName = "Supplier A",
            Revenue = 120000m,
            UnitsSold = 40,
            VelocityUnitsPerDay = 1.2m,
            MarginContribution = 24000m,
            MarginPct = 24m,
            MarginCoveragePct = 90m,
            CurrentStock = 10,
            MinStock = 5,
            StockGap = 5,
            DaysSinceLastSale = 3,
            TrendPct = 3m,
            LostSalesEstimate = 25000m,
            SlowStockCapital = 0m,
            RecommendationStatus = "REPLENISH",
            RecommendationReason = "Brza prodaja i nizak stock cover.",
            ReasonCodes = ["high_velocity", "low_stock"],
            DataQualityStatus = "good",
            ConfidencePct = 88,
            ReliabilityPct = 79,
        };

        var profile = CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(
            row,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("product", profile.SourceType);
        Assert.Equal("product:101", profile.SourceKey);
        Assert.Equal("REPLENISH", profile.RecommendationType);
        Assert.Equal("high", profile.ConfidenceLevel);
        Assert.Equal(88, profile.ConfidenceScore);
        Assert.Equal(25000m, profile.ExpectedImpactRsd);
        Assert.Equal(14, profile.ImpactWindowDays);
        Assert.Equal("Brza prodaja i nizak stock cover.", profile.ExplainabilityText);
        Assert.Equal("fresh", profile.InputFreshnessStatus);
        var confidenceNode = Assert.Single(profile.EvidenceChain, node => node.Code == "confidence_signal");
        Assert.Equal("Pouzdanost", confidenceNode.Label);
        Assert.Equal("Visoka sigurnost · 88%", confidenceNode.ValueText);
        var freshnessNode = Assert.Single(profile.EvidenceChain, node => node.Code == "freshness_signal");
        Assert.Equal("Svežina ulaza", freshnessNode.Label);
        Assert.Equal("Sveže", freshnessNode.ValueText);
        Assert.Equal("Kvalitet podataka dobar", freshnessNode.Detail);
        Assert.Contains("sales_velocity", profile.PrimaryDrivers);
        Assert.Contains("stock_risk", profile.PrimaryDrivers);
        Assert.Contains("margin", profile.PrimaryDrivers);
        Assert.NotEmpty(profile.EvidenceChain);
        Assert.Contains(profile.EvidenceChain, node => node.Code == "selected_recommendation");
        Assert.Contains(profile.EvidenceChain, node => node.Code == "sales_signal");
        Assert.Contains(profile.EvidenceChain, node => node.Code == "expected_impact");
        Assert.DoesNotContain(profile.WarningCodes, code => code == "expected_impact_denominator_missing");
    }

    [Fact]
    public void InsufficientData_ProfileDoesNotExposeHighConfidenceOrFakeImpact()
    {
        var row = new ProductDecisionCenterRowDto
        {
            ProductId = 202,
            Sku = "SKU-202",
            ProductName = "Model Y",
            Revenue = 0m,
            UnitsSold = 0,
            VelocityUnitsPerDay = 0m,
            MarginContribution = 0m,
            MarginCoveragePct = 0m,
            CurrentStock = 0,
            MinStock = 0,
            StockGap = 0,
            DaysSinceLastSale = null,
            TrendPct = null,
            LostSalesEstimate = 0m,
            SlowStockCapital = 0m,
            RecommendationStatus = "INSUFFICIENT_DATA",
            RecommendationReason = "Nedovoljno signala za pouzdanu preporuku.",
            ReasonCodes = ["insufficient_history", "missing_cost"],
            DataQualityStatus = "insufficient_data",
            ConfidencePct = 32,
            ReliabilityPct = 24,
        };

        var profile = CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(
            row,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("insufficient_data", profile.ConfidenceLevel);
        Assert.Null(profile.ConfidenceScore);
        Assert.Null(profile.ExpectedImpactRsd);
        Assert.Equal("critical", profile.InputFreshnessStatus);
        var criticalConfidenceNode = Assert.Single(profile.EvidenceChain, node => node.Code == "confidence_signal");
        Assert.Equal("Nedovoljno podataka", criticalConfidenceNode.ValueText);
        var criticalFreshnessNode = Assert.Single(profile.EvidenceChain, node => node.Code == "freshness_signal");
        Assert.Equal("Kritično", criticalFreshnessNode.ValueText);
        Assert.Contains("missing_cost", profile.WarningCodes);
        Assert.Contains("insufficient_history", profile.WarningCodes);
        Assert.Contains("expected_impact_denominator_missing", profile.WarningCodes);
        Assert.Contains("sparse_sales", profile.PrimaryDrivers);
        Assert.Contains(profile.EvidenceChain, node => node.Code == "warning:missing_cost");
        Assert.Contains(profile.EvidenceChain, node => node.Code == "warning:insufficient_history");
        var impactNode = Assert.Single(profile.EvidenceChain.Where(node => node.Code == "expected_impact"));
        Assert.True(impactNode.IsMissing);
        Assert.Equal("Nije dostupno", impactNode.ValueText);
    }
}
