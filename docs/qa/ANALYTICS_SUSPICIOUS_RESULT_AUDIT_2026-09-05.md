# Analytics Suspicious-Result Audit

Date: 2026-09-05
Repository: `ivanjovicic/Trendplus`
Queue: `direct-user-request`
Owner program: Analytics Reliability (`RQ`)
Status: audit complete; implementation follow-ups are queued as `RQ139`-`RQ146` and SQL follow-up `Q83`

## Purpose

This is a static and contract-level audit of analytics calculations and indicators that can look trustworthy without sufficient evidence. It is not a claim that the listed behavior is already fixed or proven in a live deployment.

## Confirmed findings

| Area | Confirmed evidence | Risk | Follow-up |
|---|---|---|---|
| Frontend derived intelligence | `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:155-191`, `:217-260`, `:280-350`, `:377-418` uses fallback zeroes, approximate velocity/revenue, default cost/price and local reorder calculations | Missing signals can become revenue, margin, forecast, risk, confidence-shaped or actionable values | `RQ139`, `RQ143`, `RQ145` |
| Trend momentum | `Application/Analytics/Services/TrendScoringService.cs:216-217` returns `1.0` when today/yesterday score is missing | Missing history is presented as positive momentum | `RQ139` |
| Trend index | `Application/Analytics/Services/TrendScoringService.cs:259-271` filters to positive scores and returns `0.0` when no usable score remains | Empty/unsupported evidence can look like a measured zero index unless state metadata travels with it | `RQ139`, `RQ142` |
| Inventory order quantity | `Application/Analytics/Services/TrendScoringService.cs:347-362` returns zero for non-positive velocity; frontend `:377-418` then derives reorder state and probability | True zero demand, unavailable velocity and invalid input are not demonstrably distinct | `RQ139`, `RQ143` |
| Pre/nivelacija scenarios | `Api/Services/PreNivelacijaScoringService.cs:117-134`, `:208-214` applies smoothing and clamps scenario units to at least one | No-evidence or no-stock scenario can create a positive expected result | `RQ139`, `RQ140` |
| Pre/nivelacija reliability | `Api/Services/PreNivelacijaScoringService.cs:223-240` maps zero/invalid span to midpoint and unknown confidence to `50%` | Unknown basis can look medium-confidence | `RQ139`, `RQ140` |
| Nivelacija SQL compatibility | `Api/Endpoints/AllEndpoints.cs:3227-3232`, `:3338-3346`, `:3401-3409` falls back from revenue change to quantity change and coalesces missing coverage/change fields to zero | Quantity and revenue effects can be conflated; missing coverage can look measured | `Q83`, `RQ140`, `RQ146` |
| Data Quality health | `Infrastructure/Services/AnalyticsDataQualityHealthService.cs:145-171` emits zero share when total revenue is zero; `Klijent/clientapp/src/pages/DataQualityPage.tsx:655-662` applies `?? 0` in threshold checks | No denominator can look healthy/green | `RQ144` |
| Period/refresh lineage | `RQ137` completion note states that only selected dashboard/readiness/report paths were revalidated; `Infrastructure/Seed/DatabaseInitializer.cs:2102-2107` continues after migration failure and `:458-478` leaves heavy refresh to the worker | Other pages may show query generation or fallback data as freshness, or hide schema/refresh failure | `RQ141`, `RQ146` |
| Raw code mapping | `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` contains `OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code` | Internal warning code can leak into user-facing copy | `RQ145` |

## Previously repaired, not re-opened as already complete

- Commit `29a5943ad606c67721e931d73fd5906b49c9ade3` added trust metadata and repaired selected Dashboard, Pilot Readiness, Supplier report, Daily Sales, Supplier and Shoe Type paths.
- `RQ137` added requested/effective/observed period lineage for selected surfaces, but its own residual-risk note excludes the rest of analytics.
- `RQ138` replaced Trend Models placeholders with a fail-closed contract, but its completion note says no measured production evaluation source is materialized yet.
- The existing focused tests cover several selected null/empty/metadata regressions. They do not prove the full cross-route formula, schema, cache, export/report or live-refresh matrix.

## Screen coverage and proof status

The following is a routing scope inventory for the follow-up work. “Unproven” means that this audit did not find a complete current-main proof for every lineage, formula, schema, cache, refresh and parity edge on that surface; it does not mean the route is necessarily broken.

