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
            RecommendationLabel = "Dopuni",
            RecommendationReason = "Brza prodaja i nizak stock cover.",
            ReasonCodes = ["high_velocity", "low_stock"],
            DataQualityStatus = "good",
            RecommendationAllowed = true,
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
        Assert.NotNull(profile.WhyPanel);
        Assert.Equal("REPLENISH", profile.WhyPanel.RecommendationStatus);
        Assert.Equal("Dopuni", profile.WhyPanel.RecommendationLabel);
        Assert.Equal("Brza prodaja i nizak stock cover.", profile.WhyPanel.ExplainabilityText);
        Assert.Equal("recommendation_reason", profile.WhyPanel.SummarySource);
        Assert.False(profile.WhyPanel.SummaryFallbackUsed);
        Assert.Null(profile.WhyPanel.SummaryFallbackReason);
        Assert.NotEmpty(profile.WhyPanel.ConfidenceBreakdown);
        Assert.NotEmpty(profile.WhyPanel.AlternativeRecommendations);
        Assert.NotEmpty(profile.WhyPanel.DecisionTree);
        var selectedTreeNode = Assert.Single(profile.WhyPanel.DecisionTree, node => node.Code == "selected_branch");
        Assert.True(selectedTreeNode.IsSelected);
        Assert.Equal("Aktiviraj dopunu prema minimalnoj zalihi.", selectedTreeNode.ValueText);
        Assert.NotEmpty(profile.ConfidenceBreakdown);
        var scoreNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "confidence_score");
        Assert.Equal("Ocena pouzdanosti", scoreNode.Label);
        Assert.Equal("Visoka sigurnost · 88%", scoreNode.ValueText);
        var coverageNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "evidence_coverage");
        Assert.Equal("Široka", coverageNode.ValueText);
        var reliabilityNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "reliability_signal");
        Assert.Equal("79%", reliabilityNode.ValueText);
        var freshnessBreakdownNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "freshness_signal");
        Assert.Equal("Sveže", freshnessBreakdownNode.ValueText);
        var dataQualityNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "data_quality_signal");
        Assert.Equal("dobar", dataQualityNode.ValueText);
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
        Assert.NotEmpty(profile.AlternativeRecommendations);
        var boostAlternative = Assert.Single(profile.AlternativeRecommendations, node => node.RecommendationStatus == "BOOST");
        Assert.False(string.IsNullOrWhiteSpace(boostAlternative.WhyLowerRanked));
        Assert.Equal(1, boostAlternative.Rank);
        Assert.Equal("Pojačaj", boostAlternative.RecommendationLabel);
        Assert.Contains(profile.AlternativeRecommendations, node => node.RecommendationStatus == "WATCH");

        row.RecommendationId = profile.RecommendationId;
        row.RecommendationType = profile.RecommendationType;
        row.ConfidenceLevel = profile.ConfidenceLevel;
        row.ConfidenceScore = profile.ConfidenceScore;
        row.ExplainabilityText = profile.ExplainabilityText;
        row.InputFreshnessStatus = profile.InputFreshnessStatus;
        row.PrimaryDrivers = [.. profile.PrimaryDrivers];
        row.WarningCodes = [.. profile.WarningCodes];
        row.EvidenceChain = [.. profile.EvidenceChain];
        row.ConfidenceBreakdown = [.. profile.ConfidenceBreakdown];
        CachedAnalyticsEndpoints.ApplyDecisionEvidenceSnapshotPreview(
            row,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal("absent", row.EvidenceSnapshotStatus);
        Assert.NotNull(row.EvidenceSnapshotPreview);
        Assert.Equal(1, row.EvidenceSnapshotPreview!.SchemaVersion);
        Assert.Equal(profile.RecommendationId, row.EvidenceSnapshotPreview.RecommendationId);
        Assert.Equal(profile.RecommendationType, row.EvidenceSnapshotPreview.RecommendationType);
        Assert.Equal(profile.EvidenceChain.Count, row.EvidenceSnapshotPreview.EvidenceChain.Count);
        Assert.Equal(profile.ConfidenceBreakdown.Count, row.EvidenceSnapshotPreview.ConfidenceBreakdown.Count);
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
        var insufficientScoreNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "confidence_score");
        Assert.True(insufficientScoreNode.IsMissing);
        Assert.Equal("Nedovoljno podataka", insufficientScoreNode.ValueText);
        var insufficientCoverageNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "evidence_coverage");
        Assert.Equal("Nedovoljna", insufficientCoverageNode.ValueText);
        var criticalFreshnessBreakdownNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "freshness_signal");
        Assert.Equal("Kritično", criticalFreshnessBreakdownNode.ValueText);
        var criticalDataQualityNode = Assert.Single(profile.ConfidenceBreakdown, node => node.Code == "data_quality_signal");
        Assert.True(criticalDataQualityNode.IsMissing);
        Assert.Equal("nedovoljno podataka", criticalDataQualityNode.ValueText);
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
        Assert.NotEmpty(profile.AlternativeRecommendations);
        Assert.Contains(profile.AlternativeRecommendations, node => node.RecommendationStatus == "FIX_DATA");
        Assert.Contains(profile.AlternativeRecommendations, node => node.RecommendationStatus == "WATCH");
        Assert.All(profile.AlternativeRecommendations, node => Assert.False(string.IsNullOrWhiteSpace(node.WhyLowerRanked)));
        Assert.NotNull(profile.WhyPanel);
        Assert.Equal("INSUFFICIENT_DATA", profile.WhyPanel.RecommendationStatus);
        Assert.Equal("Nedovoljno podataka", profile.WhyPanel.RecommendationLabel);
        Assert.Equal("Nedovoljno signala za pouzdanu preporuku.", profile.WhyPanel.ExplainabilityText);
        Assert.Equal("recommendation_reason", profile.WhyPanel.SummarySource);
        Assert.False(profile.WhyPanel.SummaryFallbackUsed);
        Assert.NotEmpty(profile.WhyPanel.EvidenceChain);
        Assert.NotEmpty(profile.WhyPanel.DecisionTree);
        Assert.Contains(profile.WhyPanel.DecisionTree, node => node.Code == "data_quality_gate" && !node.IsSelected);
    }

    [Fact]
    public void MissingRecommendationReason_UsesBackendComposedWhyPanelSummary()
    {
        var row = new ProductDecisionCenterRowDto
        {
            ProductId = 303,
            Sku = "SKU-303",
            ProductName = "Model Z",
            Revenue = 180000m,
            UnitsSold = 52,
            VelocityUnitsPerDay = 1.8m,
            MarginContribution = 36000m,
            MarginPct = 28m,
            MarginCoveragePct = 91m,
            CurrentStock = 3,
            MinStock = 10,
            StockGap = 7,
            DaysSinceLastSale = 5,
            TrendPct = 14m,
            LostSalesEstimate = 25000m,
            SlowStockCapital = 0m,
            RecommendationStatus = "WATCH",
            RecommendationReason = "",
            ReasonCodes = ["positive_trend"],
            DataQualityStatus = "good",
            RecommendationAllowed = true,
            ConfidencePct = 41,
            ReliabilityPct = 38,
        };

        var profile = CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(
            row,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("backend_composed", profile.WhyPanel.SummarySource);
        Assert.True(profile.WhyPanel.SummaryFallbackUsed);
        Assert.Equal("recommendation_reason_missing", profile.WhyPanel.SummaryFallbackReason);
        Assert.False(string.IsNullOrWhiteSpace(profile.WhyPanel.ExplainabilityText));
    }

    [Fact]
    public void BlockedOrStaleEvidence_ClearsActionabilityConfidenceAndExpectedImpact()
    {
        var row = new ProductDecisionCenterRowDto
        {
            ProductId = 404,
            Sku = "SKU-404",
            ProductName = "Model stale signal",
            Revenue = 120000m,
            UnitsSold = 36,
            VelocityUnitsPerDay = 1.1m,
            MarginContribution = 24000m,
            MarginPct = 24m,
            MarginCoveragePct = 92m,
            CurrentStock = 1,
            MinStock = 8,
            StockGap = 7,
            DaysSinceLastSale = 90,
            TrendPct = 8m,
            LostSalesEstimate = 36000m,
            RecommendationStatus = "REPLENISH",
            RecommendationReason = "Istorijski signal bez sveže prodaje.",
            DataQualityStatus = "good",
            RecommendationAllowed = true,
            ConfidencePct = 91,
            ReliabilityPct = 85
        };

        var profile = CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(
            row,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(row.RecommendationAllowed);
        Assert.Equal("stale", profile.InputFreshnessStatus);
        Assert.Equal("insufficient_data", profile.ConfidenceLevel);
        Assert.Null(profile.ConfidenceScore);
        Assert.Null(profile.ExpectedImpactRsd);
        Assert.Null(profile.ImpactWindowDays);
        Assert.Contains("product_recommendation_blocked", profile.WarningCodes);
        Assert.False(profile.WhyPanel.RecommendationAllowed);
        Assert.Null(profile.WhyPanel.ExpectedImpactRsd);
        Assert.Contains(profile.EvidenceChain, node => node.Code == "actionability" && node.ValueText == "Ne");
        Assert.Contains(profile.EvidenceChain, node => node.Code == "expected_impact" && node.IsMissing);
    }
}
