using MediatR;

namespace Application.Analytics.Queries.GetForecastBaselineBacktest;

/// <summary>
/// Fail-closed baseline/backtest evaluator for current main:
/// RQ96 observed stock foundation exists, but RQ97 proved no trusted forecast materializer,
/// so a paired forecast-vs-outcome comparison window is not available.
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
            ForecastBaselineBacktestContract.ReasonMissingTrustedForecastMaterializer,
            ForecastBaselineBacktestContract.ReasonNoPairedForecastOutcomeSeries,
            ForecastBaselineBacktestContract.ReasonInsufficientObservedStockWindow
        };

        var cohorts = ForecastBaselineBacktestContract.AllowedCohorts
            .Select(id => new ForecastBaselineBacktestCohortDto(
                CohortId: id,
                SkuStoreCount: 0,
                Aggregates: null,
                Note: "Cohort counts unavailable until a paired forecast/outcome window exists."))
            .ToList();

        var dto = new ForecastBaselineBacktestDto(
            GeneratedAtUtc: DateTime.UtcNow,
            EvaluationStatus: ForecastBaselineBacktestContract.EvaluationUnavailable,
            IsAuthoritativeMeasurement: false,
            ComparisonWindowStatus: "unavailable",
            WindowStartUtc: null,
            WindowEndUtc: null,
            HorizonDays: horizon,
            PrimaryBaselineId: ForecastBaselineBacktestContract.BaselineNaiveLastPeriod,
            AllowedBaselineIds: ForecastBaselineBacktestContract.AllowedBaselines,
            AllowedMetricIds: ForecastBaselineBacktestContract.AllowedMetrics,
            AllowedCohortIds: ForecastBaselineBacktestContract.AllowedCohorts,
            MissingEvidenceReasons: missingReasons,
            Cohorts: cohorts,
            Aggregates: null,
            Warning:
                "Forecast baseline/backtesting contract is defined, but comparison window is unavailable. " +
                "RQ97: no trusted forecast materializer. Do not treat missing aggregates as 0 quality. " +
                "Limited basis: RQ96 observed stock foundation exists; paired forecast outcome series does not.");

        return Task.FromResult(dto);
    }
}
