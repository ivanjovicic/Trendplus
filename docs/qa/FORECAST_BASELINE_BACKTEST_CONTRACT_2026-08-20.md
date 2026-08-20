# Forecast baseline and backtesting contract (RQ98)

Date: 2026-08-20  
Repo: `ivanjovicic/Trendplus`

## Verdict

RQ98 defines the **first deterministic baseline/backtesting contract** and a fail-closed evaluation API. On current `main`, a trustworthy paired forecast-vs-outcome **comparison window does not exist** (RQ97: no trusted forecast materializer). Measured WAPE/bias/MAE are therefore **null / unavailable**, never invented as `0`.

## Allowed baselines

| Id | Meaning |
|---|---|
| `naive_last_period` | Primary baseline: last comparable period demand (horizon-aligned). |
| `seasonal_naive` | Same season/period prior year or prior comparable season when evidence exists. |

No ML models are in scope for this contract.

## Horizons and metrics

- Horizons: **7 / 14 / 28** days only.
- Metrics: **WAPE**, **bias**, **MAE** (retail-appropriate bounded set).
- Aggregates must stay null when evidence is missing (no fake-zero quality).

## Cohorts (must stay explicit)

| Id | Meaning |
|---|---|
| `sufficient_history` | Enough paired history for the chosen horizon. |
| `sparse` | Intermittent / low-count demand. |
| `new_item` | Insufficient item history; pooling policy later. |
| `no_history` | No usable history; excluded from measured aggregates. |

Sparse/new-item/no-history must not be silently folded into headline scores.

## Comparison window requirements (not met on current main)

A ready window requires all of:

1. Trusted forecast materializer / provenance (`trusted` from RQ97) writing dated forecast snapshots.
2. Observed outcomes (sales and/or observed stock from RQ96 foundation) aligned to the same SKU/store/day keys.
3. A contiguous evaluation interval with paired forecast-at-t and outcome-at-t+horizon rows.

Missing reasons currently emitted:

- `missing_trusted_forecast_materializer`
- `no_paired_forecast_outcome_series`
- `insufficient_observed_stock_comparison_window`

## Runtime surface

- `GET /api/analytics/cached/inventory/forecast/backtest`
- Handler: `GetForecastBaselineBacktestHandler`
- `EvaluationStatus=unavailable`, `IsAuthoritativeMeasurement=false`, `Aggregates=null`

## Usage limits

- Backtest output is **evidence only**; do not auto-promote into user-facing forecast certainty.
- Later forecast upgrades must beat `naive_last_period` on the same window/cohorts before replacing the baseline narrative.
