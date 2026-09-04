using MediatR;

namespace Application.Analytics.Queries.GetForecastBaselineBacktest;

/// <summary>
/// Fail-closed baseline/backtest evaluator for current main.
/// Forecast snapshot and observed pairing foundations exist, but the dashboard still has
/// no authoritative evaluation snapshot that would justify numeric model-quality claims.
/// </summary>
public sealed class GetForecastBaselineBacktestHandler
    : IRequestHandler<GetForecastBaselineBacktestQuery, ForecastBaselineBacktestDto>
{
    public Task<ForecastBaselineBacktestDto> Handle(
        GetForecastBaselineBacktestQuery request,
        CancellationToken cancellationToken)
    {
        var horizon = ForecastBaselineBacktestContract.HorizonDays.Contains(request.HorizonDays)
            ? request.HorizonDays
            : 14;

        var missingReasons = new[]
        {
            ForecastBaselineBacktestContract.ReasonMissingAuthoritativeEvaluationSnapshot
        };

        var metrics = BuildMetricDefinitions();

        var cohorts = ForecastBaselineBacktestContract.AllowedCohorts
            .Select(id => new ForecastBaselineBacktestCohortDto(
                CohortId: id,
                SkuStoreCount: 0,
                Aggregates: null,
                Note: "Cohort counts remain unavailable until an authoritative evaluation snapshot is materialized."))
            .ToList();

        var dto = new ForecastBaselineBacktestDto(
            GeneratedAtUtc: DateTime.UtcNow,
            EvaluationStatus: ForecastBaselineBacktestContract.EvaluationUnavailable,
            IsAuthoritativeMeasurement: false,
            ComparisonWindowStatus: "unavailable",
            EvaluationFreshnessStatus: ForecastBaselineBacktestContract.FreshnessUnknown,
            LastEvaluatedAtUtc: null,
            WindowStartUtc: null,
            WindowEndUtc: null,
            HorizonDays: horizon,
            PrimaryBaselineId: ForecastBaselineBacktestContract.BaselineNaiveLastPeriod,
            PrimaryBaselineLabel: "Naivni poslednji period",
            AllowedBaselineIds: ForecastBaselineBacktestContract.AllowedBaselines,
            AllowedMetricIds: ForecastBaselineBacktestContract.AllowedMetrics,
            AllowedCohortIds: ForecastBaselineBacktestContract.AllowedCohorts,
            MissingEvidenceReasons: missingReasons,
            Metrics: metrics,
            Cohorts: cohorts,
            Aggregates: null,
            Warning:
                "Trend model evaluation contract exists, but no authoritative evaluation snapshot is available " +
                "for the selected scope. Forecast snapshots and observed pairing foundations exist; measured " +
                "quality aggregates still must remain unavailable instead of synthetic zeros.");

        return Task.FromResult(dto);
    }

    private static IReadOnlyList<ForecastBaselineBacktestMetricDto> BuildMetricDefinitions()
    {
        const string limitation =
            "Metrika nije prikazana dok backend ne materijalizuje autoritativni evaluacioni snapshot.";

        return
        [
            new ForecastBaselineBacktestMetricDto(
                MetricId: ForecastBaselineBacktestContract.MetricWape,
                Label: "WAPE",
                DisplayKind: ForecastBaselineBacktestContract.MetricDisplayPercent,
                UnitLabel: "%",
                Value: null,
                IsAvailable: false,
                Limitation: limitation),
            new ForecastBaselineBacktestMetricDto(
                MetricId: ForecastBaselineBacktestContract.MetricBias,
                Label: "Bias",
                DisplayKind: ForecastBaselineBacktestContract.MetricDisplaySignedPercent,
                UnitLabel: "%",
                Value: null,
                IsAvailable: false,
                Limitation: limitation),
            new ForecastBaselineBacktestMetricDto(
                MetricId: ForecastBaselineBacktestContract.MetricMae,
                Label: "MAE",
                DisplayKind: ForecastBaselineBacktestContract.MetricDisplayNumber,
                UnitLabel: "jed.",
                Value: null,
                IsAvailable: false,
                Limitation: limitation)
        ];
    }
}
