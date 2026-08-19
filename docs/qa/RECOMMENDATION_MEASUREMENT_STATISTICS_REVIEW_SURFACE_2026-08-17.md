# Recommendation Measurement Statistics Review Surface â€” RL09

Date: 2026-08-17
Repo: `ivanjovicic/Trendplus`
Prompt: `RL09`
Contract: `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md` (`RL07`)
Agent: cursor

## Decision

Operators can review measurement-only recommendation statistics on Centralne akcije without treating acceptance, execution or legacy `totals` rates as success. Funnel, coverage and outcome rates bind only to `measurementStatistics` from `GET /api/analytics/actions/outcomes/summary`.

## Surface

Runtime panel: `Klijent/clientapp/src/components/analytics/RecommendationMeasurementStatisticsReview.tsx`
Host page: `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
View/export helpers: `Klijent/clientapp/src/utils/recommendationMeasurementStatistics.ts`

Shared chrome: `AnalyticsTrustHeader`, `AnalyticsErrorState`, `AnalyticsEmptyState`.

## Binding rules proven

| Signal | Surface | Rates |
|---|---|---|
| Summary load failure | `AnalyticsErrorState` | hidden |
| `measurementStatistics` missing | `AnalyticsErrorState` (`missing_statistics`) | hidden; no local % from `totals` |
| `emptyReason=no_rows` | `AnalyticsEmptyState` | hidden; not `0%` |
| Null rate | `Nije dostupno` | never `0%` |
| Export while empty/error | warning, no CSV blob | on-screen review intact |
| Legacy `totals` | separate "Obim toka, nije uspeh" counts only | coverage/success aliases not used as measurement success |

## Tests

Focused Vitest, non-watch:

- `src/utils/__tests__/recommendationMeasurementStatistics.spec.ts` â€” 6/6
- `src/components/analytics/__tests__/RecommendationMeasurementStatisticsReview.spec.tsx` â€” 6/6
- existing Analytics Actions page specs still pass with `measurementStatistics` fixtures

## Out of scope

- confidence calibration
- frontend-local funnel math
- changing backend projection or API shape
- Pilot Readiness / Executive Board still may show legacy `totals` coverage copy outside this panel
