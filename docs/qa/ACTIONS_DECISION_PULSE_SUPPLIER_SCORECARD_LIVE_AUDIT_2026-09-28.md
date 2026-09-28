# Live audit: Actions, Decision Pulse and Supplier Scorecard

Date: 2026-09-28  
Queue: `direct-user-request`  
Target deployment: `https://trendplus.vercel.app`  
API host used for evidence: `https://trendplus-api.onrender.com`  

## Scope and method

Reviewed the requested routes:

- [Actions](https://trendplus.vercel.app/analytics/actions)
- [Decision Pulse](https://trendplus.vercel.app/analytics/decision-pulse)
- [Supplier Scorecard](https://trendplus.vercel.app/analytics/supplier?tab=scorecard)

The audit combined live HTTP responses, the deployed frontend bundle, current `main` source and the nearest focused contracts. The native browser helper could not initialize in this environment even after the permitted recovery attempt, so pixel-level layout, hover, responsive and click-flow conclusions are not classified as confirmed. The API and source findings below are independently reproducible and are not inferred from a dead host.

## Live evidence

| Surface | Request/result | Interpretation |
|---|---|---|
| Actions list | `GET /api/analytics/actions?page=1&pageSize=50` → HTTP 200; `totalCount=4`; all four rows are dated 2026-05-22 and use `Smoke...` titles | The live operational queue contains old smoke fixtures, not an evidently useful current action population. This is a data-hygiene/release finding, not a basis for destructive deletion. |
| Actions counts | `GET /api/analytics/actions/counts` → `{new:3, accepted:0, deferred:0, rejected:0, done:1, p1Open:1}` | Counts are global and have no filter/period metadata. They can disagree with a filtered list and are not visibly labelled as global. |
| Actions outcome summary, default | `GET /api/analytics/actions/outcomes/summary` → default created window 2026-06-30 through 2026-09-28, `sampleSize=0`, `emptyReason=no actions for selected filters` | The list shows four May rows while the summary says there are no actions in the default last-90-day window. The page does not expose a single shared period control that explains this difference. |
| Actions outcome summary, wide window | May 1–September 28 → `sampleSize=4`, `closedCount=1`, `openCount=3`, `measuredSampleSize=0`, `outcomeCoverageRate=0` | The backend correctly exposes pending measurement for these fixtures, but the page combines legacy totals with a separate measurement-statistics projection; denominator semantics need one authoritative contract. |
| Decision Pulse | `GET /api/analytics/decision-pulse/` → HTTP 200; `suppressedCount=124`, `items=[]`, `meta.success=true`, `meta.isPartial=true`, `warningCode=PULSE_PARTIAL`, warning “Supplier decision hub nije dostupan.” | This is a partial source failure with an empty post-suppression result, not an ordinary trustworthy empty state. The page currently renders the empty message and only a small metadata sentence, without a prominent partial/retry treatment. |
| Supplier Scorecard | `GET /api/analytics/suppliers/decision-hub/summary?...&dataScope=all` → HTTP 200 transport, `meta.success=false`, `errorCode=MISSING_SCHEMA`, requested 30d/effective 90d, `rowCount=0`, `recommendationAllowed=false` | The scorecard fails closed, which is correct, but the underlying readiness/recovery owner remains the existing `RQ475`; no duplicate prompt is created here. |

## Confirmed findings and queue routing

| ID | Finding | Impact | Queue owner |
|---|---|---|---|
| F1 | Actions list has no period parameter, outcome summary defaults to the last 90 created days, and counts are unfiltered/global | The list, KPI cards and outcome panel can describe different populations without a visible denominator or period contract | `RQ477` |
| F2 | `BuildSummaryAggregate` treats `not_measured` as measured because it excludes only normalized `pending`; the legacy positive/negative rates and closed-measured count therefore use a contaminated denominator | Measurement coverage and outcome rates can be overstated or mathematically disagree with the separate `measurementStatistics` projection | `RQ478` |
| F3 | Live Actions contains four old smoke fixtures with null quality/impact/evidence fields | A pilot/production-looking queue can show test data as operational work and distort counts; removal must not be done without an authorized data/deployment owner | `RQ479` |
| F4 | Decision Pulse returns `success=true` with `isPartial=true` and a Supplier source warning, but the page does not visibly treat partial data as a warning when the result is empty or populated | Users can interpret an incomplete feed as a complete “no action” conclusion | `RQ480` |
| F5 | Decision Pulse page calls `getDecisionPulse()` without period, scope, store or supplier options even though the API client supports them; the service also calls the inventory workflow without date/scope arguments | Pulse can be out of line with the shared scope/period used by comparable decision surfaces, especially Decision Board | `RQ481` |
| F6 | Supplier-origin Pulse candidates deep-link to `/analytics/supplier?tab=overview`, while the source is the Supplier Decision Hub/scorecard; raw freshness/DQ/tenant codes are rendered directly and period/generated/suppressed metadata is not exposed | A user can land on the wrong evidence surface and misread internal codes or the completeness of the feed | `RQ482` |
| F7 | Supplier Scorecard currently has a transport-200/semantic-failure `MISSING_SCHEMA` response with no data | This is confirmed again, but it is already owned by `RQ475` (schema/readiness/error/empty/retry contract) and is not duplicated | Existing `RQ475` |
| F8 | Supplier share denominator differences remain visible between raw API, overview and decision hub | This is confirmed again, but the existing owner `RQ476` already covers the raw/display/recommendation/export contract | Existing `RQ476` |

## Source evidence

### Actions

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` loads list data and outcome summary through separate requests. The page subtitle explicitly says status and text search apply only to the list, but it does not expose the summary's independent default date window.
- `Klijent/clientapp/src/services/analyticsApi.ts` supports list filters but `getAnalyticsActionCounts()` has no filter/period input. `getAnalyticsActionOutcomeSummary()` supports date filters that the page does not bind to a shared period control.
- `Api/Endpoints/AnalyticsActionsEndpoints.cs` gives the outcome summary a default created window of `DateTime.UtcNow.AddDays(-90)` through `DateTime.UtcNow`, while the list has no equivalent period contract.
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs:BuildSummaryAggregate` uses `NormalizeOutcomeStatus(x.OutcomeStatus) != Pending` as its measured predicate. The separate `RecommendationMeasurementStatisticsProjection` already uses the stricter measured lifecycle contract and is the reference for `RQ478`.

### Decision Pulse

- `Api/Services/Analytics/DecisionPulseService.cs` deliberately preserves partial-source metadata, but the no-item branch still returns a successful empty response with `isPartial=true`.
- `Klijent/clientapp/src/pages/DecisionPulsePage.tsx` checks `meta.success` but not `meta.isPartial`/`warningCode` as a blocking or warning state, has no retry action in the empty/error view, and renders raw `inputFreshnessStatus`, `dataQualityStatus` and `tenantScope` values.
- `Klijent/clientapp/src/services/decisionPulseApi.ts` accepts `fromDate`, `toDate`, `storeId`, `supplierId` and `dataScope`, but the page does not pass them.
- `Application/Analytics/DecisionPulse/DecisionPulseProjector.cs` emits Supplier deep links to `tab=overview` even when the source is the Supplier decision hub.
- `ExecutiveDecisionBoardPage.tsx` is the nearest comparison: it carries shared data scope and visibly renders partial-state warnings. Decision Pulse should align with that trust contract without copying decision logic into the frontend.

### Supplier Scorecard

- `SupplierDecisionHubPage.tsx` correctly routes `meta.success=false` through a retryable analytics error state with correlation information.
- The current live `MISSING_SCHEMA`/90d fallback/readiness failure remains the scope of `RQ475`; the audit does not invent a second scorecard owner.

## Cross-screen comparison

| Contract | Actions | Decision Pulse | Supplier Scorecard | Comparable reference |
|---|---|---|---|---|
| Period | List has no period; outcome summary silently defaults to 90 created days | Service defaults to a 30-day calendar window; page sends no explicit period | Requested/effective period is present in backend trust metadata | Decision Board exposes the period/scope used by its aggregate |
| Population | List rows, global counts and outcome-summary population are separate | Candidates are post-filter/post-suppression; 124 suppressed candidates are not otherwise visible in the main state | Backend-owned decision cohort; currently unavailable | Product/Supplier pages are expected to show analyzed/visible/decision cohorts explicitly |
| Scope | List/count requests do not share one visible scope contract | Client supports scope but page omits it; inventory source path does not receive date/scope | `dataScope=all` is recorded, but readiness fails | Decision Board uses shared `dataScope` and surfaces partial state |
| Failure vs empty | Outcome empty and list data can look jointly healthy despite different windows | `success=true` plus `isPartial=true` can look like a normal empty result | Semantic failure is correctly fail-closed but operationally unavailable | Analytics invariant: empty is not error, and partial must remain visible |
| Labels/provenance | Legacy outcome totals sit beside measurement statistics | Raw internal codes and incomplete source details are visible | Trust metadata exists but rows are absent | Shared Serbian status/quality mappings and explicit requested/effective/freshness labels |

The alignment principle is not that every screen must show identical numbers. Each screen should state its requested/effective period, population, denominator, scope, freshness and failure/partial status so that differences are explainable rather than accidental.

## Existing work not duplicated

- `RQ475` remains the owner for Supplier Scorecard/assortment semantic-data readiness, missing schema, fail-closed recommendation state and recovery UX.
- `RQ476` remains the owner for Supplier share denominator parity across raw API, overview, decision hub, recommendation and export.
- `RQ469`–`RQ474` are earlier Product Decision/Supplier report follow-ups and are not silently repurposed by this audit. `RQ477`/`RQ478` are limited to the Actions list/outcome contract and its measurement math; `RQ480`–`RQ482` are limited to Decision Pulse feed trust/lineage.
- No browser-render-only issue is promoted because the native browser helper was unavailable. A later visual pass should inspect responsive layout, filter interaction and deep-link behavior after runtime access is restored.

## Queue delivery

Six bounded prompts were registered as `RQ477`–`RQ482`, all `WAITING`. This was a direct audit and queue-registration request; no prompt was claimed or promoted. The existing primary RQ pointer remains `RQ461`; existing parallel READY entries are unchanged.
