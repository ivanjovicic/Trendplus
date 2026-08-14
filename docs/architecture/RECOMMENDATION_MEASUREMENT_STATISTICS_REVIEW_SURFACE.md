# Recommendation Measurement Statistics Review Surface Contract

Status: frozen contract for RL07
Date: 2026-08-14
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related source contract: `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`
Related rollout plan: `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md` Slice 3
Related runtime projection: `GET /api/analytics/actions/outcomes/summary` field `measurementStatistics`
Related helper: `Application/Analytics/RecommendationMeasurementStatisticsProjection.cs`

## Purpose

This contract freezes how an operator review surface, dashboard panel, print view and export may present measurement-only recommendation statistics.

The surface is a consumer of `measurementStatistics`. It does not own counts, rates, confidence or outcome meaning. It must show the same denominators as the RL06 projection and must not hide evidence gaps.

## Non-goals

- no runtime UI in this prompt
- no confidence calibration or mutation
- no schema migration
- no frontend-local rates, percentages or funnel math
- no reuse of legacy `totals` rates as success
- no treating acceptance or execution as success

## Source of truth

| Layer | Authority |
|---|---|
| Count and rate meaning | `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md` |
| Runtime numbers | `RecommendationMeasurementStatisticsDto` on `GET /api/analytics/actions/outcomes/summary` |
| Period, freshness and empty envelope | `AnalyticsActionOutcomeSummaryDto.Meta` |
| Legacy closed/open totals | `AnalyticsActionOutcomeSummaryDto.Totals` — workflow volume only, never success |

If `measurementStatistics` is missing, the review surface is an error or insufficient state, not a chance to compute rates from `totals`.

## Required header context

The review surface header must show backend-owned context before any funnel or rate:

| Context | Source | Operator meaning |
|---|---|---|
| Period | `meta.periodMode`, `createdFrom`/`createdTo`, `resolvedFrom`/`resolvedTo`, `measuredFrom`/`measuredTo` | Which window the cohort uses |
| Freshness | `meta.generatedAtUtc` | When the projection was generated |
| Sample | `measurementStatistics.issuedCount` plus `meta.sampleSize` if shown as workflow volume | Funnel baseline is issued, not closed |
| Warnings | `measurementStatistics.warningCodes` and `meta.warnings` | Caveats stay visible |
| Empty reason | `measurementStatistics.emptyReason` or `meta.emptyReason` | Empty is not error |

Do not invent a local freshness clock or a local sample-size floor.

## Review-surface fields

Bind these fields from `measurementStatistics` only.

### Lifecycle funnel

Display order is issued, then accepted / rejected / ignored, then executed.

| Field | Operator label | Must not be read as |
|---|---|---|
| `issuedCount` | Izdato | Success |
| `acceptedCount` | Prihvaćeno | Success |
| `rejectedCount` | Odbijeno | Failure of the recommendation quality |
| `ignoredCount` | Ignorisano | Failure |
| `executedCount` | Izvršeno | Success |

Acceptance rate and execution rate may be shown only as `acceptanceRate` (`acceptedCount / issuedCount`) and `executionRate` (`executedCount / acceptedCount`). A missing rate stays empty (`Nije dostupno`), never `0%`.

### Measurement coverage

| Field | Numerator / denominator | Operator label |
|---|---|---|
| `measuredCount` | measured rows | Izmereno |
| `notMeasuredCount` | not_measured rows | Nije izmereno |
| `pendingCount` | pending rows | Čeka merenje |
| `measurementCoverageRate` | `measuredCount / executedCount` | Pokrivenost merenjem |
| `notMeasuredShare` | `notMeasuredCount / executedCount` | Udeo bez merenja |

`not_measured` is a first-class gap. It is not a hidden zero and not a negative outcome.

### Outcome distribution

Eligible only for measured rows.

| Field | Numerator / denominator | Operator label |
|---|---|---|
| `successCount` | success / measured | Pozitivan ishod |
| `neutralCount` | neutral / measured | Neutralan ishod |
| `negativeCount` | negative / measured | Negativan ishod |
| `positiveOutcomeRate` | `successCount / measuredCount` | Stopa pozitivnih ishoda |
| `neutralOutcomeRate` | `neutralCount / measuredCount` | Stopa neutralnih ishoda |
| `negativeOutcomeRate` | `negativeCount / measuredCount` | Stopa negativnih ishoda |

Positive outcome rate is not a lifecycle success rate. It must never be labeled as uspeh prihvatanja or uspeh izvršenja.

## Forbidden bindings

The review surface must not:

1. show `totals.positiveOutcomeRate`, `totals.outcomeCoverageRate` or their aliases as the measurement success story;
2. divide success by issued, accepted, executed or closed rows in the client;
3. render a null rate as `0%` or `0 RSD`;
4. convert `pending` into failure or `not_measured` into success/neutral/negative;
5. mutate or display a locally derived confidence from these counts.

Legacy `totals` may appear in a separate "obim toka" block only if labeled as created/closed/open workflow volume.

## Empty, insufficient, warning and error

| Backend signal | Surface | KPI / rates |
|---|---|---|
| Summary load failure or `meta.success=false` | shared `AnalyticsErrorState` | hidden; no zeros |
| `measurementStatistics` missing on an otherwise successful summary | warning or error, not a local recompute | hidden |
| `success=true` and `emptyReason=no_rows` | shared `AnalyticsEmptyState` | hidden |
| `warningCodes` contains `small_sample` or `small_measured_sample` | visible warning; rates may be shown as insufficient, not trusted | no fake-green |
| `warningCodes` contains `outcome_coverage_low` | visible warning on coverage | coverage stays visible as a gap |
| `warningCodes` contains `rejected_actions_present` | visible caveat | rejected stays a funnel state |

Empty remains empty. Error remains error. Insufficient remains insufficient.

## Print and export

Print and export are the same contract with a document layout.

- export rows must copy backend counts and nullable rates; they must not recompute percentages;
- a failed export or print must show a graceful warning and leave the on-screen review intact;
- a failed export must not write a CSV of zeros that looks like a measured empty cohort;
- export column headers should preserve issued/accepted/executed versus measured/success wording so acceptance cannot be imported as success.

## Warning vocabulary

Reuse backend codes; do not invent a parallel UI taxonomy.

| Code | Operator meaning |
|---|---|
| `small_sample` | Premali uzorak izdatih preporuka |
| `small_measured_sample` | Premalo izmerenih ishoda |
| `outcome_coverage_low` | Merenje ne pokriva dovoljno izvršenih akcija |
| `rejected_actions_present` | U kohorti ima odbijenih akcija; to nije negativan ishod |

Unknown codes stay visible as backend warnings, not as a healthy default.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize a new API shape.
- This document does not authorize confidence calibration (Slice 4).
- If a later UI cannot bind a frozen field, the gap stays visible instead of being synthesized from `totals`.
