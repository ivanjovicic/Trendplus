using System;
using Application.Analytics;
using System.Linq;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class AnalyticsDecisionRecommendationEngineTests
{
    [Fact(DisplayName = "Unknown entity -> do_not_trust")]
    public void UnknownEntity_DoNotTrust()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: true,
            TotalRevenue: 1000m,
            TotalUnits: 10,
            ItemCount: 5,
            SharePct: 50d,
            MarginPct: 20d,
            MarginCoveragePct: 80d,
            SplitCoveragePct: 80d,
            PopRevenueChangePct: 10d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 900m,
            PreviousPeriodUnits: 9,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 20d);
        Assert.Equal("do_not_trust", res.Status);
        Assert.False(res.RecommendationAllowed);
        Assert.Contains("unknown_entity", res.ReasonCodes);
    }

    [Fact(DisplayName = "Tiny sample -> insufficient_data")]
    public void TinySample_InsufficientData()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 100m,
            TotalUnits: 2,
            ItemCount: 1,
            SharePct: 0.5d,
            MarginPct: 10d,
            MarginCoveragePct: 100d,
            SplitCoveragePct: 100d,
            PopRevenueChangePct: 5d,
            PopUnitsChangePct: 2d,
            PreviousPeriodRevenue: 90m,
            PreviousPeriodUnits: 2,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 10d);
        Assert.Equal("insufficient_data", res.Status);
        Assert.False(res.RecommendationAllowed);
        Assert.Contains("tiny_sample", res.ReasonCodes);
    }

    [Fact(DisplayName = "Missing previous period -> insufficient_data or review per rules")]
    public void MissingPreviousPeriod_ReviewOrInsufficient()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 1000m,
            TotalUnits: 10,
            ItemCount: 5,
            SharePct: 5d,
            MarginPct: 10d,
            MarginCoveragePct: 80d,
            SplitCoveragePct: 80d,
            PopRevenueChangePct: null,
            PopUnitsChangePct: null,
            PreviousPeriodRevenue: null,
            PreviousPeriodUnits: null,
            HasPreviousPeriodWindow: false,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 10d);
        // Engine may return 'insufficient_data' for low revenue or 'review' for missing context; accept either
        Assert.True(res.Status == "insufficient_data" || res.Status == "review");
        Assert.Contains("previous_period_missing", res.ReasonCodes);
    }

    [Fact(DisplayName = "Missing known-margin baseline -> insufficient_data")]
    public void MissingKnownMarginBaseline_InsufficientData()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 120000m,
            TotalUnits: 240,
            ItemCount: 24,
            SharePct: 8d,
            MarginPct: 18d,
            MarginCoveragePct: 85d,
            SplitCoveragePct: 85d,
            PopRevenueChangePct: 9d,
            PopUnitsChangePct: 4d,
            PreviousPeriodRevenue: 110000m,
            PreviousPeriodUnits: 220,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: null);

        Assert.Equal("insufficient_data", res.Status);
        Assert.False(res.RecommendationAllowed);
        Assert.Contains("missing_known_margin_baseline", res.ReasonCodes);
        Assert.Contains("baseline is missing", res.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Strong growth + good margin + reliability -> increase_focus")]
    public void StrongGrowth_IncreaseFocus()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 200000m,
            TotalUnits: 500,
            ItemCount: 50,
            SharePct: 5d,
            MarginPct: 25d,
            MarginCoveragePct: 95d,
            SplitCoveragePct: 90d,
            PopRevenueChangePct: 15d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 170000m,
            PreviousPeriodUnits: 450,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 15d);
        Assert.Equal("increase_focus", res.Status);
        Assert.True(res.RecommendationAllowed);
    }

    [Fact(DisplayName = "Low margin -> review")]
    public void LowMargin_Review()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 100000m,
            TotalUnits: 200,
            ItemCount: 20,
            SharePct: 3d,
            MarginPct: 3.5d,
            MarginCoveragePct: 90d,
            SplitCoveragePct: 90d,
            PopRevenueChangePct: 2d,
            PopUnitsChangePct: 0d,
            PreviousPeriodRevenue: 98000m,
            PreviousPeriodUnits: 195,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 15d);
        Assert.Equal("review", res.Status);
    }

    [Fact(DisplayName = "Critical data quality -> do_not_trust")]
    public void CriticalDataQuality_DoNotTrust()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 20000m,
            TotalUnits: 50,
            ItemCount: 10,
            SharePct: 1d,
            MarginPct: 5d,
            MarginCoveragePct: 10d, // low -> critical
            SplitCoveragePct: 10d,
            PopRevenueChangePct: 1d,
            PopUnitsChangePct: 1d,
            PreviousPeriodRevenue: 9000m,
            PreviousPeriodUnits: 45,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 15d);
        Assert.Equal("do_not_trust", res.Status);
        Assert.False(res.RecommendationAllowed);
        Assert.Contains("missing_cost_coverage", res.ReasonCodes);
    }

    [Fact(DisplayName = "Unknown-heavy dataset -> reason unknown_heavy_dataset")]
    public void UnknownHeavyDataset_ReasonIncluded()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 20000m,
            TotalUnits: 60,
            ItemCount: 8,
            SharePct: 2d,
            MarginPct: 10d,
            MarginCoveragePct: 80d,
            SplitCoveragePct: 80d,
            PopRevenueChangePct: 3d,
            PopUnitsChangePct: 1d,
            PreviousPeriodRevenue: 19000m,
            PreviousPeriodUnits: 58,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 20d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 12d);
        Assert.Contains("unknown_heavy_dataset", res.ReasonCodes);
    }

    [Fact(DisplayName = "RL04 lifecycle capture stays orthogonal to decision engine status")]
    public void RecommendationLifecycle_IssuedIsNotLearningEvidence()
    {
        // Decision engine recommendation status remains independent from learning eligibility.
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 200000m,
            TotalUnits: 500,
            ItemCount: 50,
            SharePct: 5d,
            MarginPct: 25d,
            MarginCoveragePct: 95d,
            SplitCoveragePct: 90d,
            PopRevenueChangePct: 15d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 170000m,
            PreviousPeriodUnits: 450,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var decision = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 15d);
        var lifecycle = RecommendationLifecycleSemantics.ProjectIssuedRecommendation();

        Assert.Equal("increase_focus", decision.Status);
        Assert.Equal(RecommendationLifecycleSemantics.LifecycleStates.Issued, lifecycle.LifecycleState);
        Assert.False(lifecycle.LearningEligible);
        Assert.DoesNotContain(lifecycle.LearningEligibilityReasonCodes, code => code == "measured_learning_eligible");
    }

    [Fact(DisplayName = "Null split coverage fails closed — not treated as measured zero")]
    public void NullSplitCoverage_FailsClosed()
    {
        var withNullSplit = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 200000m,
            TotalUnits: 500,
            ItemCount: 50,
            SharePct: 5d,
            MarginPct: 25d,
            MarginCoveragePct: 95d,
            SplitCoveragePct: null,
            PopRevenueChangePct: 15d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 170000m,
            PreviousPeriodUnits: 450,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var withZeroSplit = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 200000m,
            TotalUnits: 500,
            ItemCount: 50,
            SharePct: 5d,
            MarginPct: 25d,
            MarginCoveragePct: 95d,
            SplitCoveragePct: 0d,
            PopRevenueChangePct: 15d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 170000m,
            PreviousPeriodUnits: 450,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var nullResult = AnalyticsDecisionRecommendationEngine.Evaluate(withNullSplit, averageMarginPct: 15d);
        var zeroResult = AnalyticsDecisionRecommendationEngine.Evaluate(withZeroSplit, averageMarginPct: 15d);

        Assert.Equal("insufficient_data", nullResult.Status);
        Assert.False(nullResult.RecommendationAllowed);
        Assert.Contains("missing_split_coverage", nullResult.ReasonCodes);
        Assert.NotEqual(nullResult.Status, zeroResult.Status);
        Assert.DoesNotContain("missing_split_coverage", zeroResult.ReasonCodes);
    }

    [Fact(DisplayName = "Null margin coverage fails closed as critical / not trusted")]
    public void NullMarginCoverage_FailsClosed()
    {
        var input = new AnalyticsDecisionRecommendationEngine.RecommendationInput(
            IsUnknownEntity: false,
            TotalRevenue: 200000m,
            TotalUnits: 500,
            ItemCount: 50,
            SharePct: 5d,
            MarginPct: 25d,
            MarginCoveragePct: null,
            SplitCoveragePct: 90d,
            PopRevenueChangePct: 15d,
            PopUnitsChangePct: 10d,
            PreviousPeriodRevenue: 170000m,
            PreviousPeriodUnits: 450,
            HasPreviousPeriodWindow: true,
            IsNewEntity: false,
            UnknownBucketSharePct: 0d);

        var res = AnalyticsDecisionRecommendationEngine.Evaluate(input, averageMarginPct: 15d);

        Assert.False(res.RecommendationAllowed);
        Assert.Contains("missing_cost_coverage", res.ReasonCodes);
        Assert.Equal("critical", res.DataQualityStatus);
        Assert.True(res.Status is "do_not_trust" or "insufficient_data");
    }
}