| Screen/family | React/API/backend lineage | Formula/state proof | Queue |
|---|---|---|---|
| `/analytics` | Partial, selected dashboard lineage exists | Cross-family denominator, ranking and measured trend evaluation unproven | `RQ139`, `RQ141`, `RQ142`, `RQ143`, `RQ145` |
| `/analytics/products` | Partial product/intelligence paths | Frontend-derived margin/velocity/reorder and null semantics unproven | `RQ139`, `RQ143`, `RQ145` |
| `/analytics/supplier` | Supplier summary/report paths partly repaired | Full scope, quality thresholds, pre/post and parity unproven | `RQ139`, `RQ140`, `RQ141`, `RQ143`, `RQ145` |
| `/analytics/inventory` | Inventory status/forecast/recommendation paths exist | Missing velocity, OOS, stock value and action ranking semantics unproven | `RQ139`, `RQ142`, `RQ143`, `RQ141` |
| `/analytics/actions` | Actions/outcome endpoint and UI exist | Raw-code mapping, outcome denominator and recommendation evidence parity unproven | `RQ139`, `RQ143`, `RQ145` |
| `/analytics/decision-board` | Backend board payload and selected trust cleanup exist | Full local ranking/threshold and cross-family parity unproven | `RQ139`, `RQ143`, `RQ145` |
| `/analytics/data-quality` | Health and issue endpoints exist | No-denominator versus valid-zero health and schema/refresh failure proof unproven | `RQ139`, `RQ141`, `RQ144`, `RQ146` |
| `/analytics/reports` | Selected supplier/report lineage exists | Full export/report parity, source freshness and blocked-action behavior unproven | `RQ141`, `RQ145` |
| Sales summaries/daily/category/gender/payment/hour | Cached and legacy redirect families exist | Shared metric state, scope/cache key and empty/error parity unproven | `RQ139`, `RQ141`, `RQ145`, `RQ146` |
| Trend/momentum/index/Trend Models | Trend scoring and fail-closed evaluation contract exist | Null momentum and measured evaluation source unproven | `RQ139`, `RQ142` |
| Forecast/inventory forecast | Materializer/backtest foundations exist | Actual pairing, cutoff, horizon, metrics and stale/partial proof unproven | `RQ139`, `RQ142`, `RQ146` |
| Pre/post nivelacija, vendor, color, shoe type | Views/endpoints and selected meta tests exist | Comparable cohort, availability confounding, coverage and revenue/quantity parity unproven | `RQ139`, `RQ140`, `RQ141`, `RQ146` |

## Required proof cases

The follow-ups must cover, with expected output and state, all of these cases:

- empty successful result, null, genuine valid zero, missing denominator, NaN and Infinity;
- stale and unknown freshness, partial/fallback response, failed refresh and skipped/unregistered worker;
- wrong period, wrong scope, endpoint 404 and missing table/view/column/migration;
- export/table/chart/detail/report parity and safe unknown-code messaging;
- dark, light and soft-gray themes;
- chart initial width/height `0` and `-1`, including no console warning/error.

## Non-proof boundaries

- Static code inspection does not prove production data correctness.
- Unit tests do not prove SQL view shape, migration application, cache invalidation or worker registration unless those paths are explicitly exercised.
- A local build does not prove live freshness, refresh success, deployment SHA or provider behavior.
- No prompt should convert these findings into a `DONE` claim without backend, frontend and regression-test evidence.

## Follow-up order

1. `RQ139` establishes the shared numeric state/denominator contract.
2. `RQ140` repairs and proves pre/post nivelacija comparability.
3. `RQ141` builds the full screen lineage/scope/cache/refresh matrix.
4. `Q83` proves the raw SQL nivelacija nullability/baseline contract before cross-layer repair.
5. `RQ142` materializes measured forecast/trend evaluation.
6. `RQ143` removes frontend decision/ranking invention.
7. `RQ144` closes Data Quality health denominator semantics.
8. `RQ145` proves cross-surface parity and safe messaging.
9. `RQ146` proves schema, endpoint and refresh-failure runtime behavior.

All eight remain `WAITING` because the active RQ queue currently declares `Current READY prompt: none`. Promotion requires the canonical queue/router rules and must not bypass `STAB16` live worker/freshness gates.
