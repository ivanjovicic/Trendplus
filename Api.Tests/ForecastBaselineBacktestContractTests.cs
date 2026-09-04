using Application.Analytics.Queries.GetForecastBaselineBacktest;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class ForecastBaselineBacktestContractTests
{
    [Fact(DisplayName = "Backtest contract stays unavailable without inventing zero quality scores")]
    public async Task Handler_ComparisonWindowUnavailable_DoesNotInventScores()
    {
        var handler = new GetForecastBaselineBacktestHandler();

        var result = await handler.Handle(new GetForecastBaselineBacktestQuery(HorizonDays: 14), CancellationToken.None);

        Assert.Equal(ForecastBaselineBacktestContract.EvaluationUnavailable, result.EvaluationStatus);
        Assert.False(result.IsAuthoritativeMeasurement);
        Assert.Equal("unavailable", result.ComparisonWindowStatus);
        Assert.Equal(ForecastBaselineBacktestContract.FreshnessUnknown, result.EvaluationFreshnessStatus);
        Assert.Null(result.LastEvaluatedAtUtc);
        Assert.Null(result.Aggregates);
        Assert.Null(result.WindowStartUtc);
        Assert.Null(result.WindowEndUtc);
        Assert.Equal(14, result.HorizonDays);
        Assert.Equal(ForecastBaselineBacktestContract.BaselineNaiveLastPeriod, result.PrimaryBaselineId);
        Assert.Equal("Naivni poslednji period", result.PrimaryBaselineLabel);
        Assert.Contains(ForecastBaselineBacktestContract.ReasonMissingAuthoritativeEvaluationSnapshot, result.MissingEvidenceReasons);
        Assert.DoesNotContain(ForecastBaselineBacktestContract.ReasonMissingTrustedForecastMaterializer, result.MissingEvidenceReasons);
        Assert.Equal(ForecastBaselineBacktestContract.AllowedMetrics.Count, result.Metrics.Count);
        Assert.All(result.Metrics, metric =>
        {
            Assert.False(metric.IsAvailable);
            Assert.Null(metric.Value);
            Assert.False(string.IsNullOrWhiteSpace(metric.Label));
            Assert.False(string.IsNullOrWhiteSpace(metric.DisplayKind));
        });
        Assert.Equal(ForecastBaselineBacktestContract.AllowedCohorts.Count, result.Cohorts.Count);
        Assert.All(result.Cohorts, cohort =>
        {
            Assert.Equal(0, cohort.SkuStoreCount);
            Assert.Null(cohort.Aggregates);
        });
        Assert.DoesNotContain("0.0", result.Warning ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("authoritative evaluation snapshot", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Backtest contract clamps unknown horizons to the 14d default")]
    public async Task Handler_UnknownHorizon_DefaultsTo14()
    {
        var handler = new GetForecastBaselineBacktestHandler();

        var result = await handler.Handle(new GetForecastBaselineBacktestQuery(HorizonDays: 99), CancellationToken.None);

        Assert.Equal(14, result.HorizonDays);
        Assert.Equal(ForecastBaselineBacktestContract.EvaluationUnavailable, result.EvaluationStatus);
        Assert.Null(result.Aggregates);
    }

    [Fact(DisplayName = "Backtest contract vocabulary lists retail baselines metrics and cohorts")]
    public void Contract_ExposesDeterministicVocabulary()
    {
        Assert.Contains(ForecastBaselineBacktestContract.BaselineNaiveLastPeriod, ForecastBaselineBacktestContract.AllowedBaselines);
        Assert.Contains(ForecastBaselineBacktestContract.BaselineSeasonalNaive, ForecastBaselineBacktestContract.AllowedBaselines);
        Assert.Contains(ForecastBaselineBacktestContract.MetricWape, ForecastBaselineBacktestContract.AllowedMetrics);
        Assert.Contains(ForecastBaselineBacktestContract.MetricBias, ForecastBaselineBacktestContract.AllowedMetrics);
        Assert.Contains(ForecastBaselineBacktestContract.CohortSparse, ForecastBaselineBacktestContract.AllowedCohorts);
        Assert.Contains(ForecastBaselineBacktestContract.CohortNewItem, ForecastBaselineBacktestContract.AllowedCohorts);
        Assert.Equal(new[] { 7, 14, 28 }, ForecastBaselineBacktestContract.HorizonDays);
        Assert.Equal("percent", ForecastBaselineBacktestContract.MetricDisplayPercent);
        Assert.Equal("signed_percent", ForecastBaselineBacktestContract.MetricDisplaySignedPercent);
        Assert.Equal("number", ForecastBaselineBacktestContract.MetricDisplayNumber);
    }
}
