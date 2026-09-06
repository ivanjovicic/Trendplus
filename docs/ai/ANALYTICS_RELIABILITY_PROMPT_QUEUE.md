# Analytics Reliability Prompt Queue

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Current READY prompt: RQ160
RQ140 was explicitly promoted by the owner after the bounded RQ139/Q83 semantic hardening and is now PARTIAL after local proof; live database/refresh/browser proof remains an external follow-up.
Owner-promoted test pack: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` (`RQ100`-`RQ105` DONE); `RQ96` DONE; `RQ106` DONE; `RQ97` DONE; `RQ98` DONE. `RQ108` is DONE on current main and `RQ109` is DONE on current main.

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: isolate analytics data-reliability work from SQL formula work. This queue targets false confidence, wrong denominators, hidden fallback states, dataScope drift and board composition errors.

## Active scope

Keep only prompts whose runtime owner is an analytics screen or the data contract that directly serves one: `/analytics`, products, suppliers, inventory, actions, Decision Board, Data Quality, reports, sales and pre/post nivelacija. Backend/API/DTO, SQL/EF, cache/refresh, export/report and focused regression tests remain in scope when they protect one of those surfaces.

Exclude from this queue: standalone forecast or Trend Models work, scenario/ML/embedding/Python work, Shopify/vendor/scraper integrations, generic security/platform/worker/migration work without a direct analytics-surface contract, and test/demo/fixture-only functionality. Tests remain required evidence for an in-scope analytics change; a prompt is excluded only when it has no production analytics behavior to protect.

Historical `DONE` entries remain as audit evidence and are not claimable. Only `READY`, `IN_PROGRESS` and `WAITING` entries are candidates for future execution.

## Queue rules

1. Start only the prompt marked `READY`.
2. Create a local uncommitted lock before work:
   - `.ai/task-locks/<task-id>-<agent>.lock.md`
3. Do not mix reliability contract tests, frontend UX and SQL formula rewrites in one task.
4. When behavior is ambiguous, add tests/docs first; do not guess a product contract.
5. Any fix that can change business output must preserve old behavior in a test fixture or explicitly document the before/after.
6. After finishing one prompt, update this queue with status, changed files, checks and the next READY prompt.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ01 | DONE | decision-board-impact-trust | Prevent wrong expected-impact fallback in board product cards |
| RQ02 | DONE | product-decision-denominators | Define PDC summary top/all-row denominator contract |
| RQ03 | DONE | lost-sales-zero-vs-unknown | Separate unavailable lost-sales evidence from true zero |
| RQ04 | DONE | data-quality-no-data | Prevent no-revenue data-quality windows from looking green |
| RQ05 | DONE | analytics-datascope-consistency | Audit dataScope semantics across analytics modules |
| RQ06 | DONE | data-quality-offender-scope | Fix top-offender revenue impact scope drift |
| RQ07 | DONE | missing-cost-offenders | Add missing-cost offender drilldown contract |
| RQ08 | DONE | supplier-blocked-signal-ranking | Cap/label blocked supplier signals in Decision Board |
| RQ09 | DONE | action-source-empty-state | Decide whether zero analytics actions is healthy empty or insufficient data |
| RQ10 | DONE | inventory-evidence-confidence | Add evidence-based confidence contract for inventory cards |
| RQ11 | DONE | transaction-stat-semantics | Clarify transaction item/line/unit count semantics |
| RQ12 | DONE | pdc-ignored-rows-contract | Make Product Decision Center ignored/top rows explicit |
| RQ13 | DONE | inventory-evidence-wiring | Wire signal confidence onto board inventory cards |
| RQ106 | DONE | decision-pulse-digest | Email + in-app exception digest after QDB06 and RQ96 |
| RQ107 | DONE | scenario-planning-contract | Freeze docs-only scenario vocabulary while runtime stays gated |
| RQ108 | DONE | forecast-materializer-observed-window | Add authoritative forecast materializer and observed pairing foundation |
| RQ109 | DONE | decision-pulse-expansion | Expand Decision Pulse beyond the first Product Decision slice |
| RQ110 | DONE | analytics-screen-data-availability | Prove pilot analytics screens stay non-empty when authoritative seeded data exists |
| RQ111 | DONE | analytics-refresh-cache-parity | Close refresh/cache/materialized-view gaps that can hide existing data |
| RQ112 | DONE | analytics-summary-detail-reconciliation | Reconcile pilot analytics summary values against detail/export on the first proven family |
| RQ113 | DONE | analytics-generation-provenance-truth | Expose exact freshness/provenance truth for the first pilot family that still looks trusted by inference |
| RQ114 | DONE | analytics-deterministic-seed-pack | Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof |
| RQ115 | DONE | analytics-dashboard-seeded-proof | Isolate dashboard seeded-data proof left open by RQ110 |
| RQ116 | DONE | decision-pulse-delivery-truth | Prove Pulse queued/sent/disabled states without claiming unverified delivery |
| RQ117 | DONE | forecast-observed-pair-availability | Prove forecast/observed pairing availability and stale/missing semantics |
| RQ118 | DONE | data-quality-issues-scope-lineage | Close the residual unscoped Data Quality issues sales window |
| RQ119 | DONE | analytics-dual-origin-scope-contract | Resolve or explicitly expose PDC/inventory dual-origin scope behavior |
| RQ120 | DONE | analytics-trust-metadata-ui-propagation | Surface source/denominator/provenance metadata in the first proven pilot UI |
| RQ121 | DONE | analytics-dashboard-row-trust-payload | Expose per-row margin/recommendation trust payload in dashboard top-product tables |
| RQ122 | DONE | supplier-decision-recommendation-trust-payload | Surface backend-owned trust state on supplier summary/quadrant/header recommendations |
| RQ123 | DONE | analytics-report-cache-generation-truth | Prove report-generation freshness/cache-version truth for pilot reports |
| RQ124 | DONE | analytics-dashboard-action-trust-payload | Expose backend-owned trust payload on dashboard legacy/advanced action cards |
| RQ134 | DONE | supplier-summary-aggregation-refresh-parity | Prove supplier summary freshness after successful aggregate refresh |
| RQ135 | DONE | data-quality-trust-propagation-after-snapshot | Refresh trust-bearing analytics caches after data-quality snapshot |
| RQ128 | WAITING | pdc-actionability-deploy-parity | Prove the PDC/Decision Board actionability gate on the exact production deployment |
| RQ129 | DONE | decision-board-non-product-confidence-normalization | Remove non-product fake confidence from blocked and insufficient Decision Board cards |
| RQ132 | WAITING | dashboard-support-signal-explainability | Explain the exact block reason, evidence state and next safe operator step for Dashboard support signals |
| RQ137 | PARTIAL | analytics-period-lineage-parity | Align requested, effective and observed period truth across dashboard, pilot readiness and supplier reports |
| RQ138 | OBSOLETE | trend-model-evaluation-contract | Excluded: standalone Trend Models evaluation is outside the current analytics-surface scope |
| RQ139 | PARTIAL | analytics-denominator-null-zero-contract | Core trend/Data Quality false-zero fixes are delivered; derived intelligence, full pre/post contract and cross-surface parity still require follow-up |
| RQ140 | PARTIAL | pre-post-nivelacija-causal-comparability | Local comparability and recommendation gates are hardened; live database/refresh/browser proof remains with STAB16 |
| RQ141 | WAITING | analytics-lineage-scope-cache-refresh-parity | Map every analytics route to period, scope, source, schema, cache and refresh truth |
| RQ142 | OBSOLETE | forecast-trend-measured-evaluation | Excluded: standalone forecast/Trend Models evaluation is deferred |
| RQ143 | WAITING | backend-decision-ranking-ownership | Remove frontend decision/ranking invention and make actionability backend-owned end to end |
| RQ144 | DONE | data-quality-health-denominator-contract | Make Data Quality health distinguish no evidence, valid zero and unavailable shares |
| RQ145 | WAITING | analytics-surface-parity-and-safe-messaging | Prove table/chart/detail/export/report parity and safe mapping of backend codes |
| RQ146 | WAITING | analytics-schema-runtime-proof | Prove endpoint, EF/SQL, relation/migration, 404 and refresh-failure behavior on current runtime |
| RQ147 | WAITING | analytics-metric-evidence-registry | Make the proof level, decision use and limitation of every KPI backend-owned and portable |
| RQ148 | WAITING | sales-margin-returns-measurement-basis | Prove whether sales and margin KPIs are gross/net/returned/cost-covered before they drive decisions |
| RQ149 | WAITING | inventory-economic-metric-evidence | Make inventory economics and availability-censored demand explicitly measurable or unavailable |
| RQ150 | OBSOLETE | forecast-decision-calibration | Excluded: forecast calibration is deferred from the analytics queue |
| RQ151 | DONE | analytics-action-safe-messaging | Replace raw unknown action warning/reason codes with safe user-facing copy |
| RQ152 | DONE | analytics-derived-numeric-state | Preserve unknown/missing numeric evidence in legacy derived intelligence builders |
| RQ153 | DONE | analytics-lineage-static-matrix | Build the offline route lineage matrix without claiming live refresh proof |
| RQ154 | DONE | daily-sales-numeric-state | Keep Daily Sales missing, empty and non-finite chart/summary evidence unavailable instead of zero |
| RQ155 | DONE | dashboard-trend-unknown-visibility | Keep unknown trend values visible and out of gain/loss ranking |
| RQ156 | DONE | pre-post-coverage-unknown-state | Keep unknown pre/post coverage distinct from measured zero on supplier/category surfaces |
| RQ157 | DONE | pdc-baseline-coverage-state | Keep Product Decision trend, margin and coverage evidence unknown when the denominator or baseline is missing |
| RQ158 | DONE | inventory-null-stock-state | Keep null inventory quantity/minimum unknown instead of converting it to OOS or stable stock |
| RQ159 | DONE | inventory-decision-summary-counts | Remove incorrect inventory count arithmetic and the unmeasured 7-day risk label |
| RQ160 | READY | inventory-health-observed-series | Remove or replace the synthetic inventory health score and sparkline |
| RQ161 | WAITING | analytics-details-period-state | Reject invalid periods and keep unknown detail trends out of rankings and direction labels |
| RQ162 | WAITING | inventory-sellthrough-denominator-state | Keep partially missing sell-through denominator evidence unavailable instead of treating it as zero |
| RQ163 | WAITING | supplier-post-observation-state | Prevent absent post-nivelacija observations from becoming measured zero in supplier decisions |
| RQ164 | WAITING | pre-nivelacija-cost-evidence | Prevent null/non-positive purchase cost from becoming a complete 100% margin signal |
| RQ165 | WAITING | data-quality-window-scope | Make Data Quality time boundaries and sale/article scope consistent across health and offender queries |
| RQ166 | WAITING | action-timeline-period-state | Reject reversed action-timeline periods instead of silently swapping the requested scope |
| RQ167 | WAITING | analytics-error-kpi-state | Do not serialize failed sales/inventory KPI responses as valid-looking zero values |
| RQ168 | WAITING | top-products-margin-coverage | Keep partial cost coverage out of confirmed top-product margin ranking |
| RQ169 | WAITING | data-quality-empty-readiness | Keep empty intake data from receiving a numeric readiness score or green label |
| RQ170 | WAITING | data-quality-report-period-state | Reject invalid pilot-intake report periods instead of silently swapping or defaulting them |
| RQ183 | WAITING | inventory-opening-stock-proof | Journal-derived opening stock for sell-through denominator integrity |
| RQ184 | WAITING | velocity-divisor-accuracy | Fixed 30-day divisor for inventory velocity miscalculation |
| RQ185 | WAITING | velocity-active-days-semantics | "Velocity per day" label with active-selling-days divisor confusion |
| RQ186 | WAITING | pdc-lost-sales-arithmetic | Product Decision lost-sales formula ignores velocity |
| RQ187 | WAITING | cache-meta-freshness-truth | Cache write time published as LastRefreshAtUtc on cache hits |
| RQ188 | WAITING | price-intelligence-validity | Price-intelligence discount depth encodes missing list price as 0% |
| RQ189 | WAITING | demand-acceleration-new-product-state | Demand acceleration hardcodes 1.0 sentinel for new demand |
| RQ190 | OBSOLETE | forecast-snapshot-freshness-aggregation | Excluded: standalone forecast provenance work is deferred |
| RQ191 | WAITING | frontend-numeric-safety | Frontend percent clamp hides negative backend signals |
| RQ192 | WAITING | ml-feature-missing-encoding | Supplier ML return rate coalesces missing to 0% |
| RQ193 | WAITING | analytics-async-ordering | Inventory page cross-panel async race condition |
| RQ194 | WAITING | analytics-details-async-safety | Analytics Details missing in-flight guard |
| RQ195 | WAITING | pilot-readiness-async-consistency | Pilot Readiness multi-signal load can mix reload generations |
| RQ196 | WAITING | report-schedule-validation | Inventory report schedules saved without validation |
| RQ197 | WAITING | export-truncation-safety | Scheduled inventory export has no row cap |
| RQ198 | WAITING | decision-board-datascope-override | Executive Decision Board hardcoded dataScope |
| RQ199 | WAITING | pre-nivelacija-datascope | Pre-nivelacija priority endpoint missing DataScope |
| RQ200 | WAITING | pdc-search-pagination-boundary | Product Decision Center search capped at backend rows |
| RQ201 | WAITING | sales-stats-chart-table-parity | Daily Sales chart vs table order divergence |
| RQ202 | WAITING | date-timezone-safety | Daily Sales date sort timezone drift |
| RQ203 | WAITING | inventory-detail-scope-consistency | Inventory detail ignores parent scope and uses fixed 30-day |
| RQ204 | WAITING | analytics-details-scope-parity | Analytics Details global inventory snapshot unrelated to period |
| RQ205 | WAITING | client-cache-invalidation | Frontend 15s cache not invalidated after refresh |
| RQ206 | WAITING | refresh-run-status-accuracy | Partial nightly refresh treated as successful |
| RQ207 | WAITING | refresh-failure-cache-safety | Failed refresh skips cache invalidation |
| RQ208 | WAITING | period-timezone-boundary-safety | Dashboard per-day KPIs use local day count |
| RQ209 | WAITING | database-migration-orchestration | Dual concurrent EF migration paths cause race condition |
| RQ210 | WAITING | startup-readiness-gate | Startup init silently skipped after lock timeout |
| RQ211 | WAITING | migration-sequencing | Parallel SQL migrations without ordering guarantees |
| RQ212 | WAITING | migration-failure-safety | Migration failures swallowed; app runs on drifted schema |
| RQ213 | WAITING | migration-reversibility | EF migration Down() drops fact tables without backup |
| RQ214 | WAITING | seed-data-consistency | Seed sales created without decrementing stock |
| RQ215 | WAITING | aggregation-worker-atomicity | Aggregate refresh delete+insert is non-transactional (P0) |
| RQ216 | WAITING | aggregation-failure-cache-safety | Cache invalidated after partially failed aggregate refresh |
| RQ217 | WAITING | outbox-concurrent-processing | Outbox worker has no row-level locking |
| RQ218 | WAITING | import-retry-idempotency | Access import auto-retry requeues without rolling back |
| RQ219 | WAITING | worker-process-health | Background worker crashes are silently ignored (P0) |
| RQ220 | WAITING | outbox-dlq-observability | Outbox messages dead-lettered with no automatic surfacing |
| RQ221 | WAITING | error-response-sanitization | Insight Studio endpoints return raw exception messages |
| RQ222 | WAITING | aggregate-consistency | Daily vs dimensional aggregates disagree on orphan sales |
| RQ223 | WAITING | import-data-completeness | SkipInvalidForeignKeys default silently drops orphan lines |
| RQ224 | WAITING | analytics-db-routing-safety | Analytics DB connection silently falls back in production |
| RQ225 | WAITING | feature-flag-safety | UseSnapshotCost feature flag toggles live without validation |
| RQ226 | WAITING | worker-schedule-safety | Invalid nightly refresh schedule silently defaults |
| RQ227 | WAITING | cleanup-safety-gates | Batch delete proceeds after archive quota failure |
| RQ228 | WAITING | period-timezone-contract-consistency | Insight Studio v1/v2 period handling timezone mismatch |
| RQ176 | WAITING | inventory-snapshot-freshness-provenance | Keep query time separate from inventory snapshot freshness and last successful refresh |
| RQ177 | WAITING | size-curve-empty-error-state | Preserve missing, empty and partial size-curve states in the panel |
| RQ178 | WAITING | inventory-snapshot-safe-actionability | Add backend-owned actionability and safe user copy to inventory signal snapshots |
| RQ179 | WAITING | supplier-footwear-freshness-state | Do not mark supplier footwear data fresh from response generated time |
| RQ180 | WAITING | pre-post-aggregate-owner-parity | Remove frontend reconstruction of backend-owned pre/post aggregate denominators |
| RQ181 | WAITING | decision-board-blocked-action-cta | Do not expose an executable action CTA for blocked Decision Board cards |
| RQ182 | WAITING | pre-post-coverage-backend-null-state | Preserve unknown pre/post coverage in backend DTOs and aggregate calculations |

---

## RQ01 - Decision Board product expected-impact correctness

Status: DONE
Priority: P0
Type: backend/tests
Feature family: decision-board-impact-trust
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ01-cursor.lock.md`
Commit suggestion: `fix(analytics): preserve product impact trust in decision board`

### Why

Decision Board currently uses `row.ExpectedImpactRsd ?? row.LostSalesEstimate` for product cards. Product Decision Center already sets expected impact based on recommendation type. The board should not reattach lost-sales estimate to rows where Product Decision Center intentionally left expected impact null.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- optional `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`

### Do not touch

- Product Decision Center formula code
- SQL migrations/views
- frontend pages
- action ledger writes

### Do

1. Add tests proving:
   - `REPLENISH`/`BOOST` can show lost-sales expected impact only if PDC supplied it or contract says it is safe.
   - `FIX_DATA` and `INSUFFICIENT_DATA` do not get `LostSalesEstimate` attached as expected impact.
   - `MARKDOWN`/`DO_NOT_ORDER` use slow-stock impact only through `ExpectedImpactRsd`, not lost-sales fallback.
2. Remove or narrow the board-level fallback so board does not override PDC trust semantics.
3. Ensure `impact` section only includes cards with recommendation-aligned expected impact.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"`
- If no build artifacts exist, run `dotnet build Trendplus2.sln --no-restore --configuration Release` and rerun targeted tests.

### Acceptance

- Board no longer upgrades missing expected impact into lost-sales impact for unrelated/blocked recommendations.
- Existing insufficient-data priority cap still passes.
- No Product Decision Center formula change.

### Notes

- 2026-08-04: DONE. Removed board-level `LostSalesEstimate` fallback so product cards trust only `ExpectedImpactRsd` from PDC. Expanded endpoint tests for REPLENISH/BOOST, FIX_DATA/INSUFFICIENT_DATA, and MARKDOWN/DO_NOT_ORDER impact alignment.
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api/Api.csproj --configuration Release` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"` - pass (11 tests)
  - `git diff --check` (scoped files) - pass
- Risk:
  - Pre-existing dirty working tree outside RQ01 scope was left untouched.
  - Executive frontend companion fallback remains tracked by RQ72.
- Next:
  - `RQ02 - Product Decision Center summary denominator contract`

---

## RQ02 - Product Decision Center summary denominator contract

Status: DONE
Ready after: RQ01 DONE
Priority: P1
Type: backend/tests/docs
Feature family: product-decision-denominators
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ02-cursor.lock.md`
Commit suggestion: `test(analytics): define pdc summary denominators`

### Why

Product Decision Center count KPIs are based on top-limited returned rows, while money totals are accumulated before top-limiting. The API must clearly state whether each summary field is based on visible rows, all analyzed rows, or ignored rows.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- Product Decision Center tests
- optional DTO docs/readme update

### Do not touch

- recommendation formulas
- SQL views
- Decision Board ranking

### Do

1. Add tests for top limit behavior with more rows than `top`.
2. Decide if money totals should be:
   - all analyzed rows, with explicit field names/docs, or
   - returned rows only.
3. Avoid changing business totals without before/after notes.

### Checks

- `git diff --check`
- targeted Product Decision Center tests

### Acceptance

- Counts and money totals have explicit denominators.
- `IgnoredRowsCount` meaning is not confused with bad data.

### Notes

- 2026-08-04: DONE. Kept existing numeric split; made denominators explicit with additive fields. Counts stay on returned/top rows; money totals stay on all analyzed rows. `IgnoredRowsCount` is labeled `hidden_by_top_limit`.
- Before/after:
  - BEFORE: same numeric behavior, undocumented mixed denominators.
  - AFTER: unchanged totals/counts; `countDenominatorScope`, `moneyDenominatorScope`, `ignoredRowsMeaning` expose the contract.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/ProductDecisionCenterSummaryDenominatorTests.cs`
  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "ProductDecisionCenterSummaryDenominatorTests|ProductDecisionCenterBuilderIntegrationTests"` - pass (8 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - Frontend types do not yet surface the new optional denominator fields; UI can keep showing raw totals until a follow-up labels them.
  - RQ12 can still refine ignored-row UX copy.
- Next:
  - `RQ03 - Lost-sales unavailable vs true zero`

---

## RQ03 - Lost-sales unavailable vs true zero

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: lost-sales-zero-vs-unknown
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ03-cursor.lock.md`
Commit suggestion: `fix(analytics): separate lost sales unknown from zero`

### Why

Lost-sales validation currently treats `lostSalesEstimate <= 0` as good. Lower-level fallback can return zero when evidence is unavailable. That makes unknown/unavailable look like a clean green zero.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- validation/lost-sales DTO/tests
- optional `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`

### Do not touch

- replenishment formula
- Product Decision Center recommendation formulas
- frontend design unless contract requires a tiny compatible field

### Do

1. Add source status for lost-sales evidence:
   - `view`
   - `fallback`
   - `unavailable`
   - `true_zero`
2. Ensure unavailable does not return status `good`.
3. Add tests for unavailable, fallback positive, fallback zero and true view zero.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted validation tests

### Acceptance

- True zero is distinguishable from unknown/unavailable.
- OOS/replenishment trust remains conservative.

### Notes

- 2026-08-04: DONE. Introduced shared `LostSalesSourceStatus` / `LostSalesSnapshot` and `BuildLostSalesValidationFromSnapshot`. Unavailable â†’ `insufficient_data` with null estimate; view zero â†’ `true_zero`/`good`; fallback zero â†’ `warning`.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/LostSalesValidationSourceStatusTests.cs`
  - `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `Klijent/clientapp/src/types/analytics.ts` (optional `sourceStatus`)
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "LostSalesValidationSourceStatusTests"` - pass (7 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - UI does not yet surface `sourceStatus` labels; optional TS field is additive only.
  - SQL queue Q80 should reuse this vocabulary (not invent a second model).
- Next:
  - `RQ04 - Data Quality no-revenue/no-data status`

---

## RQ04 - Data Quality no-revenue/no-data status

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: data-quality-no-data
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ04-cursor.lock.md`
Commit suggestion: `fix(analytics): mark no revenue data quality as insufficient`

### Why

Data Quality health can produce zero percentages when total revenue is zero. That can make a no-data window appear clean rather than insufficient.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- relevant tests

### Do not touch

- supplier-decision SQL
- Product Decision Center formulas
- frontend pages

### Do

1. Add test fixture with zero total revenue and no offenders.
2. Decide expected status: likely `insufficient_data`, not `excellent`/`good`.
3. Add explicit no-data flag/status if needed.
4. Ensure Decision Board does not show data quality health as clean without evidence.

### Checks

- `git diff --check`
- targeted data quality / decision board tests

### Acceptance

- No-revenue windows do not create green health signals.
- Data Quality card distinguishes clean data from no evidence.

### Notes

- 2026-08-04: DONE. Added `HasRevenueEvidence` on the health snapshot. Decision Board evaluation returns `insufficient_data` when there is no revenue evidence, surfaces a blocker card, and emits `no_revenue_evidence`.
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
  - `Api.Tests/DecisionBoardDataQualityHealthEvaluationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "AnalyticsDataQualityHealthServiceTests|DecisionBoardDataQualityHealthEvaluationTests"` - pass (11 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - RQ75 still owns DataQualityPage UI labeling for the same fake-green family.
- Next:
  - `RQ05 - Analytics dataScope consistency audit`

---

## RQ05 - Analytics dataScope consistency audit

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: docs/tests
Feature family: analytics-datascope-consistency
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ05-cursor.lock.md`
Commit suggestion: `docs(analytics): audit data scope consistency`

### Why

Different analytics modules apply `dataScope` through article origin, sale header origin, or a mix. This can make imported/existing dashboards inconsistent.

### Scope only

- analytics endpoint/service query builders
- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
- focused tests for one or two highest-risk inconsistencies

### Do not touch

- SQL formula weights
- frontend routing
- action write logic

### Do

1. Map each analytics query's dataScope source:
   - article `DataOrigin`
   - sale header `DataOrigin`
   - both
   - not applied
2. Decide canonical rule per metric family.
3. Create follow-up prompts for concrete mismatches.

### Checks

- `git diff --check`
- docs-only unless tests are added

### Acceptance

- `imported`, `existing`, `all` semantics are visible and testable.
- No hidden filter drift remains undocumented.

### Notes

- 2026-08-04: DONE. Docs/tests matrix only; no runtime filter rewrite. Canonical rules proposed (salesâ†’header, quality/inventoryâ†’article). Highest P0 mismatch remains DQ top-offender unscoped `sales_30d`.
- Changed files:
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs` (extract `TopOffendersSql` const, no SQL change)
  - `Api.Tests/DataScopeConsistencyContractTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataScopeConsistencyContractTests"` - pass (2 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - Dual-origin PDC and inventory forced-all remain documented follow-ups (RQ05-F1/F2), not fixed here.
- Next:
  - `RQ06 - Data Quality top-offender revenue scope correctness`

---

## RQ06 - Data Quality top-offender revenue scope correctness

Status: DONE
Ready after: RQ05 DONE or explicitly unblocked
Priority: P1
Type: backend/tests
Feature family: data-quality-offender-scope
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ06-cursor.lock.md`
Commit suggestion: `fix(analytics): align top offender revenue scope`

### Why

Top offender `sales_30d` currently aggregates sales before applying dataScope at the article quality source. For imported/existing views this can overstate or cross-contaminate revenue impact.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- data quality offender tests
- optional runbook doc

### Do not touch

- Decision Board ranking
- Product Decision Center formulas
- supplier-decision SQL

### Do

1. Add tests for imported/existing dataScope where article origin and sale header origin differ.
2. Align offender revenue impact with the canonical dataScope rule from RQ05.
3. Preserve `all` behavior unless intentionally corrected.

### Checks

- `git diff --check`
- targeted data quality tests

### Acceptance

- Top offender revenue impact matches requested scope.
- No silent cross-scope revenue leakage.

### Notes

- 2026-08-04: DONE. `sales_30d` now filters by sale-header `DataOrigin`; article membership stays article-scoped. `all` still includes all headers.
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Api.Tests/DataScopeConsistencyContractTests.cs`
  - `Api.Tests/DataQualityPostgresIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataScopeConsistencyContractTests|DataQualityPostgresIntegrationTests"` - pass (9; Postgres cases may no-op when fixture unavailable)
  - `git diff --check` (scoped) - pass
- Risk:
  - `GetDataQualityIssuesHandler` still has unscoped `sales_30d` (out of RQ06 file scope) â†’ follow-up RQ06-F1.
- Next:
  - `RQ07 - Missing-cost offender drilldown`

---

## RQ07 - Missing-cost offender drilldown

Status: DONE
Ready after: RQ04 DONE
Priority: P1
Type: backend/API-contract/tests
Feature family: missing-cost-offenders
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ07-cursor.lock.md`
Commit suggestion: `feat(analytics): add missing cost offender contract`

### Why

Health snapshot tracks missing-cost revenue, but top offenders do not support `missingCost`; unknown issue types silently default to missing supplier. Operators need exact products causing missing-cost risk.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/DataQualityEndpoints.cs`
- data quality tests/docs

### Do not touch

- supplier decision scoring
- frontend redesign
- unrelated issue types

### Do

1. Add or document `missingCost` as an issue type.
2. Stop silently defaulting unknown issue types to missing supplier, or document backward-compatible validation behavior.
3. Add tests that `missingCost` returns products with missing line/article cost evidence.

### Checks

- `git diff --check`
- targeted data quality tests

### Acceptance

- Missing-cost health signal has drilldown.
- Invalid issue type does not silently return wrong offender category.

### Notes

- 2026-08-04: DONE. Top offenders support `missingCost` via article `NabavnaCena` null/â‰¤0 (`is_missing_cost`), independent of supplier CASE. Unknown issue types â†’ API 400 / service `ArgumentOutOfRangeException` (no silent supplier fallback). Issues-list `Normalize` still defaults unknownâ†’missingSupplier (handler not rewritten).
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesQuery.cs`
  - `Api/Endpoints/DataQualityEndpoints.cs`
  - `Api.Tests/DataQualityMissingCostOffenderContractTests.cs`
  - `Api.Tests/DataQualityPostgresIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataQualityMissingCostOffenderContractTests|DataScopeConsistencyContractTests|DataQualityPostgresIntegrationTests"` - pass (21)
  - `git diff --check` (scoped) - pass
- Risk:
  - Issues list / frontend tabs still lack missingCost workflow (audit R80 residual); cost evidence is article-level nabavna, not line-level ps.NabavnaCena.
- Next:
  - `RQ08 - Blocked supplier signal ranking in Decision Board`

---

## RQ08 - Blocked supplier signal ranking in Decision Board

Status: DONE
Ready after: RQ01 DONE; SQL queue Q69 evidence available if needed
Priority: P1
Type: backend/tests
Feature family: supplier-blocked-signal-ranking
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ08-cursor.lock.md`
Commit suggestion: `fix(analytics): cap blocked supplier board cards`

### Why

Decision Board adds a blocker when supplier recommendation is not allowed, but still builds supplier cards that can rank high from revenue/confidence. Blocked signal must not look like an actionable supplier decision.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`

### Do not touch

- supplier-decision SQL
- supplier report UI
- action ledger writes

### Do

1. Add tests where `RecommendationAllowed=false` and supplier has high revenue/confidence.
2. Ensure supplier cards are either:
   - capped like insufficient data,
   - explicitly `signal_check`, or
   - only shown under blockers/verification section.
3. Keep blocker card behavior.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"`

### Acceptance

- Blocked supplier signals cannot appear as ordinary high-confidence decisions.
- UI can still guide operator to verify supplier dataset.

### Notes

- 2026-08-04: DONE. When `RecommendationAllowed=false`, supplier cards are labeled `signal_check` / `insufficient_data`, priority capped â‰¤40, ImpactScore=0, excluded from `urgent` and `impact`; remain in `supplierRisk` for verification. Trust blocker card kept.
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass (13)
  - `git diff --check` (scoped) - pass
- Risk:
  - Blocked supplier cards still visible in `supplierRisk` (intentional verification path); frontend must respect `insufficient_data` / warning codes.
- Next:
  - `RQ09 - Analytics actions empty-state contract`

---

## RQ09 - Analytics actions empty-state contract

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: action-source-empty-state
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ09-cursor.lock.md`
Commit suggestion: `fix(analytics): treat empty actions as healthy source state`

### Why

Decision Board marks `analytics-actions` as `insufficient_data` when there are no actions. That may be wrong: zero open actions can be healthy empty state.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- tests/docs

### Do not touch

- action item service writes
- action outcome calculations

### Do

1. Decide empty action list semantics:
   - healthy empty
   - insufficient only if action service failed
   - warning if expected actions are missing
2. Add tests for empty actions vs service unavailable warnings.

### Checks

- `git diff --check`
- targeted Decision Board tests

### Acceptance

- No-actions is not automatically treated as bad data unless contract says so.

### Notes

- 2026-08-04: DONE. Contract: empty successful load â†’ `good` (no `no_actions` warning); `analytics_actions_unavailable` â†’ `insufficient_data`. "Expected actions missing" not auto-warned (would need cross-source expectation; left as future).
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests|DecisionBoardAggregationContractTests"` - pass (23)
  - `git diff --check` (scoped) - pass
- Risk:
  - UI that treated `no_actions` / empty as red DQ may need to switch to source Message; cross-signal "expected actions" still not detected.
- Next:
  - `RQ10 - Inventory evidence confidence contract`

---

## RQ10 - Inventory evidence confidence contract

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: docs/backend-contract
Feature family: inventory-evidence-confidence
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ10-cursor.lock.md`
Commit suggestion: `docs(analytics): define inventory evidence confidence`

### Why

Inventory cards derive confidence mostly from workflow status. Evidence quality should ideally account for velocity, stock freshness, movement history and calculation source.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- inventory DTO/service docs/tests if needed
- optional `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`

### Do not touch

- inventory action algorithm unless a separate prompt is created
- SQL formulas

### Do

1. Document current confidence mapping.
2. Identify missing evidence fields needed for better confidence.
3. Add a follow-up prompt if DTO/service changes are needed.

### Checks

- `git diff --check`
- docs-only unless tiny tests are added

### Acceptance

- Inventory confidence is not presented as stronger than its evidence source.

### Notes

- 2026-08-04: DONE. Documented contract; capped board confidence so workflow status never maps to medium/high; warning `confidence_workflow_status_only`; ConfidenceScore stays null. Follow-up RQ13 for DTO evidence wiring.
- Changed files:
  - `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass
  - `git diff --check` (scoped) - pass
- Risk:
  - Approved inventory cards now show `low` instead of `medium` (intentional honesty); evidence-grade confidence still unavailable until RQ13.
- Next:
  - `RQ11 - Transaction item/line/unit semantics`

---

## RQ11 - Transaction item/line/unit semantics

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: transaction-stat-semantics
Parallel-safe: yes
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ11-cursor.lock.md`
Commit suggestion: `fix(analytics): clarify transaction stats line vs unit semantics`

### Why

`AvgItemsPerTransaction` uses sale-line count, not sum of quantities. If the UI means units/items bought, this is inaccurate; if it means lines, the label should say lines.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- relevant DTO/test/docs

### Do not touch

- sales summary formulas
- frontend dashboard except label if explicitly required

### Do

1. Add fixture with one transaction, two lines and quantity > 1.
2. Decide whether metric is average lines or average units.
3. Rename/adjust field or add explicit second metric if needed.

### Checks

- `git diff --check`
- targeted cached analytics tests

### Acceptance

- Transaction statistic label matches actual calculation.

### Notes

- 2026-08-05: DONE. Contract: `avgItemsPerTransaction` = sale **lines** per receipt (matches UI *Stavki po transakciji*); added `avgUnitsPerTransaction` for sold units. Fixture test proves divergence when qty > 1.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
  - `docs/qa/TRANSACTION_STATS_SEMANTICS_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` (infoTip only)
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "TransactionStats_DistinguishesAverageLinesFromAverageUnits"` - pass
  - `git diff --check` (scoped) - pass
- Risk:
  - Legacy `Program.cs` transaction-stats endpoint still line-count only (documented out of scope).
- Next:
  - `RQ12 - Product Decision Center ignored/top rows contract`

---

## RQ12 - Product Decision Center ignored/top rows contract

Status: DONE
Ready after: RQ02 DONE
Priority: P2
Type: backend-contract/tests
Feature family: pdc-ignored-rows-contract
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ12-cursor.lock.md`
Commit suggestion: `docs(analytics): lock pdc ignored rows contract`

### Why

`IgnoredRowsCount` currently means rows hidden by top limit, not necessarily invalid or ignored for data quality. Operators may misread it as bad data.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- DTO docs/tests

### Do not touch

- recommendation scoring
- SQL migrations

### Do

1. Test `TotalRows`, `AnalyzedRows`, `IgnoredRowsCount` with top limit.
2. Rename/add metadata if needed to separate hidden-by-limit from ignored-because-invalid.
3. Keep backward compatibility unless explicitly approved.

### Checks

- `git diff --check`
- targeted PDC tests

### Acceptance

- Hidden top-limit rows are not confused with unreliable/invalid data.

### Notes

- 2026-08-05: DONE. Backend contract from RQ02 confirmed and documented; added `PDC_IGNORED_ROWS_CONTRACT.md`, focused contract tests (3-product top=2 fixture), TS denominator types. No numeric behavior change.
- Changed files:
  - `docs/qa/PDC_IGNORED_ROWS_CONTRACT.md`
  - `Api.Tests/ProductDecisionCenterIgnoredRowsContractTests.cs`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "ProductDecisionCenterIgnoredRows|ProductDecisionCenterSummaryDenominator|ProductDecisionCenterBuilderIntegration"` - pass (11)
  - `git diff --check` (scoped) - pass
- Risk:
  - PDC UI still labels `totalRows` without surfacing `ignoredRowsMeaning`; operators should read contract before comparing to DQ intake â€œignorisani redoviâ€.
- Next:
  - `RQ13 - Wire inventory signal evidence onto Decision Board cards`

---

## RQ13 - Wire inventory signal evidence onto Decision Board cards

Status: DONE
Ready after: RQ10 DONE
Priority: P2
Type: backend/DTO
Feature family: inventory-evidence-wiring
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ13-cursor.lock.md`
Commit suggestion: `feat(analytics): wire inventory signal confidence to decision board`

### Why

RQ10 capped board confidence because `InventoryActionSuggestionDto` lacks evidence fields. Operators still need evidence-grade confidence when inventory signals exist.

### Scope only

- `Api/Dtos/InventoryExperienceDtos.cs` / workflow builder
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- tests for mapping from signal evidence when present

### Do not touch

- inventory SQL formulas rewrite
- frontend redesign

### Do

1. Add optional signal confidence / recommendationAllowed / reasonCodes (or join insights by SKU/store).
2. Map board cards from evidence when present; keep `confidence_workflow_status_only` fallback when absent.
3. Tests for evidence-present vs evidence-absent paths.

### Checks

- `git diff --check`
- targeted Decision Board / inventory tests

### Acceptance

- Board inventory confidence can exceed `low` only when signal evidence is present on the card/DTO.

### Notes

- 2026-08-05: DONE. Extended `InventoryActionSuggestionDto` with optional signal fields; workflow builder computes evidence via `ComputeSuggestionSignalEvidence`; board resolver uses evidence path when `SignalConfidencePct` present, workflow fallback otherwise; blocked recommendations cap at `insufficient_data`.
- Changed files:
  - `Api/Dtos/InventoryExperienceDtos.cs`
  - `Api/Endpoints/InventoryEndpoints.cs`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass (27)
- Risk:
  - Approved inventory cards with signal evidence may now show `medium`/`high` (intentional when evidence supports it).
- Next:
  - Queue complete; new reliability work requires a new queue entry.

---

## RQ106 - Decision Pulse exception digest

Status: DONE
Ready after: `QDB06` is `DONE` and `RQ96` is `DONE`
Priority: P1
Type: backend/frontend-contract/tests
Feature family: decision-pulse-digest
Parallel-safe: no
Owner: Cursor Auto
Local lock: removed after DONE
Commit suggestion: `feat(analytics): add decision pulse digest`
Promotion note: 2026-08-20 - owner-scheduled after QDB06 and RQ96 both DONE; claimed when no other exclusive READY remained.

### Problem

Operators still have to open analytics screens to learn that a decision, data-quality failure or stale evidence needs action. There is no first-party exception digest that follows an existing decision/metric family with a Why and a deep link.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` ranks exception/digest delivery immediately after source adaptability and observed historical inventory.
- Owner decision 2026-08-18: queue Decision Pulse as WAITING after QDB06 and RQ96; first version is email + in-app feed; no generic DSL or Slack.

### Scope

- in-app Decision Pulse feed plus email for the same events
- events must follow an existing decision or metric family (inventory, product decision, supplier, data quality)
- each item must expose Why, freshness/data-quality, and a deep link to the owning surface
- suppress items whose evidence is stale, empty, or an error-as-zero
- do not add Slack, a generic rule DSL, or a new recommendation scorer

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- current Decision Board / Product Decision / inventory action contracts

### Do

1. Define a bounded event vocabulary owned by existing backend decision/metric families.
2. Persist or project an in-app feed that does not invent recommendations or rates.
3. Send the same events by email without logging row payloads or secrets.
4. Hide or suppress items when evidence is stale, missing, or in error; empty is not an alert.
5. Keep MT dedicated (`n/a_dedicated`); do not use caller headers as tenant authority.

### Tests

- `git diff --check`
- focused backend tests that Pulse items preserve backend status/reason/freshness and do not substitute zero KPIs for errors
- focused UI or contract test that empty/error/stale items are not shown as actionable
- email path does not include secrets or raw customer row payloads

### Acceptance

- An operator can receive a Pulse item with Why + deep link for one existing decision/metric family.
- Stale or failed evidence cannot look like a trusted alert.
- Slack and generic DSL remain out of scope.

### Dependencies

- `QDB06` DONE (owner 2026-08-18)
- `RQ96` DONE so historical inventory evidence can back inventory Pulse items without reconstructed-as-observed confusion
- Do not displace current execution `RQ96`
- Do not start MT02 or shared-SaaS notification routing

### Completion note

- Date: 2026-08-20
- Status: DONE
- Completion: Product Decision exception Pulse with Why + deep link, stale/empty/error suppression, in-app feed and SMTP email path; tenantScope fixed to n/a_dedicated
- Changed files: Application/Analytics/DecisionPulse/DecisionPulseProjector.cs; Application/Analytics/DecisionPulse/DecisionPulseEmailComposer.cs; Api/Services/Analytics/DecisionPulseService.cs; Api/Endpoints/DecisionPulseEndpoints.cs; Api/Program.cs; Api.Tests/DecisionPulseProjectorTests.cs; Klijent/clientapp/src/pages/DecisionPulsePage.tsx; Klijent/clientapp/src/services/decisionPulseApi.ts; Klijent/clientapp/src/pages/__tests__/DecisionPulsePage.spec.tsx; Klijent/clientapp/src/App.tsx; Klijent/clientapp/src/layout/navConfig.ts; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-20-RQ106-evidence.md
- Contract/runtime behavior changed: yes; new GET/POST `/api/analytics/decision-pulse` and `/analytics/decision-pulse` UI
- Checks run: dotnet test DecisionPulseProjectorTests (4 passed); npm DecisionPulsePage.spec (1 passed); governance validators
- Checks not run: full suites; live SMTP send
- Run log: .ai/runs/2026-08-20-RQ106-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 50236d144d6dd7e668be0601dac9c76c56a3f15e
- Main verification: git rev-parse origin/main -> 50236d144d6dd7e668be0601dac9c76c56a3f15e; work SHA is an ancestor
- Missed: inventory/supplier Pulse families; scheduled worker; durable inbox table
- Follow-up: RQ97 DONE (fail-closed provenance); RQ98 WAITING; SQL Server e2e commercial gate remains owner-routed
- Residual risk: email requires DecisionPulse:Recipients + SMTP enabled; otherwise in-app feed still works
- Prompt defect / scope repair: first slice limited to Product Decision family; RQ96 was already DONE on origin/main by another agent so this run claimed RQ106 instead
- Next: none (RQ Current READY none)

---

## RQ107 - Controlled markdown / replenishment scenario planning contract

Status: DONE
Completed: docs-only precursor promoted on 2026-08-20; runtime scenario work remains gated by trusted forecast materialization plus a measured backtest window
Priority: P2
Type: docs-contract (later runtime)
Feature family: scenario-planning-contract
Parallel-safe: yes, docs/contracts only until later runtime authorization
Owner: unassigned

### Problem

Competitive gap Gate 4 still needs controlled scenario planning (markdown / replenishment what-if). Starting that before trusted forecast materialization and measured backtest would invent scenario outcomes from untrusted forecasts.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` Gate 4
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`
- `docs/qa/SCENARIO_PLANNING_CONTRACT_2026-08-20.md`
- `docs/planning/QUEUE_REFILL_2026-08-20.md`

### Owner-gated path

1. Keep `RQ97` and `RQ98` DONE so the queue stays fail-closed on forecast provenance and backtesting.
2. Do not promote runtime scenario work until the forecast writer is proven and the backtest comparison window is available.
3. The docs-only precursor is now complete: freeze only the scenario vocabulary and comparison basis. Do not add simulator logic, optimizer behavior or runtime forecast mutation in that precursor.

### Scope

- docs/contracts only for fixed scenario sets, comparison basis, and no-fake rules;
- no simulator UI, optimizer, or LLM scenarios in this prompt.

### Read first

- RQ98 / RQ97 completion notes
- competitive gap audit Gate 4
- MASTER_ROADMAP.md current READY

### Do

1. Freeze allowed scenario vocabularies (e.g. no-change / fixed markdown / replenishment bands).
2. Require comparison against measured historical behavior, not invented forecast certainty.
3. Keep missing measured windows as unavailable, not zero impact.
4. Do not implement a runtime simulator in this prompt.

### Tests

- missing measured window stays unavailable, not `0` impact;
- docs/queue validators pass when promoted.

### Acceptance

- one citeable scenario-planning contract exists on main;
- RQ Current READY remains single / none as declared.

### Dependencies

- trusted forecast materializer + measured backtest window for runtime follow-up;
- do not promote ahead of higher-priority exclusive RQ work.

---

## RQ108 - Add authoritative forecast materializer and observed pairing foundation

Status: DONE
Ready after: `RQ97` and `RQ98` are `DONE` and an owner authorizes the first runtime forecasting follow-up
Priority: P1
Type: backend/persistence/tests
Feature family: forecast-materializer-observed-window
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ108-<agent>.lock.md`
Commit suggestion: `feat(analytics): materialize forecasts for measured comparison`
Promotion note: 2026-08-20 - owner-promoted from the pilot audit because forecast provenance/backtest contracts are done but no authoritative runtime writer or paired observed window exists yet.

### Problem

`RQ97` and `RQ98` deliberately closed the forecast surface in a fail-closed way, but Trendplus still has no authoritative runtime writer that materializes forecast snapshots and later pairs them to observed evidence. Without that foundation, backtesting, scorecards and scenario planning remain contracts only.

### Evidence

- `RQ97` froze snapshot provenance and made missing materialization explicit instead of inventing trust.
- `RQ98` added a fail-closed baseline/backtest contract, but documented that the paired forecast-vs-observed window is still unavailable.
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md` keeps the core pilot conservative and lists inventory/forecast fail-closed paths as a minimum smoke area.
- The 2026-08-20 audit concluded that the product still lacks:
  - a trusted forecast materializer;
  - a paired observed outcome window;
  - measured WAPE/bias/MAE proof on runtime-produced snapshots.

### Scope

- forecast snapshot persistence/materialization files under the existing inventory forecast owner path
- the observed daily inventory/sales pairing path introduced by `RQ96`
- fail-closed forecast DTO/API surfaces only where needed to expose authoritative pairing state
- focused backend tests for materialization, pairing and unavailable-window behavior
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- one dated `docs/qa/` or durable `.ai/runs/...` evidence note for the runtime follow-up

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` (`RQ96`-`RQ98`)
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- current inventory forecast query/handler files
- `RQ96` completion evidence for observed daily snapshot behavior

### Do

1. Add the smallest authoritative forecast snapshot writer/materializer that can persist a forecast snapshot together with its provenance and issue time.
2. Pair persisted forecast snapshots only to observed evidence that satisfies the canonical RQ96 daily snapshot basis; do not reconstruct observed truth from later live views.
3. Keep missing or insufficient observed windows explicit as unavailable, not zero error and not a healthy score.
4. Expose additive runtime fields only where needed so later scorecard work can consume authoritative pairing state.
5. Do not implement scenario simulation, optimizer behavior, or frontend scorecard UX in this prompt.

### Tests

- `git diff --check`
- focused backend tests for:
  - forecast snapshot materialization with provenance preserved
  - observed pairing on a deterministic historical window
  - missing observed window -> unavailable / fail-closed
  - stale or mismatched forecast basis -> unavailable / fail-closed
- nearest focused full forecast test command for the touched area

### Acceptance

- Trendplus can persist an authoritative forecast snapshot and later pair it to the correct observed window.
- Missing observed evidence remains unavailable rather than fake-measured.
- Later measured-scorecard work has a real runtime foundation instead of only contract prose.
- The prompt does not invent scenario outputs or a frontend scorecard.

### Dependencies

- `RQ96` DONE.
- `RQ97` DONE.
- `RQ98` DONE.
- Do not weaken the fail-closed contract from `RQ97`/`RQ98` while adding the writer/pairing foundation.

### Completion note

- Date: 2026-08-22
- Status: DONE
- Completion: added an authoritative inventory forecast snapshot materializer with persisted issue-time/provenance metadata, a fail-closed observed-pairing view foundation, trusted provenance surfacing in the forecast read handler, and focused tests proving upsert plus observed pairing; the implementation is now synchronized on current main
- Changed files: Application/Analytics/Queries/DbDataReaderNullableExtensions.cs; Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs; Application/Analytics/Queries/GetInventoryForecast/InventoryForecastSnapshotProvenance.cs; Application/Analytics/Queries/GetInventoryForecast/InventoryForecastMaterializationContracts.cs; Application/Common/Interfaces/IInventoryForecastSnapshotMaterializerService.cs; Infrastructure/Services/Inventory/InventoryForecastSnapshotMaterializerService.cs; Api.Tests/InventorySnapshotContractTests.cs; Api.Tests/DatabaseInitializerP0IntegrationTests.cs; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-22-RQ108-evidence.md
- Contract/runtime behavior changed: authoritative forecast snapshot persistence and observed pairing now ship on current main; missing observed evidence still fails closed as unavailable
- Checks run: `git diff --check` - pass; `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Trendplus2.Tests.InventorySnapshotContractTests|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence"` - pass; `node scripts/check-agent-instructions.mjs --self-test` - pass; `node scripts/check-agent-instructions.mjs` - pass; `node scripts/check-prompt-queues.mjs --self-test` - pass; `node scripts/check-prompt-queues.mjs` - pass; `node scripts/check-planning-architecture.mjs --self-test` - pass; `node scripts/check-planning-architecture.mjs` - pass
- Checks not run: full Release suite - not needed after the targeted materialization/pairing evidence pass; remote workflow re-run - not needed because current main verification is on the pushed delivery SHA
- Run log: `.ai/runs/2026-08-22-RQ108-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `908afeef7a76795280c8e15387454ac33dd2ada4`
- Main verification: `git rev-parse origin/main -> 908afeef7a76795280c8e15387454ac33dd2ada4`
- Missed: none known
- Follow-up: `RQ109` remains WAITING until owner promotion
- Residual risk: pairing still depends on the RQ96 observed daily stock foundation; if that foundation is absent, paired evidence remains unavailable rather than invented
- Next: `RQ109`
- Prompt defect / scope repair: same-owner runtime foundation repair for forecast materialization and observed pairing

---

## RQ109 - Expand Decision Pulse to inventory, supplier and durable delivery

Status: DONE
Ready after: `RQ108` is `DONE` and the first authoritative forecast/observed pairing surface exists
Priority: P1
Type: backend/frontend-delivery/tests
Feature family: decision-pulse-expansion
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ109-<agent>.lock.md`
Commit suggestion: `feat(analytics): expand decision pulse coverage`

### Problem

`RQ106` delivered the first Product Decision Pulse slice, but the audit showed that inventory and supplier families are still missing and there is no scheduler, durable inbox table or live delivery proof. Without a bounded follow-up prompt, Pulse can look more complete than it really is.

### Evidence

- `RQ106` completion note explicitly missed inventory/supplier families, a scheduled worker, a durable inbox table and live SMTP proof.
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` ranks exception/digest delivery as a core near-term differentiator.
- The 2026-08-20 audit confirmed that the current Pulse is still a first slice rather than a complete operator-delivery surface.

### Scope

- existing Decision Pulse projector/service/email files
- additive persistence/delivery files needed for a durable inbox or scheduled projection
- inventory/supplier deep-link/status/freshness wiring
- focused backend/frontend tests for suppression, scheduling and family coverage
- no Slack, no generic DSL, no MT/shared-SaaS routing

### Read first

- `RQ106` completion note
- Decision Pulse backend/frontend files landed by `RQ106`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`

### Do

1. Add inventory and supplier Pulse family coverage only when each item can reuse existing backend truth, Why, freshness and deep-link semantics.
2. Add the smallest durable inbox/scheduler path needed to make Pulse delivery repeatable.
3. Prove live send or an equally authoritative delivery path without logging secrets or row payloads.
4. Keep stale/empty/error suppression and `n/a_dedicated` tenant scope rules from `RQ106`.

### Tests

- focused Pulse projector tests for inventory/supplier families
- durable inbox/scheduler tests
- frontend Pulse feed tests only where new family branches are added
- live delivery proof or explicit blocker evidence

### Acceptance

- Decision Pulse covers more than Product Decision without inventing a second recommendation source.
- Delivery is durable/repeatable instead of purely ad hoc.
- Missing SMTP or scheduling proof remains explicit, not implied.

### Dependencies

- `RQ106` DONE.
- `RQ108` DONE first so inventory/forecast Pulse items can rely on authoritative runtime pairing rather than contract-only forecast truth.

### Completion note

- Date: 2026-08-22
- Status: DONE
- Completion: expanded Decision Pulse to inventory and supplier families, added a durable scheduled delivery path, verified the implementation with focused build/test checks, and synchronized the implementation to current main
- Changed files: `Api/Services/Analytics/DecisionPulseService.cs`, `Api/Services/Analytics/DecisionPulseDeliveryService.cs`, `Api/Workers/DecisionPulseSchedulerWorker.cs`, `Api/Endpoints/DecisionPulseEndpoints.cs`, `Api/Program.cs`, `Api/Config/WorkerRuntimeConfig.cs`, `Api/Services/WorkerRegistryService.cs`, `Api.Tests/DecisionPulseProjectorTests.cs`, `Application/Analytics/DecisionPulse/DecisionPulseAutomationContracts.cs`, `Application/Analytics/DecisionPulse/DecisionPulseEmailComposer.cs`, `Application/Analytics/DecisionPulse/DecisionPulseProjector.cs`, `Application/Common/Interfaces/IDecisionPulseScheduleService.cs`, `Infrastructure/Properties/AssemblyInfo.cs`, `Infrastructure/Services/Analytics/DecisionPulseScheduleService.cs`, `Infrastructure/Services/WorkerRegistryCatalog.cs`, `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Contract/runtime behavior changed: Decision Pulse now includes inventory and supplier items, email output shows the source family, and scheduler-backed delivery is available via the new pulse schedule table/worker
- Checks run: `dotnet build Api/Api.csproj --no-restore --configuration Release` (pass), `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionPulseProjectorTests"` (pass), `git diff --check` (pass)
- Checks not run: live SMTP send, full solution test suite
- Run log: `.ai/runs/2026-08-22-RQ109-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `54a29409efd842da438de99c890f5ecb3054cbc3`
- Main verification: `git merge-base --is-ancestor 54a29409efd842da438de99c890f5ecb3054cbc3 origin/main -> ancestor=true`
- Missed: no live external email proof yet
- Follow-up: none for this prompt; RQ110 remains the next queued analytics reliability prompt
- Residual risk: scheduler delivery still depends on runtime SMTP/configuration
- Next: `RQ110`
- Prompt defect / scope repair: none; the queue prompt was mechanically promotable after confirming the dependency gate

---

## RQ110 - Prove pilot analytics screens stay non-empty when authoritative seeded data exists

Status: DONE
Ready after: `RQ108` is `DONE` and the owner-supplied canonical production data-bearing route/filter matrix exists (`docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md` + `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md`)
Priority: P1
Type: docs/tests/backend-contract
Feature family: analytics-screen-data-availability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ110-<agent>.lock.md`
Commit suggestion: `test(analytics): prove non-empty pilot screen data availability`

### Problem

Trendplus should not show a blank chart, blank table, or misleading empty state on a pilot analytics screen when authoritative data already exists in the database for that screen's requested period/scope. Today this risk is spread across refresh ownership, cache identity, filter lineage, route smoke, and screen-specific endpoint behavior, but there is no single executable proof matrix for the main pilot surfaces.

### Evidence

- User requirement 2026-08-20: maximize analytics data reliability and avoid blank tables/charts when the database already contains data.
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md` already calls out screen cache identity, explicit empty-success metadata, and inventory list coverage, but not one cross-screen authoritative matrix.
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` shows that some families can lag after aggregation/data-quality refresh even when underlying data has already changed.
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md` historically captured shell-only route mismatches and route-level failures that can look like "no data" from the operator perspective.
- Current release evidence remains conservative (`docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`): the pilot is not ready until fresh exact-SHA route/smoke truth exists.
- Owner-supplied route/filter coverage exists in `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md` and `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`.

### Scope

- one new `docs/qa/` or architecture-style matrix for the main pilot analytics screens:
  - dashboard
  - product decision center
  - executive decision board
  - inventory
  - supplier decision/sales
  - analytics actions
- focused backend contract tests for those screen families only where seeded non-empty proof is missing
- the nearest existing backend test hosts for the named screens
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md`
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Do

1. Create a single matrix that names, for each main pilot analytics screen:
   - authoritative source tables/views/materialized views;
   - refresh owner;
   - canonical period/scope filters;
   - allowed successful-empty reasons;
   - one deterministic seeded non-empty fixture/query basis.
2. Add the smallest focused backend proofs that when the authoritative seeded basis exists, the corresponding API does one of only two things:
   - returns non-empty rows/series/cards; or
   - returns an explicit blocked/warning/empty reason that explains why the screen cannot trustfully show data.
3. Do not treat route-shell fallback, stale cache, or unknown refresh state as a successful empty dataset.
4. If a screen family fails the new proof, classify the failure into:
   - source/refresh ownership gap;
   - filter lineage/scope bug;
   - cache identity/invalidation bug;
   - route/render mismatch;
   - test harness gap.
5. Keep this prompt at matrix/proof level. Create or refine the runtime repair prompt from the proven failure family instead of broadening this prompt silently.

### Tests

- `git diff --check`
- focused `dotnet test` commands for the touched screen-family test hosts
- governance validators if queue docs change

### Acceptance

- There is one citeable pilot analytics screen-data availability matrix.
- Each named pilot screen has a deterministic seeded proof that authoritative data does not silently collapse into a blank screen or fake empty success.
- Allowed empty states remain explicit and distinguishable from missing/blocked data.
- Any reproduced runtime gap is classified tightly enough to feed the next owner prompt.

### Dependencies

- `RQ108` DONE first.
- Do not fix broad refresh/cache/runtime behavior inside this prompt unless one smallest same-owner repair is required to make the proof executable and is recorded as such.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: established the citable pilot screen-data availability matrix, fixed the browser request-timeout contract, and split the dashboard-isolation gap into `RQ115` instead of broadening `RQ110`.
- Changed files:
  - `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
  - `MASTER_ROADMAP.md`
  - `Klijent/clientapp/src/utils/fetchWithTimeout.ts`
  - `Klijent/clientapp/src/utils/__tests__/fetchWithTimeout.spec.ts`
- Contract/runtime behavior changed: dashboard/bootstrap proof is now citable at route/meta/smoke level, and fetch timeout abort behavior now matches the repo contract.
- Checks run: `git diff --check`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `npm run test:run -- src/utils/__tests__/fetchWithTimeout.spec.ts`; `npm run typecheck`
- Checks not run: backend runtime tests; live SMTP send
- Run log: `.ai/runs/2026-08-22-RQ110-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `0794cfc61250c23d3377b0c8670c830b21d32152`
- Main verification: `git merge-base --is-ancestor 0794cfc61250c23d3377b0c8670c830b21d32152 origin/main -> ancestor=true`
- Missed: dashboard family still lacks a separately named isolated seeded-non-empty backend proof
- Follow-up: `RQ115`
- Residual risk: some surfaces still lean on route/meta/smoke proof rather than a named physical source on every row
- Next: `RQ111`
- Prompt defect / scope repair: split the dashboard gap into `RQ115` rather than broadening `RQ110` further

---

## RQ111 - Close refresh/cache/materialized-view gaps that can hide existing data

Status: DONE
Ready after: `RQ110` is `DONE`
Priority: P1
Type: backend/workers/cache/tests
Feature family: analytics-refresh-cache-parity
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ111-codex.lock.md`
Commit suggestion: `fix(analytics): preserve screen data after refresh and cache churn`

### Problem

Even when authoritative data exists, a pilot analytics screen can still look empty or stale if refresh ownership, materialized-view readiness, or cache invalidation is incomplete. The product must not lose visible screen data behind a stale empty cache entry, a refresh family that was not invalidated, or an unlabelled materialized-view lag.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` documents remaining follow-up risk for:
  - supplier summary surfaces after aggregation-worker refresh;
  - report-family regeneration/version rotation;
  - dashboard/product/supplier/inventory trust surfaces after data-quality recalculation.
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` and `docs/roadmaps/BUSINESS_ROADMAP.md` require refresh/freshness truth to stay visible rather than inferred from page render time.
- `RQ110` is intended to classify which pilot screen families still collapse into blank or stale states despite an authoritative seeded basis.

### Scope

- `AnalyticsAggregationWorker`, `NightlyAnalyticsRefreshWorker`, `AnalyticsDataQualityHealthWorker`, and the nearest cache invalidation helpers they use
- screen-family endpoint/meta code only where refresh/materialized-view readiness must be exposed truthfully
- focused worker/cache/endpoint tests
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`
- the `RQ110` matrix/proof output
- nearest worker/cache tests for the affected family

### Do

1. Use the `RQ110` output to pick the smallest proven refresh/cache/materialized-view failure family.
2. Ensure a successful refresh or worker completion invalidates or refreshes the minimum required screen-family caches so existing data becomes visible without waiting for misleading TTL behavior.
3. If a screen depends on a materialized view that is not current, expose that as explicit freshness/warning state instead of returning a trusted-looking blank result.
4. Preserve successful empty semantics for truly empty datasets; do not turn real empty into fake "data exists" or vice versa.
5. Add focused regression tests for:
   - successful refresh -> screen family no longer serves stale empty data;
   - failed refresh -> cache/data remains clearly stale/blocked, not healthy;
   - materialized-view lag -> visible warning/degraded truth rather than silent blankness.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: proved the dashboard bootstrap family rebuilds fresh summary values after cache invalidation and a new authoritative sale lands in the active date window.
- Checks run: `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `git diff --check`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~CachedAnalyticsOperationalFallbackTests|FullyQualifiedName~AnalyticsAggregationWorkerTests"`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~CachedAnalyticsOperationalFallbackTests.DashboardBootstrap_AfterRefreshInvalidation_RebuildsFreshSummary"`
- Checks not run: full-solution build/test; live refresh smoke; production deployment proof
- Run log: `.ai/runs/2026-08-24-RQ111-evidence.md`
- Changed files:
  - `Api.Tests/CachedAnalyticsOperationalFallbackTests.cs`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
  - `MASTER_ROADMAP.md`
  - `.ai/runs/2026-08-24-RQ111-evidence.md`
- Main commit SHA: `11610dc2e27bbd486eeb27d797bc1a5d0151ab18`
- Main verification: `git merge-base --is-ancestor 11610dc2e27bbd486eeb27d797bc1a5d0151ab18 origin/main -> ancestor=true`
- Missed: no additional miss inside the RQ111 acceptance scope; `RQ112` remains the next queued follow-up
- Evidence state: synchronized
- Delivery mode: main delivered
- Follow-up: `RQ112`
- Residual risk: the refreshed dashboard proof is still focused on the first proven family; other families remain staged behind their own prompts

### Tests

- `git diff --check`
- focused worker/cache invalidation tests
- focused endpoint/meta contract tests for the affected screen family
- governance validators if queue docs change

### Acceptance

- The first proven refresh/cache/materialized-view gap that can hide existing data is closed.
- A named analytics screen family no longer returns a stale empty/trusted-looking blank state after successful refresh when the authoritative source contains data.
- Failed or lagging refresh remains visible as warning/degraded truth.

### Dependencies

- `RQ110` DONE.
- Do not broaden into a repo-wide performance or telemetry program; keep the fix inside the first proven reliability family.

---

## RQ112 - Reconcile pilot analytics summary values against detail/export on the first proven family

Status: DONE
Ready after: `RQ111` is `DONE`
Priority: P1
Type: backend/tests/docs
Feature family: analytics-summary-detail-reconciliation
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ112-<agent>.lock.md`
Commit suggestion: `test(analytics): reconcile pilot summary and detail truth`

### Problem

After `RQ110` and `RQ111`, a pilot analytics screen may be non-empty and freshly refreshed yet still numerically misleading if its summary cards, table rows, chart totals, and export values do not reconcile for the same authoritative seeded basis. Trendplus needs one current-main proof that the first proven family with both summary and detail surfaces is either numerically aligned or explicitly labeled when denominators differ.

### Evidence

- `RQ110` proves whether a screen can stay data-bearing when authoritative seeded data exists, but it does not by itself prove that screen-level summaries reconcile to the underlying detail surface.
- `RQ111` closes the first refresh/cache/materialized-view family that can hide existing data, but it still does not prove that the now-visible numbers match one another.
- Earlier prompt families closed isolated correctness gaps such as mixed denominators (`RQ02`, `RQ12`, `RQ83`) and cross-surface numeric drift (`RQ40`, `RQ55`), but not one current-main pilot-family proof that summary, detail, and export use the same defended semantics.
- Pilot analytics trust depends not only on visible data, but on the operator being able to defend why the headline number matches the underlying drilldown or why it intentionally does not.

### Scope

- the first pilot screen family identified by `RQ110`/`RQ111` that has:
  - a summary/header/KPI surface; and
  - a table/detail and/or export surface
- the nearest query/endpoint/DTO/test files for that single family only
- one dated `docs/qa/` reconciliation note or a scoped extension to `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- the final `RQ110` matrix/proof output
- the final `RQ111` runtime-gap output
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- the nearest summary/detail/export tests for the chosen family
- the chosen family's endpoint/query files

### Do

1. Use the `RQ110` and `RQ111` outputs to choose the smallest current-main pilot family where summary and detail/export can both be proven from the same seeded basis.
2. Add one deterministic reconciliation fixture that names:
   - authoritative source rows or snapshot basis;
   - requested/effective period and scope;
   - expected summary values;
   - expected detail/export values;
   - any allowed intentional denominator or coverage difference.
3. Make summary, detail, chart, and export semantics align for that family, or add additive metadata that explains the intentional difference without silently changing business meaning.
4. Do not let dropped rows, hidden unknown buckets, stale cached totals, or unit conversions create a trusted-looking headline number that the underlying surface cannot defend.
5. Keep the fix inside one family; if a second family shows the same failure, record it as follow-up evidence rather than broadening this prompt silently.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: reconciled the supplier decision family so summary metrics, detail sections, and export payload rows all match the same authoritative seeded basis.
- Changed files:
  - `Api.Tests/AnalyticsReportsContractTests.cs`
  - `docs/qa/ANALYTICS_SUPPLIER_SUMMARY_DETAIL_RECONCILIATION_2026-08-24.md`
  - `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-24-RQ112-evidence.md`
- Checks run: `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `git diff --check`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~AnalyticsReportsContractTests|FullyQualifiedName~SupplierNegotiationPackReportTests|FullyQualifiedName~SupplierDecisionHubContractTests"`
- Checks not run: full solution build/test; live smoke / production proof
- Run log: `.ai/runs/2026-08-24-RQ112-evidence.md`
- Main commit SHA: `42b6b38691d46e44c67ba0e5c36a21427755d09a`
- Main verification: `git merge-base --is-ancestor 42b6b38691d46e44c67ba0e5c36a21427755d09a origin/main -> ancestor=true`
- Missed: no intentional denominator split was needed for the first proven family
- Evidence state: synchronized
- Delivery mode: main delivered
- Follow-up: `RQ113`
- Residual risk: other analytics families still need their own staged reconciliation proofs

### Tests

- `git diff --check`
- focused backend tests for the chosen summary/detail family
- focused export/detail parity tests if an export surface is touched
- governance validators if queue docs change

### Acceptance

- One current-main pilot family has a deterministic reconciliation proof from authoritative seeded basis to summary and detail/export output.
- Summary values no longer overstate, understate, or silently redefine the same dataset relative to the underlying surface.
- Any intentional denominator or coverage split is explicit in contract metadata or proof documentation.

### Dependencies

- `RQ110` DONE.
- `RQ111` DONE.
- Do not broaden into multi-family audit work; prove the first family completely.

---

## RQ113 - Expose exact freshness/provenance truth for the first pilot family that still looks trusted by inference

Status: DONE
Ready after: `RQ112` is `DONE`
Priority: P1
Type: backend/frontend-contract/tests
Feature family: analytics-generation-provenance-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ113-<agent>.lock.md`
Commit suggestion: `fix(analytics): expose pilot generation provenance truth`

### Problem

Even when a pilot analytics family is non-empty and numerically reconciled, it can still be weakly evidenced if the operator cannot tell which refresh/materialized-view generation produced it, whether it came from a fallback path, or whether the visible period/scope is requested truth or effective fallback. Trendplus should not require an operator to infer freshness or provenance from page render time, cache age, or a single borrowed timestamp.

### Evidence

- `RQ111` is intended to close the first refresh/cache/materialized-view gap, but its acceptance is about preventing hidden stale empty states, not standardizing family-level provenance truth.
- `RQ61` and `RQ105` already fixed surface-specific freshness/fallback honesty issues, yet they do not provide one current-main contract that ties visible pilot numbers to a named generation/provenance basis.
- The `RQ110` matrix makes source tables/views and refresh owners explicit, which creates the evidence foundation for a stricter provenance contract.
- Pilot release truth still depends on being able to explain not only what number is shown, but which owned refresh/basis generated it and whether fallback or degradation was involved.

### Scope

- the first pilot family from `RQ110`/`RQ111`/`RQ112` whose visible trust still depends on inferred freshness or provenance
- nearest endpoint/meta DTO files and only the minimum frontend mapping needed to surface truthful additive metadata
- focused endpoint/meta contract tests and small UI assertions only when a visible trust state changes
- one dated `docs/qa/` provenance note if a current owner doc does not already capture the new contract
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- final outputs from `RQ110`, `RQ111`, and `RQ112`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- the chosen family's endpoint/meta/frontend trust files
- nearest freshness/fallback tests already covering that family

### Do

1. Choose the smallest pilot family whose current-main trust still depends on inferred freshness, inferred fallback, or inferred effective period/scope.
2. Add only the minimum additive contract fields needed to expose:
   - requested period/scope;
   - effective period/scope when fallback occurs;
   - refresh/materialized-view generation or equivalent provenance basis;
   - fallback/degraded/operational path state.
3. Ensure the surface does not borrow freshness or provenance from an unrelated panel or fallback branch.
4. Keep unknown or unavailable provenance explicit as unknown/unavailable; do not coerce it into fresh, healthy, or measured.
5. If a visible UI label changes, keep the wording aligned to backend truth rather than inventing new scoring language on the frontend.

### Tests

- `git diff --check`
- focused endpoint/meta contract tests for the chosen family
- focused UI trust-state tests only if visible copy or branching changes
- governance validators if queue docs change

### Acceptance

- One pilot analytics family can expose why its data is trusted using explicit requested/effective period, provenance, and fallback truth instead of inference.
- Unknown or degraded provenance no longer looks fresh or fully authoritative.
- The frontend does not invent provenance semantics that the backend contract does not own.

### Dependencies

- `RQ110` DONE.
- `RQ111` DONE.
- `RQ112` DONE.
- Keep the scope to one family and one provenance contract.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added an explicit provenance basis contract for the supplier decision hub and supplier sales stats family, surfaced it in the shared trust header / snapshot UI and report payload metadata, and verified the focused backend/frontend contract tests.
- Changed files: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Endpoints/AllEndpoints.cs`, `Api.Tests/SupplierDecisionHubContractTests.cs`, `Api.Tests/SupplierDecisionSchemaSqlTests.cs`, `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`, `Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx`, `Klijent/clientapp/src/components/supplierDecisionHub/SupplierExplainabilitySnapshot.tsx`, `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`, `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`, `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`, `Klijent/clientapp/src/services/supplierDecisionReport.ts`, `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `Klijent/clientapp/src/pages/supplierSharedState.ts`, `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`, `Klijent/clientapp/src/services/__tests__/supplierDecisionHubApi.spec.ts`, `docs/qa/ANALYTICS_GENERATION_PROVENANCE_TRUTH_2026-08-24.md`
- Contract/runtime behavior changed: supplier analytics trust surfaces now expose a backend-led provenance basis instead of leaving refresh/materialized-view generation implicit; the supplier decision hub still carries requested/effective dataset and fallback state, while supplier sales stats now carries a live-query/snapshot provenance basis.
- Checks run: `git diff --check` (pass); `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests|FullyQualifiedName~SupplierDecisionSchemaSqlTests|FullyQualifiedName~DecisionPulseProjectorTests|FullyQualifiedName~DecisionBoardEndpointsTests"` (pass); `npm ci` in `Klijent/clientapp` (pass); `npm run test:run -- src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx src/services/__tests__/supplierDecisionHubApi.spec.ts` (pass after one assertion refinement); `npm run test:run -- src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` (pass)
- Checks not run: full repo build/test suites; not needed after the focused contract and UI proof passed
- Run log: `.ai/runs/2026-08-24-RQ113-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: 25ec243515becb9d1c6bc47561cd08ba6af35cf4
- Main verification: current main contains 25ec243515becb9d1c6bc47561cd08ba6af35cf4
- Missed: none known
- Follow-up: RQ114
- Residual risk: the supplier sales stats provenance basis is intentionally string-based (`live_query` or `live_query/snapshot_cost_batch_<id>`) and may need future owner-doc refinement if that surface gets a stricter materialized-view contract.
- Next: RQ114
- Prompt defect / scope repair: none

---

## RQ114 - Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof

Status: DONE
Ready after: `RQ113` is `DONE`
Priority: P1
Type: tests/docs
Feature family: analytics-deterministic-seed-pack
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ114-<agent>.lock.md`
Commit suggestion: `test(analytics): add deterministic pilot proof seed pack`

### Problem

Trendplus now has growing current-main proof needs for pilot analytics, but many focused checks still rely on one-off seeded fixtures or prompt-local reasoning. Without a reusable deterministic seed pack and expected-output manifest, future reliability prompts can pass locally while proving different implicit datasets, which weakens both repeatability and evidence quality.

### Evidence

- `RQ110` introduces a named screen-data availability matrix, which is a strong start, but it does not by itself create a reusable authoritative seed pack for future prompts.
- `RQ112` and `RQ113` depend on deterministic seeded bases and expected outputs; without a shared pack, later prompts can drift in what they consider the authoritative proof dataset.
- Existing analytics tests already contain seeded cases across dashboard, product decision, decision board, inventory, supplier, and actions, but they are spread across hosts and are not yet documented as one reusable pilot proof basis.
- Pilot-readiness claims are stronger when repeated prompts can cite the same known seed set, expected rows, expected warnings, and allowed empty/degraded states.

### Scope

- test fixtures/builders/seed helpers already used by the pilot analytics test hosts
- one new `docs/qa/` manifest that names the canonical seed pack, its authoritative basis, and expected outputs by screen family
- minimal test-host changes needed so later prompts can reuse the same seed pack instead of cloning ad hoc datasets
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- final outputs from `RQ110`, `RQ112`, and `RQ113`
- the pilot screen-family test hosts named by `RQ110`
- the nearest existing seed helpers/builders for those hosts
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Do

1. Create one reusable deterministic seed pack for pilot analytics proof that can back at least the main families already exercised by `RQ110` through `RQ113`.
2. Document, for each included family:
   - authoritative source rows/snapshots;
   - requested/effective filters;
   - expected non-empty outputs;
   - allowed explicit empty/degraded/warning outcomes.
3. Reuse existing test helpers where possible; do not create a second parallel seed system without a clear owner reason.
4. Add only the smallest test-host hooks needed so later reliability prompts can consume the same pack with stable names and stable expected-output references.
5. Keep the seed pack deterministic and current-main-friendly; do not introduce runtime-only or environment-specific proof requirements.

### Tests

- `git diff --check`
- focused tests for any touched seed helpers or hosts
- governance validators if queue/docs metadata changes

### Acceptance

- Trendplus has one reusable deterministic pilot analytics seed pack and expected-output manifest that later prompts can cite directly.
- Future reliability prompts no longer need to reinvent the authoritative seeded basis for the same pilot families.
- The proof basis stays compatible with explicit empty/degraded semantics instead of forcing every family to look non-empty.

### Dependencies

- `RQ110` DONE.
- `RQ112` DONE.
- `RQ113` DONE.
- Keep this prompt at reusable proof-harness scope; do not broaden into general integration-test refactoring.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added a reusable pilot analytics seed pack and expected-output manifest, then switched the product-decision, inventory, and analytics-actions host tests to the shared pack so later prompts can cite one canonical proof basis instead of cloning ad hoc fixtures.
- Changed files: `Api.Tests/PilotAnalyticsSeedPack.cs`, `Api.Tests/PilotAnalyticsSeedPackTests.cs`, `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`, `Api.Tests/InventoryListEndpointIntegrationTests.cs`, `Api.Tests/AnalyticsActionsEndpointsTests.cs`, `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`
- Contract/runtime behavior changed: pilot proof fixtures now have one reusable shared seed pack and manifest; product-decision and inventory seeds stay aligned to the shared helper, while inventory keeps a runtime-relative freshness base to preserve the out-of-stock signal path.
- Checks run: `git diff --check` (pass); `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~PilotAnalyticsSeedPackTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~InventoryListEndpointIntegrationTests|FullyQualifiedName~AnalyticsActionsEndpointsTests"` (pass, 36 passed)
- Checks not run: full repo suites; not needed after the focused proof and helper tests passed
- Run log: `.ai/runs/2026-08-24-RQ114-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: 53adf409e617aacc69449ecfa1a8939b2307bd7d
- Main verification: current main contains 53adf409e617aacc69449ecfa1a8939b2307bd7d
- Missed: none known
- Follow-up: `RQ115`
- Residual risk: inventory freshness is intentionally runtime-relative so the out-of-stock path stays exercised; later prompts should reuse the pack instead of re-seeding ad hoc timestamps.
- Next: `RQ115`
- Prompt defect / scope repair: none

---

## RQ115 - Isolate the dashboard seeded-data proof left open by RQ110

Status: DONE
Ready after: `RQ110` is `DONE`
Priority: P1
Type: docs/tests/backend-contract
Feature family: analytics-dashboard-seeded-proof
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ115-<agent>.lock.md`
Commit suggestion: `test(analytics): prove dashboard seeded data path`

### Problem

The RQ110 review explicitly found that the dashboard family has no separately named isolated seeded-non-empty backend proof. The pilot matrix currently relies on route/meta/smoke evidence for that row, so a dashboard blank state could still be confused with a valid empty dataset.

### Evidence

- `.ai/runs/2026-08-22-RQ110-evidence.md` records the dashboard gap as the primary missed item.
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md` identifies the dashboard as the least isolated proof surface.
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` records dashboard refresh/cache risk.

### Scope

- the dashboard endpoint/query and its nearest backend test host;
- the dashboard row in `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`;
- one deterministic seeded fixture or source-basis note;
- this queue and a dated evidence note.

### Read first

- the final RQ110 matrix and completion note;
- dashboard endpoint/service files and nearest focused tests;
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`.

### Do

1. Name the authoritative dashboard source, requested/effective period and scope, refresh owner, and cache identity.
2. Add the smallest deterministic seeded proof that returns non-empty dashboard data when the source basis exists.
3. If the source cannot be trusted or is unavailable, return an explicit empty/warning/provenance reason; never use a blank route shell or zero-filled fallback as proof.
4. Classify any failure as source, filter, cache, route/render, or test-harness gap and create a narrower follow-up if runtime repair is needed.

### Tests

- `git diff --check`;
- focused dashboard backend contract/integration test;
- prompt and planning validators when queue/docs change.

### Acceptance

- Dashboard has a separately citeable seeded non-empty proof or an explicit blocked/degraded contract.
- A missing dashboard row cannot be reported as healthy empty data.
- The matrix names the physical source or honestly records why it cannot be named.

### Dependencies

- `RQ110` DONE.
- Do not broaden into the full refresh/cache repair owned by `RQ111`.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added a separately citeable seeded dashboard proof, then expanded the reusable pilot analytics seed pack and manifest so the dashboard, product-decision, inventory, and actions families all share one canonical proof basis instead of cloning ad hoc fixtures.
- Changed files: `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`, `Api.Tests/PilotAnalyticsSeedPack.cs`, `Api.Tests/PilotAnalyticsSeedPackTests.cs`, `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`, `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- Contract/runtime behavior changed: the dashboard now has a deterministic non-empty seeded proof; the pilot proof pack manifest now names the dashboard basis alongside the existing reusable shared families; inventory keeps a runtime-relative freshness base to preserve the out-of-stock signal path.
- Checks run: `git diff --check` (pass); `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~PilotAnalyticsSeedPackTests|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.DashboardBootstrap_SeededData_ReturnsNonEmptyExecutiveSnapshot|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.SalesSummary_ReturnsExactScopedTotalsAndHealthyMeta|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.InventoryBalance_ReturnsExactCountsAndValueForStore"` (pass, 5 passed)
- Checks not run: full repo suites; not needed after the focused proof and helper tests passed
- Run log: `.ai/runs/2026-08-24-RQ115-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: `fb9771406bfca1e98f9a001f379c9a7e21d4e141`
- Main verification: current main contains `fb9771406bfca1e98f9a001f379c9a7e21d4e141`
- Missed: none known
- Follow-up: none
- Residual risk: inventory freshness is intentionally runtime-relative so the out-of-stock path stays exercised; later prompts should reuse the pack instead of re-seeding ad hoc timestamps.
- Next: none
- Prompt defect / scope repair: none

---

## RQ116 - Prove Decision Pulse queued/sent/disabled states without claiming unverified delivery

Status: DONE
Ready after: n/a
Priority: P1
Type: backend/tests/docs
Feature family: decision-pulse-delivery-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ116-<agent>.lock.md`
Commit suggestion: `test(analytics): prove decision pulse delivery states`

### Problem

RQ109 added scheduled Pulse generation and a delivery path, but its evidence explicitly missed live SMTP proof and did not prove a durable receipt for each attempt. Operators must be able to distinguish queued, delivered, disabled, and failed delivery without treating missing SMTP configuration as success.

### Evidence

- `.ai/runs/2026-08-22-RQ109-evidence.md` records that live SMTP send was not exercised.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` records that RQ106/RQ109 still lack external delivery proof.
- Existing delivery/config services define the runtime boundary; this prompt must not invent production credentials or recipients.

### Scope

- Decision Pulse delivery/schedule services, config, and nearest tests;
- an additive delivery-attempt/receipt contract or durable in-app state if the existing owner supports it;
- queue/docs evidence only.

### Read first

- RQ109 completion note and current Pulse service/worker tests;
- `Api/Services/Analytics/DecisionPulseDeliveryService.cs`;
- `Application/Analytics/DecisionPulse/DecisionPulseAutomationContracts.cs`;
- the current SMTP/runtime configuration contract.

### Do

1. Define explicit states such as `queued`, `sent`, `disabled`, `failed`, and `not_attempted` with safe reason codes.
2. Ensure disabled/missing SMTP or recipients cannot be reported as delivered.
3. Add deterministic tests for successful composition, disabled configuration, recipient absence, and delivery failure.
4. If external SMTP cannot be exercised, record that as an external gate and prove the local receipt/state contract instead of fabricating a live-send result.

### Tests

- `git diff --check`;
- focused Decision Pulse delivery/scheduler tests;
- governance validators if queue/docs metadata changes.

### Acceptance

- Every Pulse attempt has an honest local delivery state and reason.
- No evidence claims live SMTP delivery without an actual configured send.
- Existing empty/error suppression and tenant scope remain unchanged.

### Dependencies

- `RQ109` DONE.
- No production SMTP, recipient, or secret changes are authorized by this prompt.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Proved Decision Pulse delivery states locally with deterministic tests for source_error, recipients_missing, smtp_disabled, and successful send; added a contract note so missing SMTP or recipients stay explicit instead of looking delivered.
- Changed files: `Api.Tests/DecisionPulseServiceTests.cs`; `docs/qa/DECISION_PULSE_DELIVERY_STATE_CONTRACT_2026-08-24.md`
- Contract/runtime behavior changed: delivery attempts now have locally provable non-delivery states and a clear success path; no live SMTP proof was claimed.
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DecisionPulseServiceTests|FullyQualifiedName~Trendplus2.Tests.InventorySnapshotContractTests|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_StaleAndMismatchedScopesRemainUnpaired"` - pass (21 total, 2 targeted integration checks passed in final rerun); `git diff --check` - pass; `node scripts/check-prompt-queues.mjs` - pass
- Checks not run: live SMTP send; full repo suites
- Run log: `.ai/runs/2026-08-24-RQ116-RQ117-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `f78fcfef96863051fbeec470dafe350597ab31ff`
- Main verification: current main contains `f78fcfef96863051fbeec470dafe350597ab31ff`
- Missed: live SMTP credentialed delivery proof (intentionally out of scope)
- Follow-up: none
- Residual risk: external mail infrastructure remains unproven by design; local receipt/state contract is the durable proof
- Next: `RQ118`
- Prompt defect / scope repair: converted the prompt from gated WAITING to a local proof-and-receipt contract without inventing live delivery evidence

---

## RQ117 - Prove forecast/observed pairing availability and stale/missing semantics

Status: DONE
Ready after: n/a
Priority: P1
Type: backend/tests/docs
Feature family: forecast-observed-pair-availability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ117-<agent>.lock.md`
Commit suggestion: `test(analytics): prove forecast observed pairing availability`

### Problem

RQ108 delivered the forecast materializer and fail-closed observed pairing foundation, but its residual risk states that pairing remains dependent on the RQ96 observed daily stock foundation. A forecast comparison must be explicitly unavailable when no observed window exists, rather than silently becoming zero, trusted, or complete.

### Evidence

- `.ai/runs/2026-08-22-RQ108-evidence.md` records the observed-pair dependency as the remaining risk.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` defines RQ96 provenance and missing-history semantics.
- Current forecast materialization code already exposes provenance fields that can be tested without inventing observations.

### Scope

- inventory forecast read/materializer/pairing contracts and nearest tests;
- one dated pairing availability contract note;
- queue and run evidence.

### Read first

- RQ96, RQ97, RQ98, and RQ108 completion evidence;
- forecast materialization and observed-pairing source files;
- `Api.Tests/InventorySnapshotContractTests.cs` and related integration coverage.

### Do

1. Add deterministic fixtures for trusted paired data, missing observed data, stale observed data, and mismatched store/period scope.
2. Return explicit `trusted`, `stale`, `missing_relation`, or `unavailable` semantics with null comparison values when evidence is absent.
3. Prove forecast issue time, observed date, tenant/store scope, and provenance cannot be borrowed from unrelated rows.
4. Keep this prompt at pairing availability; do not add forecasting formulas or fabricate historical stock.

### Tests

- `git diff --check`;
- focused forecast materializer/pairing tests;
- governance validators if queue/docs metadata changes.

### Acceptance

- Paired comparisons are trusted only when both forecast and observed evidence match the requested scope/window.
- Missing, stale, and mismatched observations remain explicit and non-actionable.
- No synthetic zero or inferred freshness is used to complete a comparison.

### Dependencies

- `RQ96`, `RQ97`, and `RQ108` DONE.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Proved forecast/observed pairing availability with stale, trusted, and mismatched-scope fixtures; stale provenance is now explicit on the read path and pairings stay fail-closed when scope evidence does not match.
- Changed files: `Api.Tests/DatabaseInitializerP0IntegrationTests.cs`; `Api.Tests/InventorySnapshotContractTests.cs`; `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`; `Application/Analytics/Queries/GetInventoryForecast/InventoryForecastSnapshotProvenance.cs`; `Infrastructure/Services/Inventory/InventoryForecastSnapshotMaterializerService.cs`; `docs/qa/FORECAST_OBSERVED_PAIRING_CONTRACT_2026-08-24.md`
- Contract/runtime behavior changed: stale forecast provenance is explicit instead of implicit, and observed pairings are now visibly `stale` or `missing_observed_window` rather than borrowing unrelated evidence.
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_StaleAndMismatchedScopesRemainUnpaired"` - pass (2/2); `git diff --check` - pass; `node scripts/check-prompt-queues.mjs` - pass
- Checks not run: full repo suites; broader live DB/production verification
- Run log: `.ai/runs/2026-08-24-RQ116-RQ117-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `f78fcfef96863051fbeec470dafe350597ab31ff`
- Main verification: current main contains `f78fcfef96863051fbeec470dafe350597ab31ff`
- Missed: a wider historical comparison matrix beyond the targeted trusted/stale/mismatched fixtures
- Follow-up: none
- Residual risk: the pairing surface remains intentionally fail-closed for any evidence that does not match the exact requested window/scope
- Next: `RQ118`
- Prompt defect / scope repair: tightened the pairing contract so stale provenance is visible and null/absent comparison evidence stays non-actionable

---

## RQ118 - Close the residual unscoped Data Quality issues sales window

Status: DONE
Ready after: owner promotes the P1 dataScope residual
Priority: P1
Type: backend/tests
Feature family: data-quality-issues-scope-lineage
Parallel-safe: no
Owner: root
Local lock: `.ai/task-locks/RQ118-<agent>.lock.md` (removed after DONE)
Commit suggestion: `fix(analytics): align data quality issues scope`

### Problem

RQ05/RQ06 fixed the top-offender query path, but the audit still names `GetDataQualityIssuesHandler` as using an unscoped `sales_30d` CTE. That can leak sales from another origin into a scoped Data Quality issue list and make a warning amount look more authoritative than its source.

### Evidence

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md` marks the residual as `RQ06-F1`.
- `.ai/runs/2026-08-22-large-commit-review-evidence.md` confirms earlier work did not re-audit this handler.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` RQ06 completion notes leave this exact residual open.

### Scope

- `GetDataQualityIssuesHandler` and its nearest SQL/query tests;
- the dataScope consistency audit and this queue;
- no PDC, inventory, or supplier formula changes.

### Read first

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`;
- RQ05/RQ06 completion notes;
- the handler and `DataScopeConsistencyContractTests`.

### Do

1. Reproduce imported/existing/all cases where article and sale-header origins differ.
2. Apply the canonical sale-header scope to the sales window, or document an explicit forced-all contract in response metadata.
3. Add true-zero, missing-scope, and cross-origin regression cases.
4. Preserve existing all-scope behavior unless a before/after contract note proves it was wrong.

### Tests

- `git diff --check`;
- focused Data Quality issues and dataScope tests;
- governance validators if queue/docs change.

### Acceptance

- Scoped Data Quality issue revenue cannot include an unrelated sale origin silently.
- Missing/unknown scope is explicit, not treated as all or zero.

### Dependencies

- RQ05/RQ06 DONE; owner promotion required because this is a residual follow-up, not a new current READY task.

### Completion note

- Date: 2026-08-27
- Status: DONE
- Completion: `GetDataQualityIssuesHandler` now scopes `sales_30d` by sale-header `DataOrigin`, so imported/existing issue lists no longer mix revenue across origins
- Changed files: `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs`, `Api.Tests/DataQualityIssuesHandlerTests.cs`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`, `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`, `docs/qa/ANALYTICS_RELIABILITY_RETROSPECTIVE_AUDIT_2026-08-23.md`, `MASTER_ROADMAP.md`, `.ai/runs/2026-08-27-RQ118-evidence.md`
- Missed: none known
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DataQualityIssuesHandlerTests.Handle_ScopesSales30dByDataScope"` pass; `git diff --check` pass; `node scripts/check-prompt-queues.mjs --self-test` pass; `node scripts/check-prompt-queues.mjs` pass; `node scripts/check-planning-architecture.mjs --self-test` pass; `node scripts/check-planning-architecture.mjs` pass
- Checks not run: broader backend suite - not needed for this narrow handler regression because the focused integration test proved the residual
- Run log: `.ai/runs/2026-08-27-RQ118-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: ae8835e80676aaa0c51f5aae90e7519b8ffef9fe
- Main verification: `git branch --contains ae8835e80676aaa0c51f5aae90e7519b8ffef9fe` -> `* main`
  - Missed: RQ119 dual-origin lane was still waiting at that time and was not pulled into this same prompt; it was later closed by RQ119.
- Follow-up: none
- Residual risk: query load still depends on the same sale-header `DataOrigin` contract being accurate in source data
- Next: none
- Prompt defect / scope repair: none

---

## RQ119 - Resolve or explicitly expose PDC/inventory dual-origin scope behavior

Status: DONE
Ready after: `RQ118` is `DONE` or the owner explicitly reprioritizes the dual-origin lane
Priority: P1
Type: backend/tests/docs
Feature family: analytics-dual-origin-scope-contract
Parallel-safe: no
Owner: unassigned
Local lock: removed after DONE
Commit suggestion: `docs(analytics): freeze dual origin scope contract`

### Problem

The RQ05 audit found high-risk dual-origin or forced-all behavior in Product Decision Center, inventory insights, and Decision Board inventory cards. Without an explicit contract, users can compare scoped sales with unscoped inventory and draw a false replenishment or supplier conclusion.

### Evidence

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md` tracks `RQ05-F1` and `RQ05-F2`.
- `docs/qa/ANALYTICS_SQL_FILTER_CONSISTENCY_AUDIT.md` repeats the same unresolved scope split.
- RQ05 completion explicitly states these follow-ups were documented, not fixed.

### Scope

- one smallest proven PDC or inventory/Decision Board scope family;
- contract tests and an additive scope/provenance note;
- no SQL formula rewrite or frontend redesign.

### Read first

- both dataScope audits and RQ05 completion note;
- the selected builder/endpoint and nearest contract tests;
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

### Do

1. Choose one family and state whether article origin, sale-header origin, both, or forced-all is authoritative.
2. Add mismatch fixtures and expose requested/effective scope in metadata when the two origins cannot be aligned safely.
3. Keep recommendation/action eligibility conservative when scope evidence is mixed or unavailable.
4. Create a separate follow-up for any second family instead of broadening this task.

### Tests

- `git diff --check`;
- focused scope-lineage tests for the selected family;
- governance validators if queue/docs change.

### Acceptance

- One high-risk dual-origin family has a tested, citeable scope contract.
- Mixed-scope values are labelled/degraded rather than silently compared as like-for-like.

### Notes

- 2026-08-27: DONE. Product Decision Center now exposes explicit dual-origin provenance metadata on the response itself (`RequestedDataScope`, `ScopeAuthority`, `ScopeBreakdown`) and the integration test asserts the requested scope for both imported and existing paths. The family is now explicit about article-origin membership plus sale-header revenue, rather than silently comparing the two as though they were interchangeable.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/qa/ANALYTICS_RELIABILITY_RETROSPECTIVE_AUDIT_2026-08-23.md`
- Checks:
  - `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.ProductDecisionCenterBuilderIntegrationTests.BuildProductDecisionCenter_DataScopeSeparatesImportedAndExistingProducts"` - pass
  - `git diff --check` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
- Run log: `.ai/runs/2026-08-27-RQ119-evidence.md`
- Evidence state: synchronized
- Next: `RQ05-F2 - Inventory + Decision Board apply article dataScope or explicit forced-all meta`

### Dependencies

- `RQ118` DONE or explicit owner reprioritization.

---

## RQ120 - Surface source, denominator, and provenance metadata in the first proven pilot UI

Status: DONE
Ready after: `RQ112` and `RQ113` are `DONE`
Priority: P1
Type: frontend-contract/tests
Feature family: analytics-trust-metadata-ui-propagation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ120-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface pilot trust metadata`
Promotion note: 2026-08-25 - `RQ112` and `RQ113` are DONE on current main, so this follow-up is promoted to READY.

### Problem

Several earlier contracts added additive backend trust metadata, while earlier evidence noted that UI labels can still be absent. A numerically correct pilot result is not fully defensible if the operator cannot see its denominator, source status, effective scope, or generation/provenance state.

### Evidence

- RQ02/RQ12 introduced denominator metadata; RQ03 introduced `sourceStatus` vocabulary.
- RQ104 proves selected core pages do not invent reliability, but does not cover every pilot family or every new provenance field.
- RQ112/RQ113 are intended to select the first reconciled/provenance-backed family for this UI slice.

### Scope

- one pilot family selected by RQ112/RQ113;
- its TypeScript API type, trust header/metadata mapping, and nearest UI contract tests;
- no frontend formula or local confidence scoring.

### Read first

- final RQ112/RQ113 contracts;
- selected family backend DTO and TypeScript service/type definitions;
- RQ104 evidence and the shared analytics trust UI patterns.

### Do

1. Map backend source status, denominator scope, requested/effective period/scope, freshness, and provenance fields without renaming their meaning.
2. Render unknown/unavailable as explicit trust states; never coerce them to zero, green, fresh, or measured.
3. Add one success, true-zero, unknown/fallback, and error/empty display test for the selected family.
4. Keep machine reason codes behind the established operator mapping.

### Tests

- `git diff --check`;
- focused Vitest contract tests and analytics guardrails;
- governance validators if queue/docs change.

### Acceptance

- The first proven pilot family visibly explains the backend-owned data trust metadata.
- UI output preserves denominator, source/fallback, freshness, and effective-scope semantics.
- No local scoring or fake-zero fallback is introduced.

### Dependencies

- `RQ112` and `RQ113` DONE.
- If frontend dependencies are unavailable, record the environment failure and do not change backend semantics to satisfy the harness.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced the pilot trust metadata in the first proven UI by forwarding requested/effective scope lineage and the available effective period window into the shared supplier trust header.
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `.ai/runs/2026-08-26-RQ120-evidence.md`
- Contract/runtime behavior changed: supplier sales stats now shows the shared trust header with the source label, requested/effective scope lineage, and effective data-window truth derived from the existing API response.
- Checks run: `npm run test:run -- src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx`
- Checks not run: full repo build/test suites
- Run log: `.ai/runs/2026-08-26-RQ120-evidence.md`
- Evidence state: pending
- Delivery mode: direct-main
- Main commit SHA: ead24ae3c531802ca54a58b607f3ef107121abb2
- Main verification: `git merge-base --is-ancestor ead24ae3c531802ca54a58b607f3ef107121abb2 HEAD -> true`
- Missed: none known
- Follow-up: `RQ121`
- Residual risk: the supplier sales stats endpoint still derives scope lineage on the frontend because its backend response does not expose a dedicated requested/effective dataset contract
- Prompt defect / scope repair: none

---

## RQ121 - Expose per-row margin/recommendation trust payload in dashboard top-product tables

Status: DONE
Ready after: `RQ120` is `DONE` or the owner explicitly promotes the dashboard row-trust lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: analytics-dashboard-row-trust-payload
Parallel-safe: no
Owner: agent-system
Local lock: `.ai/task-locks/RQ121-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface dashboard row trust payload`

### Problem

Dashboard top-product tables still render margin rows with generic fallback copy like `Kvalitet marže nije dostupan`, while the backend DTO/type layer still carries TODOs for row-level margin-quality tier, cost-coverage, and recommendation-quality payload. A row can look financially meaningful without showing whether margin evidence is missing, partial, or intentionally unavailable.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` still marks `TopProductAdvancedItemDto` with a backend DTO TODO to expose per-row margin quality tier / cost coverage and recommendation quality payload.
- `Klijent/clientapp/src/types/analytics.ts` keeps the matching TODO on `TopProductAdvancedItem`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` falls back to `Kvalitet marže nije dostupan` for table rows instead of rendering a proven row-level trust contract.
- Earlier trust work (`RQ18`, `RQ45`) already established that hidden coverage fields make margin output look more trustworthy than the evidence allows.

### Scope

- dashboard top-product backend DTO/query mapping;
- `TopProductAdvancedItem` TypeScript contract and the nearest dashboard row rendering/tests;
- no margin formula rewrite, no ranking-score rewrite, and no Supplier Decision Hub changes.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT_ADVANCED_V2.md` and `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`.

### Do

1. Decide the smallest truthful row contract: real margin-quality / cost-coverage fields when available, otherwise an explicit unavailable/insufficient row state.
2. Propagate that contract through DTOs and frontend types without inventing frontend-only scoring.
3. Replace the generic disclaimer with row-level trust text/badges that distinguish missing evidence from true zero or healthy coverage.
4. Add focused regression coverage for good, warning/partial, and unavailable margin rows.

### Tests

- `git diff --check`;
- focused dashboard/backend contract tests for top-product rows;
- focused frontend/Vitest tests for row trust rendering;
- governance validators if queue/docs change.

### Acceptance

- Dashboard top-product rows visibly explain whether margin/recommendation trust is good, partial, or unavailable.
- Unknown coverage is not presented as normal-looking margin confidence.
- The dashboard no longer relies on a generic shared disclaimer when row-level evidence is actually the missing contract.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

---

## RQ134 - Prove supplier summary freshness after successful aggregate refresh

Status: DONE
Ready after: `RQ111` is `DONE` and the owner explicitly promotes the supplier-summary cache-parity lane
Priority: P1
Type: backend/workers/cache/tests
Feature family: supplier-summary-aggregation-refresh-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ134-<agent>.lock.md`
Commit suggestion: `fix(analytics): refresh supplier summary after aggregate worker`

### Problem

Supplier decision summary surfaces can remain on TTL-managed cache after `AnalyticsAggregationWorker` refreshes the aggregate tables they depend on. The worker already clears the dashboard family and dashboard aggregate-backed prefixes, but the supplier-decision-hub family is not part of the same invalidation path. That leaves supplier summary responses able to lag behind successful aggregate refreshes, which makes freshness look stronger or more current than the system can prove.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` says supplier summary cards that use aggregate tables can lag until TTL expiry and marks supplier-decision-hub as a P1 follow-up after aggregation-worker refresh.
- `Workers/AnalyticsAggregationWorker.cs` currently clears `AnalyticsCachePolicy.DashboardFamily` plus dashboard aggregate-backed prefixes only.
- `Api.Tests/AnalyticsAggregationWorkerTests.cs` only asserts dashboard-prefix invalidation.
- `AnalyticsCachePolicy.CoreFamilies` already includes `SupplierDecisionHubFamily`, so the family is first-class even though the aggregation worker does not currently touch it.

### Scope

- `Workers/AnalyticsAggregationWorker.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` only if the shared cache contract needs a new assertion
- `Api.Tests/SupplierDecisionHubContractTests.cs` or `Api/Endpoints/SupplierDecisionHubEndpoints.cs` only if freshness must be surfaced explicitly instead of being cleared
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `Workers/AnalyticsAggregationWorker.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- the nearest supplier summary/cache contract tests

### Do

1. Decide the smallest truthful contract for supplier summary after a successful aggregate refresh: clear the supplier-decision-hub family, or expose an explicit stale/lag state if the family is intentionally TTL-bound.
2. Prove the selected contract with focused tests for success and failure paths, including at least one counterexample that would have left stale supplier summary data visible before the fix.
3. Keep dashboard bootstrap/report freshness behavior out of scope.
4. If the prompt chooses explicit stale/lag state, add the smallest metadata path that tells the operator the summary is stale instead of letting TTL masquerade as freshness.
5. Do not broaden into nightly refresh, report generation, or inventory signal panels.

### Tests

- `git diff --check`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~SupplierDecisionHubContractTests"`
- focused frontend test only if supplier summary rendering changes
- governance validators if queue/docs change

### Acceptance

- Supplier summary freshness after aggregate refresh is either immediately cleared/refreshed or explicitly labeled as stale/lagging.
- The worker/cache contract is proven by tests rather than inferred from TTL behavior.
- Dashboard bootstrap and report freshness remain unchanged and out of scope.

### Dependencies

- `RQ111` DONE.
- No live-production proof is required for the queue prompt itself.

### Promotion note

- Date: 2026-09-01
- Status: READY
- Promotion: owner-promoted after the cache invalidation audit identified supplier summary lag after successful aggregate refresh
- Next: implement cache parity proof on the worker/test path

### Completion note

- Date: 2026-09-01
- Status: DONE
- Completion: supplier summary freshness now follows the aggregate refresh invalidation path because `AnalyticsAggregationWorker` clears the supplier-decision-hub family alongside the dashboard family
- Changed files: `Workers/AnalyticsAggregationWorker.cs`; `Api.Tests/AnalyticsAggregationWorkerTests.cs`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-09-01-RQ134-evidence.md`
- Checks run: `git diff --check`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (18 passed)
- Checks not run: full solution build; wider frontend regression tests
- Run log: `.ai/runs/2026-09-01-RQ134-evidence.md`
- Delivery mode: local-workspace
- Main commit SHA: uncommitted
- Main verification: not verified; the work remains local in this workspace
- Missed: none
- Follow-up: `RQ128` once `STAB16` is resolved
- Residual risk: other cache paths still use the existing TTL-based contract where the worker does not explicitly clear them

---

## RQ135 - Refresh trust-bearing analytics caches after data-quality snapshot

Status: DONE
Completed after: `RQ134` is `DONE` and the owner explicitly promoted the data-quality trust-propagation lane
Priority: P1
Type: backend/workers/cache/tests
Feature family: data-quality-trust-propagation-after-snapshot
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ135-<agent>.lock.md`
Commit suggestion: `fix(analytics): refresh trust caches after data quality snapshot`

### Problem

`AnalyticsDataQualityHealthWorker` captures a new quality snapshot, saves it durably, and then clears only the `data-quality` and `reports` cache families. The audit still records medium-risk lag on dashboard, product-decision-center, supplier-decision-hub, and inventory trust surfaces when only the quality snapshot changes, which means those operator-facing trust callouts can stay one TTL behind the newest evidence.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` says `AnalyticsDataQualityHealthWorker` clears `data-quality` and `reports`, while dashboard/product-decision-center/supplier-decision-hub/inventory trust surfaces are not explicitly cleared and may lag until TTL expiry.
- `Workers/AnalyticsDataQualityHealthWorker.cs` currently clears only `AnalyticsCachePolicy.DataQualityFamily` and `AnalyticsCachePolicy.ReportsFamily`.
- `AnalyticsCachePolicy.CoreFamilies` already includes `DashboardFamily`, `ProductDecisionCenterFamily`, `SupplierDecisionHubFamily`, and `InventoryFamily`, so the trust-bearing families are first-class cache targets.
- `AnalyticsCacheAdminServiceTests` already prove that report-family invalidation bumps the report cache version, so this follow-up should preserve that contract if reports remain in the clear set.

### Scope

- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` only if the shared cache contract needs a new assertion
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Infrastructure/Services/AnalyticsDataQualityHistoryService.cs`
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs`

### Do

1. Decide the smallest truthful contract after a successful data-quality snapshot: clear the trust-bearing families that consume the snapshot, or expose an explicit stale/lag state if those families are intentionally TTL-bound.
2. Prove the selected contract with focused tests for a successful snapshot refresh and a failure path that leaves cache state untouched.
3. Keep aggregation-worker, nightly-refresh, and report-template behavior out of scope.
4. Preserve the existing report-version bump behavior owned by the worker if reports remain in the clear set.
5. Do not broaden into dashboard redesign or recommendation-scoring changes.

### Tests

- `git diff --check`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthServiceTests"` only if the snapshot contract needs a new counterexample
- governance validators if queue/docs change

### Acceptance

- Successful data-quality snapshot refreshes do not leave dashboard, product-decision-center, supplier-decision-hub, or inventory trust surfaces one TTL behind the newest quality evidence.
- Failure paths remain fail-closed and do not clear caches.
- Report freshness semantics stay truthful and unchanged except for the existing data-quality worker behavior.

### Completion note

- Date: 2026-09-01
- Status: DONE
- Completion: `AnalyticsDataQualityHealthWorker` now clears the trust-bearing dashboard, product-decision-center, supplier-decision-hub, inventory, data-quality, and reports cache families after a successful snapshot refresh, so the operator trust surfaces no longer wait for TTL expiry.
- Changed files: `Workers/AnalyticsDataQualityHealthWorker.cs`; `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`; `.ai/runs/2026-09-01-RQ135-evidence.md`
- Checks run: `git diff --check`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (17 passed)
- Checks not run: full solution build; wider frontend regression tests
- Run log: `.ai/runs/2026-09-01-RQ135-evidence.md`
- Delivery mode: local-workspace
- Main commit SHA: uncommitted
- Main verification: not verified; the work remains local in this workspace
- Missed: none
- Follow-up: `RQ128` once `STAB16` is resolved
- Residual risk: other cache paths still use the existing TTL-based contract where the worker does not explicitly clear them

### Dependencies

- `RQ134` DONE.
- No production mutation or worker scheduling change is authorized outside this worker/test path.

### Promotion note

- Date: 2026-09-01
- Status: READY
- Promotion: owner-promoted after the cache invalidation audit identified medium-risk trust lag on dashboard/product/supplier/inventory surfaces after data-quality snapshot refresh
- Next: implement trust-cache parity on the data-quality worker/test path

---

## RQ132 - Explain Dashboard support-signal limits and the next safe operator step

Status: WAITING
Ready after: `STAB16` is DONE and the canonical production API has a healthy runtime/refresh-status proof
Priority: P1
Type: backend-frontend-contract/tests
Feature family: dashboard-support-signal-explainability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ132-<agent>.lock.md`
Commit suggestion: `fix(analytics): explain dashboard support-signal limits`

### Problem

The Dashboard currently repeats the generic Serbian copy `Prikazani su pomoćni signali. Signal je ograničen zbog kvaliteta ili nedovoljno podataka.` when all displayed actions have `recommendationAllowed=false`. That condition proves only that the shown recommendations are blocked; it does **not** prove that the selected period/store has no source data. The backend often has a specific reason (`missing_cost`, `missing_supplier`, `insufficient_history`, critical/stale/unknown freshness, or a legacy action with unavailable trust payload), but the Dashboard does not turn it into a single operator-facing diagnosis, affected scope/count, and next safe action. A source/API failure must remain an error/partial state, never a support-signal explanation.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` sets `recommendationsBlocked` when every one of up to four prioritized actions has `recommendationAllowed === false`, then renders the same generic explanation both in the cockpit banner and on each blocked card.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` can build Product Decision action reasons from backend rows, including `FIX_DATA`, `INSUFFICIENT_DATA`, `DataQualityStatus`, `RecommendationReason`, and warning/reason codes, but `DashboardDecisionActionDto` does not expose a bounded, display-ready block-cause contract.
- The existing Product Decision profile already distinguishes `missing_cost`, `missing_supplier`, `insufficient_history`, critical data quality, and stale/unknown input freshness. Those causes must remain backend-owned and must not be recreated by a frontend score heuristic.
- The legacy advanced fallback can carry explicit trust metadata, or it can be an unavailable legacy helper payload. `RQ124` made the distinction representable, but the Dashboard still presents a generic limit sentence instead of explaining it to an operator.
- On 2026-08-31 the public production API returned HTTP 500 even for liveness, runtime-version, refresh-status, and dashboard bootstrap routes. Until `STAB16` restores liveness, the live UI cannot establish whether a visible limit came from absent data, stale refresh, partial/fallback content, or a failed API call.

### Scope

- Dashboard action/trust DTO composition in `Api/Endpoints/CachedAnalyticsEndpoints.cs` and existing analytics response metadata only where the authoritative cause is already known;
- `Klijent/clientapp/src/types/analytics.ts` and `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- focused backend and Dashboard regression tests;
- the nearest Dashboard/Data Quality guidance only if it must describe the new operator-facing states.

Do not change recommendation thresholds, financial calculations, Product Decision scoring, worker scheduling, or the Data Quality issue formulas. Do not make the frontend infer or count business causes from card text.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` (`BuildDashboardDecisionActions`, Product Decision confidence/warning helpers, dashboard bootstrap metadata);
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Api.Tests/CachedAnalyticsDashboardActionTrustTests.cs`;
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`;
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (`STAB16`);
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`.

### Do

1. Define the smallest additive, backend-owned Dashboard support-signal payload. It must state a normalized state such as `no_qualifying_data`, `data_quality_blocked`, `insufficient_history`, `stale_or_unrefreshed`, `legacy_trust_unavailable`, or `partial_or_failed`; include bounded reason codes, effective filter/period, and a safe next-step target when the backend can prove one.
2. Keep these states visibly distinct:
   - a successful empty requested window;
   - rows with missing master/cost data;
   - insufficient sales/history evidence;
   - stale, unknown, or not-yet-refreshed input;
   - legacy/helper content whose trust is unavailable;
   - API/section failure or partial response.
   Do not label any of the last four as “nema podataka” unless the response explicitly proves an empty source window.
3. Render one concise, deduplicated diagnosis in the Dashboard cockpit. For each state, show what is missing or degraded, the selected period/scope, and the direct safe next step: correct source fields, inspect the affected Data Quality items, restore/await refresh, widen a genuinely empty date range, or contact support with a correlation ID for a failed response.
4. Preserve the existing per-card reason as supporting detail, but do not repeat the generic warning on every card. Keep the Data Quality and worker/refresh links only where they correspond to the backend-owned cause.
5. Add focused tests for: genuinely empty data, missing cost/supplier, insufficient history, stale/unknown refresh, legacy payload without trust data, and an API/partial failure. Verify no case renders fake zero, fake green, or an actionable recommendation.

### Tests

- `git diff --check`;
- focused `CachedAnalyticsDashboardActionTrustTests` plus the smallest bootstrap/meta contract test;
- focused `AnalyticsDashboard.operationalFallback.spec.tsx` or a dedicated Dashboard support-signal presentation test;
- governance validators if queue/docs change.

### Acceptance

- An operator can tell whether there are truly no qualifying records, data is incomplete, history is too short, freshness is degraded, trust is unavailable, or the API failed.
- Every non-error support-signal state has a truthful, scoped next step; failed/partial responses point to recovery/support rather than pretending a data-quality diagnosis.
- The Dashboard uses backend-owned reason/status semantics and does not introduce frontend scoring or data-quality inference.
- The generic support-signal sentence is not duplicated as the only explanation at both cockpit and card level.

### Dependencies

- `STAB16` DONE with current-main runtime, worker/freshness, and production liveness proof.
- `RQ124` is DONE and supplies the legacy-action trust payload foundation.

---

## RQ128 - Prove Product Decision actionability parity on the exact deployed runtime

Status: WAITING
Ready after: `STAB16` is DONE with worker/freshness evidence and read-only reconciliation on the canonical Render runtime
Priority: P0
Type: backend-frontend-contract/live-evidence
Feature family: pdc-actionability-deploy-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ128-<agent>.lock.md`
Commit suggestion: `test(analytics): prove product decision actionability parity in production`

### Problem

The first 2026-08-27 production audit found PDC rows looking actionable under insufficient evidence. A same-day API-only recheck now shows the main fail-closed product repair on the canonical Render runtime, but exact live parity still cannot be claimed until `STAB16` closes worker/freshness, browser, and read-only reconciliation proof on that runtime family.

### Evidence

- A same-day 2026-08-27 API-only recheck returned runtime `commitSha=6ecbfa67a7304c3cbeeb71755a35255e766c8e24`, which is contained in current `main`.
- The same recheck returned 50 visible rows from 12,422 analyzed rows with 12 visible rows already blocked by `recommendationAllowed=false`, showing the product fail-closed path is now live for clearly blocked cases.
- `/api/analytics/refresh-status?dataScope=all` still returned `workersEnabled=false`, process `web`, unknown freshness, an in-memory-cache warning, and zero successful job timestamps, so exact live parity cannot yet be claimed.
- `CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(...)` now centrally clears recommendation allowance, decision confidence, and expected impact for blocked/stale/critical/unknown evidence.
- `DecisionBoardEndpoints` and `ExecutiveDecisionBoardPage.tsx` now fail closed for blocked recommendation payloads so an old numeric diagnostic value cannot look like decision confidence.
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` plus `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md` record the live observations and the remaining proof gap.

### Scope

- Product Decision Center response/profile, Decision Board aggregate card, and Executive Board presentation parity;
- focused backend/frontend regression tests and exact-deploy live evidence;
- no new recommendation formula, ranking threshold, database migration, or frontend-owned business scoring.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`;
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`;
- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`;
- `Api.Tests/DecisionBoardEndpointsTests.cs`;
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`;
- `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`.

### Do

1. Keep the backend PDC profile as the authoritative actionability gate; do not recreate it in UI code.
2. Prove the four minimum counterexamples: source-blocked, `INSUFFICIENT_DATA`/`FIX_DATA`, critical data quality, and stale/unknown freshness all return `recommendationAllowed=false`, `confidenceScore=null`, and `expectedImpactRsd=null`.
3. Prove the Decision Board removes the blocked row from executable impact ranking and renders it as insufficient/blocked with a visible reason.
4. Prove the PDC and Executive Board UIs preserve the backend block even when a compatibility payload contains an old diagnostic percentage.
5. After `STAB16`, run the same checks on the exact deployed SHA and record returned/analyzed counts separately from visible rows.

### Tests

- focused `AnalyticsProductDecisionConfidenceTests`, `ProductDecisionCenterBuilderIntegrationTests`, and `DecisionBoardEndpointsTests`;
- focused `ExecutiveDecisionBoardPage.spec.ts` and PDC confidence presentation test;
- exact-deploy API/browser smoke after `STAB16`;
- `git diff --check` and governance validators when queue/evidence docs change.

### Acceptance

- Blocked PDC rows cannot carry actionable decision confidence or expected impact through API, Board aggregation, UI, or action payload.
- A numeric diagnostic percentage is never rendered as high/medium/low recommendation confidence when the recommendation is blocked.
- Live evidence ties the PDC/Board result to the exact current-main deployed SHA and records true returned/analyzed/ignored counts.
- Empty, unknown, stale, warning, and critical states remain visibly distinct from a valid zero or healthy recommendation.

### Dependencies

- `STAB16` DONE; it supplies the exact current-main deployment, worker/freshness evidence, and read-only reconciliation path.
- No direct production data mutation or formula change is authorized in this prompt.

---

## RQ129 - Remove non-product fake confidence from blocked and insufficient Decision Board cards

Status: DONE
Ready after: n/a
Priority: P0
Type: backend-contract/tests
Feature family: decision-board-non-product-confidence-normalization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ129-<agent>.lock.md`
Commit suggestion: `fix(analytics): remove fake confidence from blocked board cards`

### Problem

The live Decision Board still exposes numeric confidence where the contract says operators should see a blocked or insufficient signal. On 2026-08-27, production inventory cards with `recommendationAllowed=false` still carried scores like `55` and `35`, and the outcome summary card returned `confidenceLevel=insufficient_data` together with `confidenceScore=0`. Those values can read like decision confidence instead of blocked evidence or an undersized sample.

### Evidence

- Live `GET /api/analytics/decision-board?dataScope=all` returned inventory cards with `recommendationAllowed=false`, `confidenceLevel=insufficient_data`, warning `inventory_recommendation_blocked`, and still `confidenceScore` values `55` / `35`.
- The same response returned the `actionsOutcome` summary card with `confidenceLevel=insufficient_data` and `confidenceScore=0` because `BuildOutcomeCards(...)` currently maps `outcomeSummary.Meta.MeasuredSampleSize` into `ConfidenceScore`.
- `DecisionBoardEndpoints.ResolveInventoryBoardConfidence(...)` currently preserves `SignalConfidencePct` even when `RecommendationAllowed == false`, and `Api.Tests/DecisionBoardEndpointsTests.cs` locks that behavior with `Assert.Equal(72m, resolved.ConfidenceScore)`.
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md` still documents “score preserved” for blocked inventory cards, but `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` separately says missing/blocked recommendation confidence must stay nullable and that outcome feedback must not become recommendation confidence by itself.

### Scope

- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- targeted backend tests in `Api.Tests/DecisionBoardEndpointsTests.cs` and `Api.Tests/DecisionBoardAggregationContractTests.cs`;
- Decision Board contract docs only where they describe the now-misleading blocked/insufficient confidence semantics;
- optional `ExecutiveDecisionBoardPage` test coverage if a rendering regression needs to be locked.

### Read first

- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- `Api.Tests/DecisionBoardEndpointsTests.cs`;
- `Api.Tests/DecisionBoardAggregationContractTests.cs`;
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`;
- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`;
- `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`.

### Do

1. Make blocked inventory cards fail closed: when `RecommendationAllowed == false`, keep `confidenceLevel=insufficient_data` but clear `confidenceScore` and `reliabilityPct` instead of preserving the signal score.
2. Separate outcome sample size from decision confidence: a small-sample or incomplete outcome summary may remain visible, but its `confidenceScore` must not be `0` or another numeric value that looks like recommendation confidence.
3. Add focused regression coverage for at least:
   - blocked inventory with signal evidence;
   - workflow-only inventory fallback;
   - outcome summary with `MeasuredSampleSize < 10`;
   - one healthy inventory or outcome counterexample that keeps legitimate confidence behavior unchanged.
4. Update the owning Decision Board/inventory contract docs to match the fixed semantics without redesigning the broader board DTO.

### Tests

- `git diff --check`;
- focused `dotnet test` for `DecisionBoardEndpointsTests` and `DecisionBoardAggregationContractTests`;
- focused frontend board test only if render behavior changes;
- governance validators when queue/docs change.

### Acceptance

- Inventory cards with `recommendationAllowed=false` cannot carry numeric decision confidence or reliability through the Decision Board API.
- Outcome summary cards with insufficient sample do not expose `confidenceScore=0` or another numeric confidence surrogate; sample size stays visible only as sample/coverage context.
- Blocked/insufficient non-product Decision Board cards remain visibly blocked without introducing fake zero, fake confidence, or frontend-owned scoring.

### Dependencies

- `RQ13` is historical DONE and may be refined here only within the same Decision Board confidence family.
- No production mutation, worker configuration, or formula rewrite is authorized in this prompt.

### Completion note

- Date: 2026-08-28
- Status: DONE
- Completion: cleared fake Decision Board confidence on blocked inventory cards by nulling decision confidence/reliability when `recommendationAllowed=false`, separated outcome sample size from `confidenceScore`, and added focused backend regression coverage for blocked inventory and insufficient-sample outcome summaries.
- Changed files: `Api/Endpoints/DecisionBoardEndpoints.cs`; `Api.Tests/DecisionBoardEndpointsTests.cs`; `Api.Tests/DecisionBoardAggregationContractTests.cs`; `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-28-RQ129-evidence.md`
- Contract/runtime behavior changed: yes; blocked inventory Decision Board cards now keep `confidenceLevel=insufficient_data` while returning `confidenceScore=null` and `reliabilityPct=null`, and insufficient-sample `actionsOutcome` summaries keep sample context in copy instead of exposing numeric pseudo-confidence
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DecisionBoardEndpointsTests|FullyQualifiedName~DecisionBoardAggregationContractTests"` (pass); `git diff --check` (pass); `node scripts/check-agent-instructions.mjs --self-test` (pass); `node scripts/check-agent-instructions.mjs` (pass); `node scripts/check-prompt-queues.mjs --self-test` (pass); `node scripts/check-prompt-queues.mjs` (pass); `node scripts/check-planning-architecture.mjs --self-test` (pass); `node scripts/check-planning-architecture.mjs` (pass)
- Checks not run: full solution build/test not run; live production recheck not run in this prompt
- Run log: `.ai/runs/2026-08-28-RQ129-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `08abe2bff58c561f64e3c58ea231d249376c6af9`
- Main verification: `passed - origin/main contains 08abe2bff58c561f64e3c58ea231d249376c6af9`
- Missed: no live redeploy/runtime verification was attempted here because this prompt only corrected the backend contract/tests/docs on current `main`
- Follow-up: no additional RQ prompt is READY; `RQ128` remains `WAITING` on `STAB16`
- Residual risk: the production API will continue showing the old numeric values until the updated backend runtime is deployed on the active Decision Board environment
- Prompt defect / scope repair: historical completion-note blocks for earlier RQ prompts were already adjacent below this section before this claim; they were preserved to avoid a broader queue-structure rewrite inside this same-owner contract fix

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced backend-owned trust payload on dashboard legacy/advanced action cards by extending the dashboard action DTO, preserving actionable/blocked/legacy trust states in the advanced fallback bridge, and proving the new rendering contract in backend and frontend regression tests.
- Changed files: `Api/Endpoints/CachedAnalyticsEndpoints.cs`; `Api.Tests/CachedAnalyticsDashboardActionTrustTests.cs`; `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`; `Klijent/clientapp/src/types/analytics.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ124-evidence.md`
- Contract/runtime behavior changed: dashboard legacy/advanced action cards now carry explicit trust metadata instead of collapsing to one generic `insufficient_data` fallback, while legacy/unavailable fallback still stays explicit when trust payload is missing
- Checks run: `git diff --check` (pass); `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsDashboardActionTrustTests|FullyQualifiedName~CachedAnalyticsOperationalFallbackTests"` (pass); `npm run test:run -- src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ124-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: pending
- Main verification: pending
- Missed: the broader cross-surface trust/freshness lanes remain for RQ125-RQ127
- Follow-up: `RQ125` is now READY
- Residual risk: legacy dashboard action cards still rely on the advanced fallback bridge when Product Decision rows are absent, so any future backend schema drift should be caught by the new trust-state regression test before it reaches the UI

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: Added backend-owned row-trust payload to advanced top-product DTOs, surfaced it in the dashboard margin column as explicit trust badges/details, and added backend/frontend contract coverage for good vs insufficient-data rows.
- Changed files: Api/Endpoints/CachedAnalyticsEndpoints.cs; Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs; Klijent/clientapp/src/pages/AnalyticsDashboard.tsx; Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx; Klijent/clientapp/src/types/analytics.ts; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-26-RQ121-evidence.md
- Contract/runtime behavior changed: yes; dashboard margin rows now show a trust badge and explanatory detail instead of generic fallback copy
- Checks run: npm run test:run -- src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx (pass); dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.TopProducts_ExposesMarginTrustPayloadForDashboardRows" (pass)
- Checks not run: full solution build; broader suite; direct cached top-products-advanced endpoint against InMemory factory because that route requires relational SQL behavior
- Run log: .ai/runs/2026-08-26-RQ121-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: pending
- Main verification: pending
- Missed: supplier trust payload remains the next lane in RQ122; no formula/ranking rewrite was attempted
- Follow-up: RQ122 READY
- Residual risk: advanced top-products data still depends on the existing SQL path for real runtime data; the new trust payload itself is derived conservatively from margin-impact availability
- Prompt defect / scope repair: the cached advanced top-products route cannot be exercised end-to-end in the InMemory integration factory, so the backend check was shifted to a serialization contract test tied to the actual DTO namespace

---

## RQ122 - Surface backend-owned trust state on supplier summary/quadrant/header recommendations

Status: DONE
Completed after: `RQ112` and `RQ120` are `DONE`, or the owner explicitly promotes the supplier trust-payload lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: supplier-decision-recommendation-trust-payload
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ122-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface supplier recommendation trust`

### Problem

Supplier Decision Hub SQL/tests already preserve missing-evidence guardrails, but the API/UI still hide part of that trust context on summary cards, quadrant items, and supplier header payloads. Operators can see a recommendation, revenue, and confidence label without the backend-owned reliability/data-quality/status-reason context that proves whether the recommendation is actually decision-safe.

### Evidence

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs` still carries backend DTO TODOs for `SummarySupplierItem`, `QuadrantItem`, `RankingItem`, and `SupplierHeaderDto` to expose recommendation quality payload and margin-quality context.
- `Klijent/clientapp/src/services/supplierDecisionHubApi.ts` omits reliability/data-quality/status-reason fields from `SummarySupplierItem`, `QuadrantItem`, and `SupplierHeaderDto`, even though `RankingItem` already carries part of that vocabulary.
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx` currently shows revenue and confidence copy but no explicit trust/degradation reason.
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md` notes that supplier-decision SQL already keeps explicit cost-coverage / missing-evidence flags and conservative `REVIEW_QUALITY` fallback.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced backend-owned supplier recommendation trust on summary, quadrant and header contracts; rendered the trust context in the recommendation rail, quadrant tooltip and supplier detail header; and added focused contract/UI tests so the backend-owned reliability, data-quality and status-reason payload no longer disappears between API and UI.
- Changed files: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`; `Api.Tests/SupplierDecisionHubContractTests.cs`; `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`; `Klijent/clientapp/src/services/__tests__/supplierDecisionHubApi.spec.ts`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionQuadrant.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDetailDrawer.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.spec.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDetailDrawer.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ122-evidence.md`
- Contract/runtime behavior changed: supplier decision summary items, quadrant items and supplier header payloads now carry backend-owned reliability/data-quality/status-reason/reason-codes fields; the rail, quadrant tooltip and header drawer now render the trust state explicitly instead of implying stronger confidence by omission
- Checks run: `git diff --check` (pass); `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests"` (pass); `npm run test:run -- src/components/supplierDecisionHub/SupplierRecommendationRail.spec.tsx src/components/supplierDecisionHub/SupplierDetailDrawer.spec.tsx src/services/__tests__/supplierDecisionHubApi.spec.ts` (pass); `npm run typecheck` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ122-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `569705f11ba0db22fcb0e13b88c1ca7c3a971878`
- Main verification: `git branch --contains 569705f11ba0db22fcb0e13b88c1ca7c3a971878 -> * main`
- Missed: no dedicated hover regression for the quadrant tooltip itself; the new trust text is covered by component-level rendering and contract tests
- Follow-up: none for this prompt; `RQ123` remains `WAITING`
- Residual risk: older API consumers that do not yet send the new trust fields will need to be upgraded to avoid empty trust lines in the new UI surfaces

### Scope

- Supplier Decision Hub summary/quadrant/header DTOs and frontend contracts;
- the rail/header rendering and nearest contract tests;
- no supplier score weighting rewrite, no SQL score formula rewrite, and no report-template redesign.

### Read first

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`;
- `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`;
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx`;
- `docs/qa/ANALYTICS_SUPPLIER_SUMMARY_DETAIL_RECONCILIATION_2026-08-24.md`;
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`.

### Do

1. Expose the smallest additive backend-owned recommendation trust payload on summary, quadrant, and header contracts.
2. Render degraded/partial/review-quality states explicitly instead of leaving the rail/header to imply stronger trust than the backend proved.
3. Preserve existing recommendation codes and confidence semantics; do not invent frontend fallback formulas.
4. Add focused tests for good, degraded/review-quality, and unavailable trust payloads.

### Tests

- `git diff --check`;
- focused Supplier Decision Hub contract tests;
- focused frontend tests for rail/header trust rendering;
- governance validators if queue/docs change.

### Acceptance

- Supplier summary/quadrant/header recommendations can show backend-owned trust state or explicit unavailable semantics.
- A recommendation with partial or review-quality evidence no longer looks like a plain high-confidence action by omission.
- Frontend types stop hiding trust fields that already belong to the backend contract.

### Dependencies

- `RQ112` and `RQ120` DONE, or explicit owner promotion.

---

## RQ123 - Prove report-generation freshness/cache-version truth for pilot reports

Status: DONE
Completed after: `RQ112` is `DONE` or the owner explicitly reprioritizes report freshness truth
Priority: P1
Type: backend/tests/docs
Feature family: analytics-report-cache-generation-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ123-<agent>.lock.md`
Commit suggestion: `fix(analytics): prove report freshness truth`

### Problem

Pilot supplier/data-quality reports now have reconciled numbers and stable URLs, but the current evidence still leaves one trust gap: report generation itself does not prove a report cache-version bump or another freshness guarantee. A report can therefore be numerically correct for some earlier refresh yet still appear freshly generated without a fully tested cache/freshness contract.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` lists as the highest-risk finding that report generation does not rotate report cache version on its own and still depends on import/nightly/data-quality/admin invalidation.
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md` still names report cache version bump as part of the supplier-decision/report owner chain.
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` proves that explicit report-family invalidation bumps the report version, but not that report generation itself truthfully refreshes freshness semantics.
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md` proves stable report URLs and report-cache invalidation exist, but not the exact on-demand generation freshness contract.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: proved the supplier report freshness contract by asserting that report generation and last authoritative refresh are exposed as separate facts, that the report payload metadata carries both timestamps, and that the report cache version contract still cleanly separates cached generation from refresh truth.
- Changed files: `Api.Tests/AnalyticsReportsContractTests.cs`; `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ123-evidence.md`
- Contract/runtime behavior changed: no runtime formula changed; the supplier report contract now explicitly proves generated-vs-refreshed freshness semantics and the report UI test verifies both timestamps are presented separately
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsReportsContractTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (pass); `npm run test:run -- src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ123-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1e2f5539f6b7884bddb08e3b5272f47d39ac6f10`
- Main verification: `git branch --contains 1e2f5539f6b7884bddb08e3b5272f47d39ac6f10 -> * main`
- Missed: no new runtime report cache invalidation behavior was added; the contract was proven rather than altered
- Follow-up: `RQ124` is now READY
- Residual risk: the runtime still relies on existing cache-version rotation from administrative or refresh-family paths; this task only proved the contract truthfully

### Scope

- one pilot report family (`/analytics/supplier/report` or `/analytics/reports/pilot-intake`) and its cache-version/freshness path;
- the nearest cache/report contract tests plus one QA doc note;
- no broad cache-family redesign beyond the selected report truth contract.

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`;
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`;
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs`;
- `Api.Tests/AnalyticsReportsContractTests.cs`;
- the selected report endpoint/cache path.

### Do

1. Decide the truthful contract: either report generation rotates/invalidates the report family when needed, or generation remains read-only but must expose freshness as inherited from the last authoritative refresh.
2. Encode that contract in focused tests so “generated now” cannot be mistaken for “refreshed from source now”.
3. If generation is intentionally read-only, surface/document the exact freshness/version semantics rather than relying on inference.
4. Keep the fix scoped to one report family; split any second report lane into a follow-up.

### Tests

- `git diff --check`;
- focused report/cache contract tests;
- governance validators if queue/docs change.

### Acceptance

- The selected pilot report family has a citeable freshness/cache-version contract.
- Report generation no longer implies a stronger freshness guarantee than the system can prove.
- Operators can tell whether a report is newly rendered, newly refreshed, both, or neither.

### Dependencies

- `RQ112` DONE or explicit owner reprioritization.

---

## RQ124 - Expose backend-owned trust payload on dashboard legacy/advanced action cards

Status: DONE
Completed after: `RQ120` is `DONE` or the owner explicitly promotes the dashboard action-trust lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: analytics-dashboard-action-trust-payload
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ124-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface dashboard action trust payload`

### Problem

The dashboard still carries a thin legacy/advanced action contract. `DashboardActionDto` exposes only priority/title/recommendation, while the dashboard action UI and bridge layer already reason about confidence, reliability, recommendation gating, data-quality state, and status reason. When Product Decision rows are unavailable, `BuildDashboardDecisionActions(...)` maps legacy advanced actions into generic helper signals with `RecommendationAllowed=false`, `DataQualityStatus="insufficient_data"`, and null confidence/reliability values. That keeps the UI fail-closed, but it also hides whether the action is truly blocked, merely stale, or actually backed by a known validation condition.

### Evidence

- `Klijent/clientapp/src/types/analytics.ts` still carries `TODO(backend-dto): add confidence/reliability/dataQualityStatus/statusReason to dashboard actions`.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` defines `DashboardActionDto` with only `Priority`, `Title`, and `Recommendation`.
- The same file maps `advancedSnapshot.Actions` into `DashboardDecisionActionDto` by forcing generic fallback trust fields: `RecommendationAllowed = false`, `DataQualityStatus = "insufficient_data"`, `ConfidencePct = null`, `ReliabilityPct = null`, and `StatusReason = action.Recommendation`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` renders decision/action cards with explicit trust UI (`confidencePct`, `reliabilityPct`, `dataQualityStatus`, `statusReason`), so the current bridge can make every legacy advanced action look equally blocked even when the originating signal was more specific.

### Scope

- `DashboardActionDto` and the advanced snapshot action builder/bridge in `CachedAnalyticsEndpoints.cs`;
- the dashboard TypeScript contract and nearest dashboard action rendering/tests;
- no ranking-score rewrite, no Product Decision formula rewrite, and no Decision Board contract redesign outside the dashboard action payload.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`;
- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`.

### Do

1. Decide the smallest truthful additive payload for legacy/advanced dashboard actions: real recommendation gate/trust fields when the backend knows them, otherwise an explicit unavailable/legacy fallback state that is distinct from a proven blocked recommendation.
2. Propagate that payload through the backend DTO and frontend type without inventing frontend-only scoring or silently upgrading trust.
3. Replace the generic fallback mapping so helper actions preserve why they are limited: stale validation, missing evidence, workflow-only fallback, or another explicit backend-owned reason.
4. Add focused regression coverage for a healthy/actionable action, an explicitly limited helper signal, and a legacy fallback action with unavailable trust payload.

### Tests

- `git diff --check`;
- focused dashboard backend/contract tests for advanced action mapping;
- focused Vitest coverage for dashboard action trust rendering/fallback behavior;
- governance validators if queue/docs change.

### Acceptance

- Dashboard legacy/advanced action cards no longer collapse every thin payload into the same generic `insufficient_data` helper state.
- The UI can distinguish actionable trust, blocked trust, and unavailable/legacy fallback semantics without inventing local confidence.
- Backend-owned action trust metadata is visible or explicitly unavailable, not silently implied by generic copy.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

---

## RQ136 - Preserve truth in analytics action messages and notifications

Status: WAITING
Priority: P1
Type: backend/contract/frontend/tests
Feature family: analytics-action-notification-truth
Parallel-safe: no, shared action semantics require one owner
Owner: Codex
Commit suggestion: `fix(analytics): align action messages with trust metadata`

### Problem

Analytics messages, notifications and action labels must describe the backend decision state that the data supports. A user must not receive an actionable or success-looking message when the result is empty, stale, degraded, fallback, insufficient or failed.

### Evidence

- Core analytics invariants require a strict distinction between error, empty, warning/degraded and actionable success.
- Backend decision metadata is the source of truth; the frontend must not reconstruct confidence or recommendation status.
- Existing queue work closed several page-level empty/error cases, but cross-surface action and notification wording still needs an explicit parity proof.

### Scope

- the owning backend response/meta contract for action state, reason, confidence/reliability and data quality;
- the shared frontend mapping for action labels, toast/notification text and empty/error states;
- focused backend and frontend tests plus one evidence note.

Do not change recommendation formulas, introduce new notification channels, or add fake defaults for missing fields.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- the shared analytics response/meta contract
- current action/notification mapping and nearest focused tests

### Do

1. Inventory every user-visible message for the selected action surface and map it to a backend state.
2. Define explicit copy for actionable, insufficient, empty, stale/degraded, fallback and error states.
3. Ensure unknown or missing decision metadata blocks actionable copy instead of falling back to zero, success or generic confidence.
4. Keep Serbian text and established formatters intact; do not duplicate business scoring in React.
5. Add counterexample tests for stale, empty, failed and insufficient payloads.

### Tests

- focused backend contract tests for each state;
- focused Vitest tests for message/notification mapping;
- analytics guardrail check;
- typecheck and build when shared frontend code changes;
- `git diff --check` and queue validators.

### Acceptance

- Every user-visible action message has a proven backend state mapping.
- Error and unknown never render as valid zero or success.
- Empty remains distinct from error, and degraded/fallback remains visible.
- Tests cover both actionable and blocked/counterexample states.
- No recommendation formula or worker/infrastructure change is introduced.

### Dependencies

- `STAB16` must provide production liveness/freshness evidence before this is claimed as live pilot proof.
- `RQ128` remains the primary post-STAB actionability parity lane; reuse it rather than duplicating its live scope.
- This prompt is a later focused contract candidate, not current `READY`.

---

## RQ137 - Align requested, effective and observed period truth across analytics surfaces

Status: PARTIAL
Priority: P0
Type: backend/contract/frontend/tests
Feature family: analytics-period-lineage-parity
Parallel-safe: no, shared period semantics must stay under one owner
Owner: Codex
Commit suggestion: `fix(analytics): align period lineage across trust surfaces`

### Problem

Dashboard bootstrap, Pilot Readiness / Pilot Intake, and Supplier Decision report surfaces still expose period truth through different fields and fallback rules. A user can therefore see a requested range in one place, an observed data window in another, and a generated/report period elsewhere without one explicit canonical lineage.

### Evidence

- `.ai/runs/2026-09-03-analytics-followup-audit-evidence.md` recorded that supplier all-history reports needed the observed data period instead of synthetic default bounds and that cross-endpoint period alignment remains a separate contract follow-up.
- `.ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md` recorded that dashboard bootstrap, intake, and supplier report endpoints currently expose different periods/denominators, so the UI must not treat them as interchangeable.
- Core analytics invariants require requested/effective period truth, visible fallback state, and no fake “last refreshed” timestamp derived from query generation.

### Scope

- the smallest backend-owned period lineage contract across the selected analytics endpoints and DTOs;
- the frontend trust/report surfaces that render requested, effective, observed, generated and last-successful-refresh facts;
- focused backend/frontend regression tests and one evidence note.

Do not rewrite recommendation formulas, move worker ownership into the web process, or merge unrelated data-quality scoring changes.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `.ai/runs/2026-09-03-analytics-followup-audit-evidence.md`
- `.ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md`
- the current dashboard bootstrap, pilot intake/report, supplier report DTO/page contracts and nearest focused tests

### Do

1. Inventory the current requested/effective/observed/generated/refresh fields for the selected dashboard, readiness/report and supplier-report surfaces.
2. Define one backend-owned lineage vocabulary: requested period, effective calculation period, observed data period when they differ, generated-at, and last successful refresh.
3. Fail closed when the effective or observed range cannot be proven; show unknown/degraded/fallback explicitly instead of synthetic bounds or query-time refresh labels.
4. Keep cards, details, table/export/report and trust headers on the same period contract for each chosen surface.
5. Add counterexample tests for bounded vs all-history, wrong-scope/wrong-period fallback, missing refresh history, and generated-at vs refresh parity.

### Tests

- focused backend contract tests for the selected endpoints/DTOs;
- focused Vitest/report page tests for visible period lineage and fallback copy;
- analytics guardrail check;
- frontend build if shared DTO/page contracts change;
- `git diff --check` and queue validators.

### Acceptance

- Requested, effective and observed period facts are distinguishable and consistent across the changed analytics surfaces.
- No report or trust header presents query generation time as the last successful refresh.
- Unknown/fallback/partial period state remains visible and user-readable.
- Export/report/detail surfaces do not drift from the visible page period contract.

### Completion note

- Date: 2026-09-04
- Status: PARTIAL
- Completion: dashboard bootstrap, pilot readiness/intake, and supplier decision report surfaces now share explicit requested/effective/observed period lineage fields, keep generated-at separate from last successful refresh, and render the observed-period explanation only when the backend proves it differs from the effective calculation window
- Changed files: `Api/Dtos/AnalyticsResponseMetaDto.cs`; `Api/Dtos/AnalyticsReportResponseDto.cs`; `Api/Endpoints/CachedAnalyticsEndpoints.cs`; `Api/Endpoints/DataQualityEndpoints.cs`; `Api/Endpoints/SupplierDecisionHubEndpoints.cs`; `Api.Tests/AnalyticsSalesReadinessRegressionTests.cs`; `Api.Tests/AnalyticsReportsContractTests.cs`; `Klijent/clientapp/src/types/analytics.ts`; `Klijent/clientapp/src/utils/analyticsPeriodLineage.ts`; `Klijent/clientapp/src/utils/__tests__/analyticsPeriodLineage.spec.ts`; `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/PilotReadinessPage.tsx`; `Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts`; `Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`; `Klijent/clientapp/src/services/supplierDecisionReport.ts`; `Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-04-RQ137-evidence.md`
- Contract/runtime behavior changed: yes - the selected analytics trust/report surfaces now expose backend-owned period lineage and no longer substitute query-generation time for refresh truth
- Checks run: `git diff --check`; `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `npm run test -- --run src/utils/__tests__/analyticsPeriodLineage.spec.ts src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts`; `npm run check:analytics-guardrails`; `npm run build`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsSalesReadinessRegressionTests|FullyQualifiedName~AnalyticsReportsContractTests"`
- Checks not run: full solution `dotnet build`; full solution `dotnet test`; browser/live console smoke; production/live freshness verification from `STAB16`
- Run log: `.ai/runs/2026-09-04-RQ137-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 29a5943ad606c67721e931d73fd5906b49c9ade3
- Main verification: passed - origin/main contains 29a5943ad606c67721e931d73fd5906b49c9ade3
- Missed: delivery to `main` and live-runtime proof remain out of scope for this local queue execution
- Follow-up: `STAB16` for live freshness proof, then `RQ128` for broader post-stabilization actionability parity
- Residual risk: other analytics surfaces outside the selected dashboard/readiness/supplier-report path still rely on their own existing period contracts and were not revalidated here
- Next: none
- Prompt defect / scope repair: because the queue had no current `READY` prompt, `RQ137` was locally promoted as the smallest same-owner owner-bounded period-lineage repair candidate and is now returned to non-runnable `PARTIAL` truth after local proof

### Dependencies

- `STAB16` remains the live-runtime/deploy proof owner; do not duplicate production deploy verification here.
- This prompt remains `PARTIAL` and non-runnable; live freshness/deployment proof stays with `STAB16`.

---

## RQ138 - Add an authoritative Trend Models evaluation contract before numeric claims return

Status: OBSOLETE
Priority: P1
Type: backend/contract/frontend/tests
Feature family: trend-model-evaluation-contract
Parallel-safe: no, score semantics must remain backend-owned
Owner: Codex
Obsolete reason: User scope excludes standalone Trend Models and trend-evaluation functionality; historical fail-closed evidence remains in the completed prompt and QA audit.
Commit suggestion: `feat(analytics): add trend model evaluation contract`

### Problem

The dashboard Trend Models panel is now fail-closed, but there is still no authoritative endpoint or DTO that defines what a model score means, which period/sample it covers, or whether it is current enough to trust. Numeric model accuracy must not return until that contract exists.

### Evidence

- `.ai/runs/2026-09-03-trend-model-truthfulness-evidence.md` proved that the prior Trend Models values were hardcoded placeholders with no backend endpoint, period, sample, or evaluation result.
- `RQ108`, `RQ117`, and the forecast/backtest chain already established foundation work for forecast materialization and observed pairing, but they do not yet expose a user-facing model evaluation contract on the dashboard.
- Core analytics invariants require backend ownership for score/confidence/recommendation semantics and explicit freshness/limitations before display.

### Scope

- the smallest backend-owned registry/evaluation DTO and endpoint, if an authoritative evaluation source now exists;
- the dashboard Trend Models UI mapping and tooltip/copy for available vs unavailable evaluation;
- focused backend/frontend tests and one evidence note.

Do not invent scores from frontend heuristics, backfill fake history, or mix scenario-planning/runtime forecast work beyond the chosen evaluation contract.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `.ai/runs/2026-09-03-trend-model-truthfulness-evidence.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections for `RQ108` and `RQ117`
- the current Trend Models component and nearest focused tests

### Do

1. Confirm whether an authoritative backend evaluation source exists; if not, stop with a bounded docs/evidence update rather than recreating placeholder scores.
2. When a source exists, define explicit fields for metric name, score/unit, evaluated sample/window, freshness, last evaluated at, and limitation/warning state.
3. Keep unavailable evaluation fail-closed: no numeric accuracy, no percent delta, no fake confidence.
4. Map the dashboard panel to the backend contract and keep explanatory copy user-readable in Serbian.
5. Add tests for available evaluation, unavailable evaluation, stale evaluation and malformed/missing score payloads.

### Tests

- focused backend tests for the evaluation DTO/endpoint if one is added;
- focused Vitest for the Trend Models component mapping;
- analytics guardrail check;
- frontend build and changed backend project build if the contract changes;
- `git diff --check` and queue validators.

### Acceptance

- Trend Models show numeric evaluation only from an authoritative backend contract.
- Period/sample/freshness/limitations are visible whenever a numeric score is shown.
- Missing, stale or malformed evaluation remains explicitly unavailable instead of falling back to placeholder numbers.

### Completion note

- Date: 2026-09-04
- Status: PARTIAL
- Completion: Trend Models now consume the backend forecast backtest contract instead of hardcoded placeholders; the contract exposes freshness, last-evaluated, baseline label, and backend-owned metric metadata, while the dashboard keeps stale, unavailable, and malformed evaluation fail-closed and shows numerics only for `ready` + authoritative + non-stale payloads
- Changed files: `Application/Analytics/Queries/GetForecastBaselineBacktest/ForecastBaselineBacktestContract.cs`; `Application/Analytics/Queries/GetForecastBaselineBacktest/GetForecastBaselineBacktestQuery.cs`; `Application/Analytics/Queries/GetForecastBaselineBacktest/GetForecastBaselineBacktestHandler.cs`; `Api.Tests/ForecastBaselineBacktestContractTests.cs`; `Klijent/clientapp/src/types/analytics.ts`; `Klijent/clientapp/src/services/analyticsApi.ts`; `Klijent/clientapp/src/components/dashboard/TrendModelList.tsx`; `Klijent/clientapp/src/components/dashboard/TrendModelList.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-04-RQ138-evidence.md`
- Contract/runtime behavior changed: yes - Trend Models no longer present static descriptive placeholders as the only source of truth; they now render backend-owned evaluation state and fail closed when trust conditions are not met
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ForecastBaselineBacktestContractTests"`; `dotnet build .\Api\Api.csproj`; `npm run test -- --run src/components/dashboard/TrendModelList.spec.tsx`; `npm run check:analytics-guardrails`; `npm run build`; `git diff --check`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs`
- Checks not run: full solution `dotnet test`; browser/live console smoke; production/live model-evaluation proof
- Run log: `.ai/runs/2026-09-04-RQ138-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 29a5943ad606c67721e931d73fd5906b49c9ade3
- Main verification: passed - origin/main contains 29a5943ad606c67721e931d73fd5906b49c9ade3
- Missed: no authoritative measured `ready` payload is materialized from production data yet; the backend contract still truthfully defaults to unavailable until that runtime source exists
- Follow-up: a later evaluation-materialization prompt can reuse this contract instead of inventing new dashboard semantics
- Residual risk: the workspace contains unrelated in-flight analytics changes; this prompt proves only the trend-model evaluation contract path listed above
- Next: none
- Prompt defect / scope repair: because the queue had no current `READY` prompt, `RQ138` was locally promoted as the smallest same-owner trend-evaluation contract follow-up and is now returned to non-runnable `PARTIAL` truth after local proof

### Dependencies

- Reuse the forecast/backtest foundation from `RQ108` and `RQ117`; do not duplicate that lower-layer provenance work.
- This prompt remains `PARTIAL` and non-runnable until a real measured evaluation source is available.

---

## RQ139 - Prove analytics denominator, null and zero semantics across every decision surface

Status: PARTIAL
Priority: P0
Type: backend/contract/frontend/tests
Feature family: analytics-denominator-null-zero-contract
Parallel-safe: no, this is the shared numeric trust contract
Owner: Codex
Commit suggestion: `fix(analytics): fail closed on missing numeric evidence`

### Problem

Several analytics paths still use a numeric zero as a compatibility/default value when the underlying signal is missing. This can turn missing cost, missing velocity, missing margin, missing supplier/stock coverage or a missing denominator into a valid-looking KPI, score, forecast, ranking value or action. The contract must distinguish a real zero from unknown, not applicable, insufficient evidence and calculation failure.

### Evidence

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:155-191` derives approximate units/revenue and averages with `?? 0` and `Math.max(..., 1)`; `:217-260`, `:280-310`, `:325-350` and `:377-418` repeat this for price, aging, depletion and reorder outputs.
- `Application/Analytics/Services/TrendScoringService.cs:216-217` returns momentum `1.0` when either score is missing, and `:265-271` returns index `0.0` for an empty/positive-score-free input.
- `Application/Analytics/Services/TrendScoringService.cs:307-315` treats missing social input as zero and `:347-362` returns recommended order quantity zero for non-positive velocity without distinguishing true zero demand from unavailable/invalid velocity.
- `Api/Services/PreNivelacijaScoringService.cs:63-79`, `:198-205` and `:223-240` use zero/midpoint fallbacks for zero normalization spans and unknown confidence; `:208-214` clamps scenario units to at least one, so no evidence can create a positive scenario.
- `Api/Endpoints/AllEndpoints.cs:3338-3346`, `:3401-3409` and `:3510-3517` coalesce pre/post quantity, revenue, coverage and change percent to zero before the response is built.
- Existing RQ work fixed selected Daily Sales, supplier and shoe-type cases, but `RQ137` explicitly records that other analytics surfaces were not revalidated and `RQ138` records that measured evaluation data is still unavailable.

### Scope

- A canonical numeric-state contract for currency, quantity, ratios/percentages, rates, scores, confidence/reliability and dates.
- Backend DTO/meta fields that preserve `unknown`, `missing`, `insufficient`, `not_applicable`, `error` and `valid_zero` without overloading numeric zero.
- Frontend shared mapping/formatting for all analytics pages, cards, tables, charts, details, action lists, exports and reports.
- The affected sales, trend, forecast, inventory, supplier, data-quality and pre/post nivelacija calculations; do not silently limit the repair to one page.

Do not edit the raw vendor nivelacija SQL/reader branch owned by `Q83`; consume its additive contract after that SQL prompt lands. This keeps the independently runnable numeric-state work disjoint from the SQL owner path.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections `RQ02`, `RQ03`, `RQ04`, `RQ10`, `RQ137` and `RQ138`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Application/Analytics/Services/TrendScoringService.cs`
- `Api/Services/PreNivelacijaScoringService.cs`
- nearest backend/frontend tests for each changed calculation

### Do

1. Build a formula inventory with metric name, unit, numerator, denominator, source fields, valid-zero rule, missing rule, minimum evidence and owner. Include every `?? 0`, `?? 100`, `|| 0`, `Math.max(..., 1)`, epsilon and synthetic sentinel found on the mapped surfaces.
2. Replace only semantics-proven fallbacks. A missing denominator must produce null/unknown/insufficient metadata, never a share of zero or 100. A true measured zero must remain zero. NaN and Infinity must be rejected before serialization/rendering.
3. Remove frontend reconstruction of trusted revenue, margin, forecast, risk, score and recommendation values where a backend contract exists. If a legacy fallback must remain, label it degraded and keep it out of recommendation/actionable ranking.
4. Preserve empty-success separately from backend error; keep stale, fallback, partial and insufficient states visible and user-readable.
5. Keep backward compatibility only through additive nullable/meta fields or an explicitly versioned contract. Do not hide a changed business meaning behind the old numeric field.

### Tests

- Backend unit/contract tests for empty result, null input, a genuine valid zero, missing denominator, NaN, Infinity, negative/invalid input and zero normalization span.
- Frontend tests for the same cases through card, table, chart, detail, action, export and report adapters.
- Tests proving no unavailable value becomes a score, confidence, reliability, forecast, revenue share, reorder quantity or recommendation.
- Tests for stale and unknown freshness, partial/fallback response, wrong period and wrong scope where the numeric state is displayed.
- `npm run test -- --run <changed analytics specs>`; the nearest targeted `dotnet test`; analytics guardrail; `git diff --check` and queue validators.

### Acceptance

- Every audited metric has an explicit numerator/denominator and state contract.
- `null`, unknown, missing, insufficient, NaN and Infinity cannot render as a trusted zero or produce an allowed recommendation.
- A valid zero remains visible as zero and is not confused with no evidence.
- Backend values and states are identical across card, table, chart, details, export and report for the same query.
- Focused regression tests fail against the pre-fix behavior for all required counterexamples.

### Dependencies

- `RQ137` and `RQ138` remain partial/non-runnable; their existing fields may be reused, but completing them is not a prerequisite for this bounded numeric-state work.
- `Q83` is independently promoted for the raw vendor nivelacija SQL path; `RQ139` must not edit those SQL/reader files in parallel.
- `STAB16` remains the owner of production worker/live refresh access. This prompt may use deterministic fixtures and current runtime contracts but must not claim live proof without that evidence.
- Later prompts `RQ140`-`RQ146` must reuse this numeric-state vocabulary rather than creating local exceptions.

### Completion note

- Date: 2026-09-05
- Status: PARTIAL
- Completion: Delivered the bounded backend/frontend numeric-state hardening that was provable without taking ownership of Q83: missing/non-finite trend momentum and index components remain unknown, inventory fallback cannot create an order from dummy velocity, Data Quality revenue shares preserve missing denominators as null, and pre-nivelacija zero-stock scenarios remain zero instead of inventing one unit.
- Changed files: trend scoring queries/worker, Data Quality health/history/API/UI, pre-nivelacija scenario guard, focused regression tests.
- Contract/runtime behavior changed: null/unknown is preserved through the Data Quality health/trend response; empty or degraded trend/inventory results are not promoted to actionable numeric values; valid zero remains zero.
- Checks run: `dotnet build Trendplus2.sln --no-restore` (0 errors; existing analyzer warnings), focused backend tests (30/30), frontend guardrails (pass), focused frontend tests (12/12), `git diff --check` (pass).
- Checks not run: browser console smoke, live provider/refresh proof, complete route-by-route table/chart/export/report parity, and full analytics formula inventory.
- Run log: `.ai/runs/2026-09-05-RQ139-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `da18187c30e8f91b29b0f036138c63a027895888`
- Main verification: `git rev-parse origin/main -> da18187c30e8f91b29b0f036138c63a027895888`; implementation commit is contained in `origin/main`
- Missed: `analyticsIntelligenceDerived.ts` still contains legacy fallback arithmetic; pre/post raw vendor SQL/reader remains owned by `Q83`; full recommendationAllowed parity remains for follow-up prompts.
- Follow-up: keep `RQ140`-`RQ146` WAITING; create a bounded follow-up for derived-intelligence null states and complete pre/post/parity proof before promoting the next reliability prompt.
- Residual risk: live refresh proof, raw vendor nivelacija SQL ownership and full recommendationAllowed parity remain external or follow-up work.

---

## RQ140 - Prove pre/post nivelacija effects are comparable and not availability artifacts

Status: PARTIAL
Priority: P0
Type: backend/SQL/contract/frontend/tests
Feature family: pre-post-nivelacija-causal-comparability
Parallel-safe: no, pre/post semantics are shared by sales and decision screens
Owner: Codex
Promotion note: 2026-09-05 - owner-promoted after RQ139/Q83 semantic hardening; live database, refresh and deployed-runtime proof remain external and must not be inferred.
Commit suggestion: `fix(analytics): harden pre-post nivelacija comparability`

### Problem

Pre/post nivelacija screens expose revenue, units, margin and impact signals, but a delta after a price change is not evidence of a price effect when the article set, stock availability, observation window, cost coverage or event timing differs. Current SQL compatibility branches also coalesce missing coverage/change fields to zero and may fall back from revenue change to quantity change. This can present an unproven effect as a measured recommendation input.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:3227-3232` selects `change_percent_revenue` when available but falls back to `change_percent_qty`; `:3338-3346` and `:3401-3409` coalesce coverage and change fields to zero.
- `Api/Endpoints/AllEndpoints.cs:2240-2285`, `:2821-2861` build split, cost and margin snapshots for shoe type/color families, while `:2373-2422` and `:2949-2995` pass split coverage and impact into recommendation inputs. This is a high-risk boundary because coverage and recommendation are coupled.
- `Api/Endpoints/AllEndpoints.cs:2485-2489` and `:3050-3054` return null for prior-period changes when the denominator is not positive, but the same semantic distinction is not proven for every pre/post SQL response path.
- `Api/Services/PreNivelacijaScoringService.cs:106-152` uses smoothed scenario units and minimum-one-unit clamping without an observed comparable cohort or availability adjustment.

### Scope

- `/analytics` sales/trend surfaces, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/actions`, `/analytics/decision-board`, `/analytics/data-quality`, `/analytics/reports`, and all vendor/color/shoe-type/pre/post nivelacija screens that consume the split.
- Backend/SQL view contract for event date, pre/post windows, comparable article cohort, stock/availability, revenue, quantity, cost/margin coverage and control/test evidence.
- Frontend explanation, recommendation gate and export/report parity for the same split payload.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- the installed `analytics-nivelacija` skill instructions
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections for `RQ107`, `RQ112`, `RQ119`, `RQ137` and `RQ139`
- `Api/Endpoints/AllEndpoints.cs` vendor/color/shoe-type nivelacija handlers
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`
- `Api.Tests/AnalyticsResponseMetaContractTests.cs` and nearest nivelacija tests

### Do

1. Define pre-window, event boundary, post-window, timezone and effective/observed period semantics as half-open dates; prove the same article/cohort and scope are used in both periods.
2. Separate price effect from stock availability, OOS, assortment/composition, seasonality and traffic effects. If a control group or comparable cohort is unavailable, return an unproven/insufficient state and suppress recommendation.
3. Keep revenue deltas, quantity deltas, price metrics, margin/profit metrics and coverage as separate fields with units and denominators. Never substitute quantity percentage for revenue percentage without an explicit backend status and user-facing explanation.
4. Remove SQL/reader coalescing that hides missing coverage or change evidence. Preserve a true zero only when the source proves the measured value is zero.
5. Ensure recommendation status, score, confidence/reliability and `recommendationAllowed` are computed by the backend from the same validated split and reused unchanged by every frontend consumer.

### Tests

- Deterministic fixtures for: no event, event with no pre sales, event with no post sales, true zero delta, missing denominator, partial article cohort, stockout in post window, different scope, wrong period, duplicate event rows and control/test mismatch.
- SQL/view and endpoint tests proving revenue and quantity denominators are not interchangeable and missing coverage is not zero.
- Backend tests for margin with historical cost, estimated cost and no cost; recommendation suppression when comparability or coverage is insufficient.
- Frontend tests for visible explanation and no action when `recommendationAllowed=false`, including export/table/chart parity.
- `dotnet test` nearest nivelacija filters, focused frontend specs, analytics guardrail and `git diff --check`.

### Acceptance

- Every pre/post number states its window, cohort/scope, denominator, coverage and whether the effect is measured or only descriptive.
- Price, availability and composition effects are not silently conflated.
- Missing/partial/insufficient comparability never becomes zero effect, positive effect, confidence or an allowed action.
- The same backend split payload drives page, table, chart, detail, export and report without frontend recomputation.

### Dependencies

- `RQ139` numeric-state contract is the required semantic baseline.
- `Q83` is the separate SQL owner for raw nivelacija nullability/baseline behavior; reuse its result instead of duplicating SQL formula work here.
- Reuse existing `RQ107` scenario vocabulary and `RQ112` reconciliation work; do not create a second pre/post formula owner.
- Production event/refresh proof remains subject to `STAB16`; local deterministic evidence is not live deployment proof.

### Completion note

- Date: 2026-09-05
- Status: PARTIAL
- Completion: Backend pre/post nivelacija snapshots now expose a comparable-signal contract, preserve missing change evidence as null in the vendor article DTO, and gate recommendation score/confidence/reliability on valid revenue and quantity impact. Supplier, shoe-type and color aggregate endpoints reuse the same backend split result and fail closed when comparability is missing. Vendor pre/post, supplier footwear, color, shoe-type and supplier sales frontends preserve valid zero, reject non-finite values, suppress unproven pre/post numbers and hide action-like statuses/confidence when `recommendationAllowed=false`.
- Changed files: `Application/Analytics/AnalyticsNivelacijaSplitPolicy.cs`; `Api/Endpoints/AllEndpoints.cs`; `Api/Models/VendorSalesNivelacijaModels.cs`; `Api.Tests/AnalyticsNivelacijaSplitPolicyTests.cs`; `Api.Tests/SupplierDecisionSchemaSqlTests.cs`; affected pre/post frontend pages, API contracts and focused regression specs; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-09-05-RQ140-evidence.md`.
- Contract/runtime behavior changed: missing/partial/non-comparable pre/post evidence remains unavailable; a measured zero is preserved; recommendation status is neutralized in the UI when the backend denies actionability; backend-owned decision signals are not recreated by frontend arithmetic.
- Checks run: `git diff --check` (pass); focused frontend specs (26/26 pass); focused backend tests (31/31 pass); `npm run check:analytics-guardrails` (pass); `dotnet build` (0 errors, existing warnings); `npm run build` (pass); full backend test (`Api.Tests`, 1099 passed, 16 failed on existing SQL Server/Neon/config/runtime dependencies).
- Checks not run: live database query, production refresh event, deployed browser console smoke, actual missing-table/migration execution and complete route-by-route export/report parity; these require the external `STAB16`/runtime proof path and were not inferred from local tests.
- Run log: `.ai/runs/2026-09-05-RQ140-evidence.md`
- Evidence state: fallback live database/refresh/browser runtime unavailable; local contract evidence synchronized
- Delivery mode: direct-main
- Main commit SHA: `570a31e8471a0c98ea43cd3a2e8089fea4bba98c`
- Main verification: passed - `git rev-parse origin/main -> ea293c963539c84fca6b68e220a1e9b267b4b847`; implementation commit `570a31e8471a0c98ea43cd3a2e8089fea4bba98c` is contained in `origin/main`
- Missed: live STAB16 proof and full analytics route/export/report matrix remain outside this local bounded prompt execution.
- Follow-up: keep `RQ141`-`RQ146` WAITING; restore live runtime proof through `STAB16`, then promote `RQ141` for full lineage/scope/cache/refresh parity.
- Residual risk: production SQL/view availability, refresh freshness and browser runtime behavior are not proven by deterministic local tests; full backend suite still has 16 unrelated environment/config failures.
- Next: `STAB16` live proof, then `RQ141`.
- Prompt defect / scope repair: Q83 remains the raw vendor SQL/nullability owner; RQ140 consumed its contract and did not duplicate SQL formula work. Full parity and deployed-runtime proof were explicitly left external rather than falsely marked complete.

### Routing correction

- Date: 2026-09-05
- The prompt was already locally executed and has a synchronized `PARTIAL` completion note above. The stale per-prompt `READY` marker was corrected to `PARTIAL`; no later `WAITING` prompt was promoted because `RQ139` and `STAB16` gates are not complete.

---

## RQ141 - Map full analytics lineage, scope, cache and refresh parity

Status: WAITING
Priority: P0
Type: audit/backend/contract/frontend/tests
Feature family: analytics-lineage-scope-cache-refresh-parity
Parallel-safe: no, this is the cross-screen source-of-truth map
Owner: Codex
Commit suggestion: `fix(analytics): align full lineage and refresh provenance`

### Problem

The existing period-lineage repair covers selected dashboard/readiness/report paths only. The remaining analytics pages can still disagree about requested/effective/observed period, data scope, generated time, successful refresh, cache version or fallback source. A query timestamp must not be shown as data freshness, and a cache hit or fallback must not look like a fresh authoritative result.

### Evidence

- `RQ137` completion note explicitly records residual risk for analytics surfaces outside the selected dashboard/readiness/supplier-report path.
- `Api/Endpoints/AllEndpoints.cs:4058-4103` redirects legacy analytics routes to cached routes, creating a compatibility/cache boundary that needs route-by-route proof.
- `Api/Endpoints/AllEndpoints.cs:3187-3211` keys vendor nivelacija cache by request parameters and applies response metadata on cache hits; the full set of cache inputs, invalidation and last-successful-refresh behavior is not proven for all families.
- `Infrastructure/Seed/DatabaseInitializer.cs:458-478` explicitly keeps heavy analytics refresh out of startup and assigns it to `NightlyAnalyticsRefreshWorker`, while `:2102-2107` logs migration failure and continues. These paths require visible degraded/runtime truth rather than optimistic freshness.

### Scope

Produce and implement a matrix for every listed route and all sales, trend, forecast and pre/post nivelacija screens. Each row must map React page/component, API client, endpoint, DTO/response, backend service, SQL/EF query, table/view/migration, cache key/invalidation, refresh owner/source, existing tests and these facts:

- requested period;
- effective calculation period;
- observed data period;
- data scope;
- generated-at;
- last successful refresh;
- freshness status;
- data-quality status;
- empty/partial/error state;
- recommendation allowed;
- limitation/reason.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections `RQ111`, `RQ113`, `RQ120`, `RQ123`, `RQ135`, `RQ137` and `RQ139`
- `Api/Dtos/AnalyticsResponseMetaDto.cs`
- `Api/Services/AnalyticsRefreshStatusService.cs`
- `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- all route clients/pages named in Scope

### Do

1. Build the matrix before changing behavior and record every unresolved source-of-truth or schema gap.
2. Standardize backend lineage fields and ensure every cache hit, fallback, partial result and stale result carries its real source and status.
3. Keep generated-at separate from last successful refresh. If refresh history is missing, return unknown, not the current query time.
4. Validate all period and scope parameters at the endpoint boundary and include normalized values in cache identity. Wrong period/scope must not reuse a trusted-looking cache entry.
5. Ensure empty success, endpoint 404, missing table/migration, refresh failure and true server error have distinct API and user-facing states.
6. Reconcile one seeded data fixture across page/card/table/chart/detail/export/report and document any intentional aggregation conversion.

### Tests

- Matrix completeness check covering all required routes and all sales/trend/forecast/nivelacija families.
- Endpoint/client tests for wrong period, wrong scope, cache-key collision, stale and unknown freshness, fallback/partial response, failed refresh, endpoint 404 and missing relation/migration.
- Tests proving generated-at is not displayed as last successful refresh and empty success is not server error.
- Frontend route tests for dark/light/soft-gray theme and user-readable messages without raw backend codes.
- `npm run check:analytics-guardrails`, focused frontend/backend tests, `dotnet build`/test for changed backend contracts, `npm run build`, `git diff --check` and planning/queue validators.

### Acceptance

- A complete, current matrix exists for every requested screen and each field is either confirmed by code/test or explicitly marked unproven.
- Period, scope, provenance, freshness and quality cannot drift between cached and uncached responses.
- Last successful refresh is sourced from refresh history, never from request generation time.
- 404, missing schema, refresh failure, partial/fallback, empty and stale states are distinguishable and visible.

### Dependencies

- `RQ137`, `RQ139` and existing cache/refresh prompts are prerequisites for vocabulary and compatibility.
- `STAB16` owns provider/live worker access; this prompt must label live proof as pending when unavailable.

---

## RQ142 - Materialize measured forecast and trend evaluation with safe chart states

Status: OBSOLETE
Priority: P1
Type: backend/SQL/contract/frontend/tests
Feature family: forecast-trend-measured-evaluation
Parallel-safe: no, evaluation semantics must remain backend-owned
Owner: Codex
Obsolete reason: User scope excludes standalone forecast and Trend Models functionality; inventory analytics guardrails remain covered by inventory-surface prompts.
Commit suggestion: `feat(analytics): materialize measured forecast evaluation`

### Problem

`RQ138` added a fail-closed Trend Models contract, but its completion note states that no measured `ready` evaluation source is materialized from production data. Forecast/trend screens therefore still lack proven actual-vs-forecast pairing, horizon, cut-off, baseline and error metrics. A score, confidence or reliability claim without these facts is not evidence.

### Evidence

- `RQ138` completion note: the contract is present, but numeric values remain unavailable until a real measured evaluation source exists.
- `RQ108` and `RQ117` provide forecast materialization/observed-pairing foundations but do not by themselves prove the user-facing evaluation sample and metrics.
- `Application/Analytics/Services/TrendScoringService.cs:245-271` computes a normalized index from positive scores and returns zero for no usable scores without a sample/quality state.
- Frontend chart coverage includes `TrendModelList`, dashboard analytics charts, supplier/shoe-type charts and `SupplierSalesStatsPage`’s positive-size gate; all require explicit handling of initial width/height `0` or `-1`.

### Scope

- Authoritative forecast/trend evaluation materializer and DTO/endpoint.
- Actual/forecast pair identity, cutoff, horizon, baseline, sample, missing pairs, WAPE/MAE/bias or explicitly selected metrics, units and denominator rules.
- Trend Models/dashboard/sales/inventory forecast consumers plus chart/table/export/report parity and zero-dimension safety.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ108`, `RQ117`, `RQ138` and their evidence notes
- current forecast backtest query/handler/contract
- current `TrendModelList` and forecast/chart components

### Do

1. Prove the actual and forecast rows belong to the same entity, scope, cutoff and observed period; exclude leakage from future actuals.
2. Define each metric’s numerator/denominator, zero-demand behavior, missing-pair treatment, minimum sample and rounding. Do not emit measured accuracy for insufficient or stale samples.
3. Make baseline, horizon, last evaluated time, freshness, data quality and limitations mandatory when a numeric result is available.
4. Keep unavailable/stale/partial evaluation fail-closed; no frontend score or percent reconstruction.
5. Make every chart render a stable empty/blocked state while width or height is `0`, negative, NaN or not yet measured; never pass invalid dimensions to the chart library.

### Tests

- Pairing fixtures for perfect forecast, valid zero demand, no actual, no forecast, missing denominator, all-zero actuals, partial horizon, stale evaluation, leakage/wrong cutoff and wrong scope.
- Metric tests for WAPE/MAE/bias (or the chosen authoritative set), NaN/Infinity and minimum sample.
- Frontend tests for unavailable/stale/partial states, chart dimensions `0` and `-1`, table/chart/export parity and dark/light/soft-gray themes.
- Focused backend/frontend tests, analytics guardrail, changed project builds and `git diff --check`.

### Acceptance

- Numeric forecast/trend evaluation appears only from a measured backend source with explicit sample, period, baseline, horizon, freshness and limitations.
- Missing, zero-denominator, stale, partial or wrong-scope evaluation is visibly unavailable, not zero accuracy.
- Charts never receive invalid initial dimensions and do not generate console warnings/errors in the tested states.

### Dependencies

- `RQ138` contract, `RQ108` materializer foundation and `RQ117` observed-pair semantics are prerequisites.
- `RQ139` supplies the shared missing/zero/finite-number contract.

---

## RQ143 - Remove frontend decision and ranking invention from analytics surfaces

Status: WAITING
Priority: P0
Type: backend/contract/frontend/tests
Feature family: backend-decision-ranking-ownership
Parallel-safe: no, actionability has one source of truth
Owner: Codex
Commit suggestion: `fix(analytics): keep ranking and recommendation backend-owned`

### Problem

Backend ownership of recommendation status, score and confidence is not enough if pages still derive local thresholds, confidence tones, priority scores, urgency, reorder probability or ranking fallbacks. The same item can then be actionable in one surface and blocked in another, especially when impact/confidence is null or data quality is insufficient.

### Evidence

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:782-786`, `:821-825` use `?? 0` for expected impact and locally compute/sort priority and impact values.
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:778-784` derives data-quality status from measured sample size and warning-code counts in the page, which can diverge from backend status.
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:380-425` computes reorder need, urgency, reorder probability and expected profit on the frontend from fallback-filled inputs.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`, `ColorSalesStatsPage.tsx` and `DailySalesStatsPage.tsx` contain local quality/coverage thresholds that must be classified as presentation-only or moved to backend-owned status fields.
- `Api/Services/PreNivelacijaScoringService.cs:155-193` and `:234-280` show backend decision semantics already exist for one family, making frontend reimplementation especially risky.

### Scope

- Decision Board, Product Decision, supplier, inventory, actions, pre/nivelacija and all cards/tables/details/exports/reports with recommendation or ranking.
- Backend DTOs for status, decision score, expected impact, confidence/reliability, reason codes, data quality and `recommendationAllowed`.
- Frontend adapters and display-only sorting/filtering that must not change business decisions.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ01`, `RQ08`, `RQ10`, `RQ12`, `RQ13`, `RQ121`, `RQ122`, `RQ124`, `RQ129`, `RQ139`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- backend decision/recommendation DTOs and nearest tests

### Do

1. Inventory every frontend threshold, score, fallback, sort key and action visibility condition. Classify it as harmless presentation formatting or business logic.
2. Move business logic to the backend or consume an existing backend field. The frontend may sort by a backend-provided rank for presentation but may not invent rank from null-as-zero impact/confidence.
3. Enforce `recommendationAllowed=false` as a hard no-action rule across buttons, links, bulk actions, exports and reports.
4. Do not display confidence/reliability without valid backend basis; do not infer quality from sample length or warning count when backend already owns it.
5. Map reason/warning codes to safe Serbian user-facing copy, preserving raw codes only in an explicitly technical/audit channel.

### Tests

- Backend/frontend parity fixtures for allowed, blocked, insufficient, stale, partial, fallback, null impact, null confidence and true zero impact.
- Tests proving no action appears when `recommendationAllowed=false` and no ranking promotion occurs from missing values.
- Tests proving local threshold changes do not alter backend decision status.
- Table/card/detail/export/report parity and safe unknown-code mapping tests.
- Focused backend/frontend tests, analytics guardrail, frontend build and `git diff --check`.

### Acceptance

- Business decision, score, confidence/reliability, quality status, reason and actionability have one backend owner.
- Frontend never converts missing impact/confidence into zero for a trusted ranking or recommendation.
- Blocked recommendations expose explanation and limitation, but no executable action.
- All changed surfaces use the same backend payload and user-readable reason mapping.

### Dependencies

- `RQ139` is required for null/zero semantics.
- Reuse completed `RQ121`, `RQ122`, `RQ124` and `RQ129`; do not duplicate their contracts.

---

## RQ144 - Make Data Quality health distinguish no evidence from a valid zero

Status: DONE
Priority: P1
Type: backend/contract/frontend/tests
Feature family: data-quality-health-denominator-contract
Parallel-safe: no, health status gates trust everywhere
Owner: Codex
Promotion note: 2026-09-05 - owner-promoted as the smallest independent Data Quality continuation; RQ139 supplied the shared null/zero vocabulary and RQ118 is DONE. Forecast, vendor-comparison and live-worker work remain out of scope.
Commit suggestion: `fix(analytics): preserve data-quality denominator truth`

### Problem

Data Quality health uses revenue shares as decision signals. When the sales denominator is zero or unavailable, a share of zero is not evidence that quality is healthy. The page currently applies thresholds through `?? 0`, while the backend snapshot exposes non-null share fields that cannot tell no revenue from a measured zero share.

### Evidence

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs:145-171` sets `HasRevenueEvidence`, but `MissingCostRevenueSharePct` and `UnknownSupplierRevenueSharePct` become `0d` when `totalRevenue <= 0`.
- `Klijent/clientapp/src/pages/DataQualityPage.tsx:655-662` evaluates missing-cost and unknown-supplier health thresholds through `?? 0`, which can make unavailable health look green.
- `Klijent/clientapp/src/pages/DataQualityPage.tsx:401`, `:625` defaults issue totals to zero; this must remain distinct from a successful empty query versus unavailable issue data.
- Existing RQ04/RQ118/RQ135 work improved selected health/scope paths, but this specific backend nullable denominator contract is not proven across all consumers.

### Scope

- Data Quality health DTO/service/page, issue list and trend chart.
- Dashboard, Decision Board and supplier/product surfaces that consume health status.
- Period/scope, freshness, refresh and empty/error metadata for the health snapshot.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ04`, `RQ05`, `RQ07`, `RQ118`, `RQ135`, `RQ139` and their evidence
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Api.Tests/AnalyticsResponseMetaContractTests.cs` and Data Quality tests

### Do

1. Make share fields nullable or stateful and define: valid zero with positive denominator, unknown/no denominator, insufficient evidence, stale, partial and error.
2. Keep `HasRevenueEvidence` and denominator facts backend-owned; frontend must not infer health from null-coalesced values or local thresholds.
3. Distinguish successful empty issue list (`total=0`) from failed/unavailable issue query; show a user-readable explanation for both.
4. Carry exact health period, scope, generated-at, last successful refresh, freshness and data-quality status into every consumer.

### Tests

- Backend/frontend tests for empty sales window, null share, valid zero share with positive denominator, nonzero share, missing health payload, stale/unknown freshness and partial response.
- Issue-list tests for successful empty, filtered empty, endpoint error and missing relation.
- Dashboard/Decision Board tests proving no green/healthy recommendation from missing denominator.
- Focused tests, analytics guardrail, changed builds and `git diff --check`.

### Acceptance

- No-revenue/no-denominator health is not rendered as a measured zero or green state.
- A real zero share with a positive denominator remains zero and can be healthy.
- Health status and explanation are identical across Data Quality, dashboard, board, export and report consumers.

### Dependencies

- `RQ139` numeric-state vocabulary and `RQ118` scope contract are prerequisites.
- Reuse `RQ135` cache invalidation/freshness work.

### Completion note

- Completion: 100% of the bounded local denominator/error contract; no forecast, Shopify, vendor-comparison or live-worker scope was promoted.
- Changed files: `Api/Endpoints/DataQualityEndpoints.cs`; `Api.Tests/AnalyticsDataQualityConsistencyTests.cs`; `Klijent/clientapp/src/pages/DataQualityPage.tsx`; `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`; `Klijent/clientapp/src/types/analytics.ts`; queue and roadmap files.
- Checks run: backend focused tests 22/22; `DataQualityPage.spec.tsx` 9/9; isolated Dashboard, Data Quality empty and Inventory regressions passed; analytics guardrails and typecheck passed; `dotnet build .\Api\Api.csproj --no-restore --configuration Release` passed with 0 warnings and 0 errors; `git diff --check` passed.
- Checks not run: full backend suite, live database/refresh worker/browser console, and full cross-route export/report parity. The broader sales-readiness regression spec was attempted but the Pilot intake test emitted existing MSW unhandled-request warnings and hung, so it is not claimed as passed.
- Run log: `.ai/runs/2026-09-05-RQ144-evidence.md`.
- Delivery mode: direct-main delivery; implementation and evidence sync commits were pushed to `origin/main`.
- Main commit SHA: `f929e6fa92e570fea51ff4ffd6ab0ccf32372b87` (implementation and evidence delivery baseline).
- Main verification: `git merge-base --is-ancestor f929e6fa92e570fea51ff4ffd6ab0ccf32372b87 origin/main` passed; current main contains the delivered SHA.
- Missed: live refresh/runtime proof and complete cross-surface parity remain outside this bounded prompt and require `STAB16`/`RQ141`/`RQ145`.
- Residual risk: deployed frontend/backend compatibility and the HTTP 503 health-failure path require live runtime proof; forecast prompt `RQ150` remains WAITING by explicit user instruction.

---

## RQ145 - Prove analytics card/table/chart/detail/export/report parity and safe messaging

Status: WAITING
Priority: P1
Type: frontend/backend/contract/tests
Feature family: analytics-surface-parity-and-safe-messaging
Parallel-safe: no, parity requires one fixture and one semantic adapter
Owner: Codex
Commit suggestion: `test(analytics): prove cross-surface metric parity`

### Problem

Even when an endpoint is correct, analytics trust fails if cards, tables, charts, details, exports and reports use different values, fallback rules, rounding, period labels or warning text. Unknown backend codes can also leak into user-facing action/measurement messages.

### Evidence

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts` creates separate derived structures for category, price, aging, depletion and reorder views, so parity cannot be assumed from one API response.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` contains `OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code`, an explicit raw-code fallback risk.
- `RQ112`, `RQ120`, `RQ123`, `RQ136` and `RQ137` closed selected parity/provenance paths but do not establish one fixture-based parity proof for every required route/family.
- Chart components include both fixed heights and responsive containers; initial zero/negative measurement states need a common safe adapter rather than per-page behavior.

### Scope

- All routes listed by the user and all sales, trend, forecast and pre/post nivelacija surfaces.
- Shared formatters, metric adapters, warning/reason mappings, export/report builders and chart state wrappers.
- One deterministic fixture manifest with exact expected values/states for card/table/chart/detail/export/report.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ112`, `RQ120`, `RQ123`, `RQ136`, `RQ139`, `RQ141` and their evidence
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- existing analytics export/report/chart tests

### Do

1. Select representative fixtures for valid zero, null/unknown, stale, partial/fallback, empty, error and valid nonzero results.
2. Assert that all surfaces consume the same backend metric/state and only apply documented presentation formatting; no surface may recreate a decision or denominator.
3. Centralize safe Serbian labels for unknown warning/reason/status codes. Never fall back to the raw code in a user-facing label, tooltip, export or report.
4. Add a common chart guard for width/height `0`, `-1`, NaN and Infinity, preserving an accessible empty/preparing state and avoiding console warnings.
5. Verify dark, light and soft-gray themes using semantic tokens; do not fix parity by hardcoding a new unrelated theme.

### Tests

- Fixture-based exact parity tests for card/table/chart/detail/export/report values, units, rounding, period, scope, freshness, quality, fallback and recommendation status.
- Unknown-code mapping tests and no-raw-code assertions for page, export and report text.
- Chart tests for dimensions `0` and `-1`, empty data, NaN/Infinity point values and responsive initial render.
- Dark/light/soft-gray visual or DOM-state tests, focused frontend suite, analytics guardrail and `npm run build`.

### Acceptance

- One fixture produces semantically identical values and states everywhere it is shown.
- Exports/reports cannot silently restore a value hidden or blocked on the page.
- User-facing messages contain clear Serbian explanation, not internal backend codes.
- No chart warning/error is introduced by initial invalid dimensions or invalid metric values.

### Dependencies

- `RQ139`, `RQ141` and the completed parity/provenance prompts are prerequisites.
- This prompt consumes backend truth; it must not add frontend business formulas to repair a mismatch.

---

## RQ146 - Prove analytics endpoint, schema, migration and refresh-failure behavior

Status: WAITING
Priority: P1
Type: backend/integration/EF/SQL/tests
Feature family: analytics-schema-runtime-proof
Parallel-safe: no, runtime schema is an owner boundary
Owner: Codex
Commit suggestion: `test(analytics): prove schema and refresh failure states`

### Problem

Analytics code references EF entities, raw SQL relations, views and startup repair scripts across multiple databases. A missing table/view/migration, 404 route, failed refresh or partially applied schema can currently be reported as an empty or fallback result unless each path has a tested error contract. The user must never trust an empty dataset caused by a schema/runtime failure.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:3227-3232` probes relation columns and selects a compatibility expression; `:3338-3346`, `:3401-3409` then coalesce missing fields, making schema/column drift a numeric-trust boundary.
- `Infrastructure/Seed/DatabaseInitializer.cs:2102-2107` catches analytics migration failures and continues, while `:150-168` performs supplier/nivelacija schema repair. This requires explicit readiness/failure propagation to analytics responses.
- `Api/Endpoints/AllEndpoints.cs:4058-4103` maintains legacy redirect routes, so endpoint 404/redirect parity must be tested rather than inferred.
- Existing `AnalyticsDbInfrastructureTests` and response-meta tests cover selected contracts, not a complete endpoint-to-relation/migration proof for all requested analytics families.

### Scope

- Endpoint inventory and 404/redirect behavior for all required analytics routes.
- EF/SQL query, relation/view/table, migration and startup repair mapping for sales, trend, forecast, inventory, supplier, Data Quality and nivelacija.
- Refresh worker/recorder, cache invalidation and API meta behavior for successful, failed, partial and skipped refreshes.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ111`, `RQ113`, `RQ135`, `RQ141` and their evidence
- `Infrastructure/Seed/DatabaseInitializer.cs`
- `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs`
- `Api/Services/AnalyticsRefreshStatusService.cs`
- analytics migrations/views and nearest infrastructure tests

### Do

1. Generate an endpoint-to-service-to-query-to-relation/migration inventory and mark every edge confirmed or unproven.
2. Add deterministic integration/contract tests for missing table/view, missing column, unapplied migration, endpoint 404, failed refresh, skipped worker and stale cache.
3. Ensure missing schema and failed refresh return an explicit degraded/error/readiness state with safe user copy; never return successful empty data without an empty reason.
4. Verify migration listing and current model/view compatibility for the analytics context. Do not perform destructive production schema repair in this prompt.
5. Prove cache invalidation/versioning after successful and failed refresh; a failed refresh must not advance the last-successful-refresh timestamp.

### Tests

- Endpoint route/redirect tests including 404 and wrong method/path.
- EF/SQL integration tests for missing table/view/column and migration mismatch, with safe classification and no fake zero rows.
- Refresh recorder/status tests for success, failure, retry, skipped/unregistered worker and partial family refresh.
- Cache tests proving failed refresh does not publish fresh metadata and successful refresh invalidates all dependent families.
- `dotnet ef migrations list` for the affected context, focused `dotnet test`, backend build, analytics guardrail if contract changes, `git diff --check` and queue validators.

### Acceptance

- Every requested analytics endpoint has a confirmed route, query and schema/migration owner or an explicit blocker.
- Missing schema, 404 and refresh failure are visible as failures/degraded states, never as trusted empty/zero analytics.
- Last successful refresh changes only after a confirmed successful refresh and cache metadata agrees with it.
- The proof is reproducible on current main without destructive database operations.

### Dependencies

- `RQ141` lineage matrix and `RQ139` numeric-state contract are prerequisites.
- `STAB16` remains the owner of provider/live worker registration and production refresh proof; local integration tests cannot replace that evidence.

---

## RQ147 - Make KPI evidence, decision use and limitations backend-owned

Status: WAITING
Priority: P0
Type: backend/contract/frontend/export/report/tests
Feature family: analytics-metric-evidence-registry
Parallel-safe: no, metric semantics are a shared analytics contract
Owner: Codex
Commit suggestion: `feat(analytics): expose metric evidence and decision tiers`

### Problem

`analyticsMetricDefinitions.ts` gives useful frontend methodology text, but it is not the authoritative proof of a value returned by an API. A card can name a formula without declaring whether its value is directly observed, derived, modelled or causally measured; which source generation and denominator produced it; or whether it is valid for an action. This permits a modeled estimate, heuristic confidence or incomplete coverage KPI to read like an observed financial fact.

### Evidence

- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts` centralizes labels/formulas and blocked states, but it is a client asset rather than an API-owned metric contract.
- `docs/qa/KPI_METHODOLOGY_CONSISTENCY_REVIEW.md` confirms formulas for selected KPIs but leaves GMROI as a future contract and does not assign evidence or decision tiers.
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` records historical runtime evidence where freshness was unknown and PDC actionability contradicted insufficient evidence; a methodology label alone is not a live proof.
- Tableau propagates data-quality warnings from upstream assets to downstream dashboards, while Shopify connects metric cards to detailed reports. Trendplus needs the equivalent metric-level provenance to stay consistent across its own card, detail, export and report surfaces.

### Scope

- A small backend-owned, versioned metric-evidence contract reused by analytics DTOs and report/export projections.
- The priority KPI families: revenue, units, gross margin/margin contribution, data quality/readiness, sell-through, stock cover, inventory turnover, stock-at-risk, lost sales, forecast/trend evaluation, pre/post effect, confidence/reliability and supplier decision metrics.
- Presentation-only mapping in shared frontend methodology/trust components; no duplicate frontend classification or scoring.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ141`, `RQ143`, `RQ145` and their evidence
- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`
- `Klijent/clientapp/src/components/analytics/MetricMethodologyPanel.tsx`
- `Api/Dtos/AnalyticsResponseMetaDto.cs` and nearest analytics DTOs
- `docs/qa/KPI_METHODOLOGY_CONSISTENCY_REVIEW.md`

### Do

1. Define a backward-compatible contract for `metricKey`, unit, aggregation, requested/effective/observed period, scope, source generation, numerator/denominator availability, freshness, data quality, coverage and limitation reason.
2. Classify every surfaced KPI as exactly one of `observed`, `derived`, `modelled`, `causal` or `unavailable`; define a separate backend-owned decision-use tier. Do not call a modeled estimate, score or confidence causal.
3. Require action-bearing/display-confidence eligibility to be explicit. Missing coverage, stale/unknown freshness, fallback, partial result or unproven denominator must downgrade or suppress the claim rather than inventing `0`, `100%` or a positive confidence.
4. Reuse the same serialized evidence object for card, table, chart tooltip, detail, export and report. The frontend may translate it but must not reclassify it.
5. Keep scope bounded: do not rewrite raw KPI formulas, introduce a generic metadata platform, or add a second recommendation engine.

### Tests

- Contract tests for every priority metric covering observed valid zero, missing/null input, missing denominator, stale/unknown freshness, fallback/partial source, NaN/Infinity and unavailable source.
- Tests that a modeled or unavailable metric cannot acquire `causal` classification, numeric confidence or recommendation action through export/report projection.
- Fixture parity tests proving card/table/chart/detail/export/report carry the same key, unit, evidence class, period/scope and limitation.
- Frontend tests for clear Serbian copy and dark/light/soft-gray states without raw internal codes.
- Focused backend/frontend tests, `npm run check:analytics-guardrails`, changed builds, `git diff --check` and queue/planning validators.

### Acceptance

- Every priority KPI has one backend-owned evidence class, decision-use tier and limitation reason that survive every consumer.
- A metric without sufficient proof is visibly a signal or unavailable, never an observed fact or actionable recommendation.
- Frontend methodology text remains a translation of backend truth instead of an independent source of KPI semantics.

### Dependencies

- `RQ141` provides lineage/freshness fields, `RQ143` owns backend decision status and `RQ145` proves consumer parity.
- `RQ139` supplies null/zero/non-finite semantics. This prompt must wait until its declared dependencies are complete.

---

## RQ148 - Prove the gross/net/return/cost basis of sales and margin KPIs

Status: WAITING
Priority: P0
Type: audit/backend/EF-SQL/contract/export/report/tests
Feature family: sales-margin-returns-measurement-basis
Parallel-safe: no, finance-facing metric meaning has one canonical owner
Owner: Codex
Commit suggestion: `fix(analytics): make sales and margin basis explicit`

### Problem

Revenue is currently described as a sales-line sum and margin contribution as revenue less available cost. That is useful descriptive context, but it does not prove whether each route includes returns, cancellations, tax, discounts, shipping, markdowns, cost fallback or incomplete cost coverage. A number called "prihod" or "marža" can therefore be internally consistent yet commercially misleading, especially when used in supplier or markdown recommendations.

### Evidence

- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts` documents revenue as `SUM(prodajna_vrednost_stavke)` and margin contribution as available-cost margin; neither definition is a route-specific net/gross/returns proof.
- `docs/analytics/PILOT_ONBOARDING_IMPORT_MAP.md` identifies return quantity as optional input, so absence of return history cannot be silently interpreted as zero returns.
- `docs/analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md` lists return/refund impact as a future metric with risks including unlinked refunds and duplicate records.
- `RQ146` already owns schema and migration failure semantics; this task consumes that work to distinguish missing return/cost evidence from a valid zero.

### Scope

- Revenue, net sales, discount/markdown contribution, gross margin percentage, margin contribution, return/refund impact and supplier margin/revenue slices used by `/analytics`, products, supplier, reports and nivelacija surfaces.
- Endpoint/service/query/view/EF inventory for each metric basis, plus consistent DTO metadata and export/report projection.
- No accounting ledger, tax engine, invoice mutation or source-system write-back.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ141`, `RQ146`, `RQ147` and `RQ145`
- `docs/analytics/PILOT_ONBOARDING_IMPORT_MAP.md`
- `docs/analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md`
- current sales, supplier, report and nivelacija queries/DTOs and their nearest tests

### Do

1. Produce a route-by-route measurement-basis matrix: gross versus net, sale/return/cancellation inclusion, discount/markdown treatment, tax/shipping treatment, cost source/coverage and period/scope.
2. Add additive backend metadata naming the basis and coverage. Return `unknown`, `partial` or `unavailable` when the source cannot prove an element; do not substitute zero returns or complete cost coverage.
3. Ensure KPI labels distinguish gross sales, net sales, margin contribution and accounting profit. Do not call partial-cost margin "net profit".
4. Gate financial recommendations and confidence on the declared cost/return coverage policy; preserve descriptive values where useful but label their limitation.
5. Reconcile one seeded fixture across card/table/chart/detail/export/report; explicitly document any legal/accounting distinction rather than hiding it in a formatter.

### Tests

- Valid no-return zero; missing return relation/history; return linked to original sale; duplicate/unlinked return; cancellation; discount/markdown; tax/shipping present versus unavailable; complete/partial/missing/fallback cost coverage.
- Wrong period/scope and cache-key tests for financial slices; missing table/migration and refresh failure must be degraded/error, not empty successful revenue.
- Export/report parity for basis, amount, unit, coverage and limitation; no raw backend codes in UI.
- Focused integration/contract/frontend tests, migration inspection, changed builds, guardrail and queue/planning validators.

### Acceptance

- Every displayed finance-facing metric says what it includes and what it cannot prove.
- A missing return, cancellation or cost source cannot increase trust by becoming zero or complete coverage.
- Financial recommendation surfaces preserve the same measurement basis and limitation in UI, export and report.

### Dependencies

- `RQ141`, `RQ145`, `RQ146` and `RQ147` are prerequisites.
- Source-system accounting policy remains external: where it is not supplied, the result must remain descriptive/limited rather than guessed.

---

## RQ149 - Establish inventory economic evidence before GMROI or demand-value claims

Status: WAITING
Priority: P1
Type: backend/EF-SQL/contract/frontend/export/report/tests
Feature family: inventory-economic-metric-evidence
Parallel-safe: no, inventory value and demand evidence are shared decision inputs
Owner: Codex
Commit suggestion: `feat(analytics): expose inventory economic evidence`

### Problem

Trendplus correctly blocks selected sell-through and stock-cover denominators, but the commercial interpretation remains weaker than the formula: daily stock history is limited, current on-hand can censor observed sales, and average inventory cost value needed for GMROI is not a stable runtime contract. Without an explicit observed-versus-reconstructed inventory basis, low sales can be mistaken for low demand and modeled lost sales or turnover can be mistaken for accounting-grade value.

### Evidence

- `docs/qa/KPI_METHODOLOGY_CONSISTENCY_REVIEW.md` marks GMROI as a future contract and warns that cost-based and unit-based turnover are not interchangeable.
- `docs/analytics/STOCK_COVER_SELL_THROUGH_AUDIT.md` confirms denominator guards for stock cover and sell-through, but not a full historical stock/economic valuation basis.
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` identifies persisted historical inventory as a major foundation gap for average inventory, GMROI, OOS duration, lost sales and forecast validation.
- Retail inventory products expose reorder parameters such as trailing sales period, forecast period and supplier lead time; Trendplus must expose whether its own inventory economics has the required observed inputs rather than only displaying a recommendation.

### Scope

- Sell-through, stock cover, inventory turnover, stock-at-risk/slow-stock capital, OOS/lost-sales evidence and future GMROI eligibility across inventory, Product Decision Center, Decision Board, supplier and reports.
- Backend measurement-basis/coverage state and frontend presentation reuse; no new optimizer, reorder algorithm or retroactive stock reconstruction.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ96`, `RQ117`, `RQ119`, `RQ141`, `RQ144`, `RQ147`
- `docs/analytics/STOCK_COVER_SELL_THROUGH_AUDIT.md`
- `docs/analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md`
- inventory snapshot/foundation queries, DTOs, cache policies and nearest tests

### Do

1. Define one backend evidence basis for each inventory KPI: observed daily snapshot, current-state proxy, reconstructed history, modelled demand or unavailable. Preserve stock snapshot time, SKU/store scope, cost coverage and inbound/transfer/reservation treatment.
2. Distinguish observed sales from demand censored by stockout. Lost sales, OOS duration and demand-adjusted turnover must remain modeled/unavailable unless matching observed inventory and demand assumptions exist.
3. Define GMROI eligibility strictly as gross-margin value divided by observed average inventory at cost over an explicit window. Do not expose GMROI-lite as GMROI; return unavailable with a clear reason until requirements are met.
4. Keep true zero stock, true zero sales and true zero inbound distinct from missing/reconstructed source facts and denominator failure.
5. Reuse basis/coverage/limitation unchanged across the inventory list, details, PDC, Decision Board, supplier rollups, exports and reports.

### Tests

- Observed zero stock/sales/inbound; missing opening snapshot; stale snapshot; wrong SKU/store/day; transfer/reservation; partial cost coverage; missing velocity denominator; stockout-censored sales; reconstructed-only history; modelled lost sales; GMROI eligible and ineligible fixtures.
- Parity tests for unit/currency basis, source classification, status/recommendationAllowed and limitation across all consumers.
- Cache/refresh tests proving a failed inventory snapshot refresh cannot publish a newly trusted valuation or last-success time.
- Focused backend/frontend tests, analytics guardrail, affected builds, migration inspection where touched and queue/planning validators.

### Acceptance

- Inventory value and demand claims visibly identify whether they are observed, reconstructed, modelled or unavailable.
- GMROI is absent until its exact observed cost-basis contract is satisfied.
- No inventory recommendation can present censored sales or fallback valuation as a proven economic outcome.

### Dependencies

- `RQ96`/`RQ117` foundations, `RQ119` scope contract, `RQ141` lineage, `RQ144` denominator status and `RQ147` evidence registry are prerequisites.
- `STAB16` retains live refresh and source reconciliation proof ownership.

---

## RQ150 - Calibrate forecast usefulness by cohort and decision cost

Status: OBSOLETE
Priority: P1
Type: backend/contract/frontend/export/report/tests
Feature family: forecast-decision-calibration
Parallel-safe: no, evaluation and forecast confidence must have one backend owner
Owner: Codex
Obsolete reason: User scope excludes forecast calibration and confidence work for now.
Commit suggestion: `feat(analytics): calibrate forecast decision value`

### Problem

`RQ142` will materialize the first measured forecast/trend evaluation, but a headline WAPE/MAE/bias alone does not establish that a forecast is useful for replenishment. Accuracy varies by SKU/store/lifecycle, intermittent demand and horizon; point accuracy also does not validate an uncertainty range or the asymmetric cost of understocking versus overstocking. A numeric forecast confidence must therefore remain unavailable until it is calibrated against measured cohorts and an explicit decision-loss policy.

### Evidence

- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md` intentionally keeps WAPE/bias/MAE unavailable until a paired authoritative window exists and requires cohorts such as sufficient history, sparse, new item and no history.
- `RQ142` owns measured pairing, baseline, horizon and safe chart states, but does not define forecast interval calibration or business cost of a miss.
- Amazon Forecast evaluates multiple backtest windows and supports WAPE/MASE/RMSE plus quantile loss; it explicitly treats WAPE as undefined when the observed denominator is near zero and uses quantiles for asymmetric under- versus over-prediction costs.

### Scope

- A post-`RQ142` backend calibration layer over the measured forecast evaluation contract.
- Cohort-specific forecast performance, baseline comparison, interval coverage/quantile loss when intervals exist, decision-loss policy and strict recommendation/confidence eligibility.
- Forecast/trend/inventory consumers and their export/report presentation; no ML replacement, optimizer, live score mutation or source-data write-back.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ108`, `RQ117`, `RQ138`, `RQ142`, `RQ147`, `RQ149`
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- current forecast materializer/backtest DTOs, report/export projections and nearest tests

### Do

1. Define fixed evaluation cohorts at least by SKU/store scope, horizon, sufficient-history/sparse/new-item/no-history, availability state and lifecycle; headline aggregates must name their cohort composition and exclusions.
2. Compare the candidate forecast to the declared naive baseline on the same cutoff/window/cohort. Do not promote model confidence if it does not beat or cannot be compared to the baseline.
3. Preserve WAPE/MAE/bias denominator truth; add interval coverage/quantile-loss only when the backend emits a real interval. An all-zero or missing observed denominator is unavailable, not zero error.
4. Define a bounded, visible loss policy for under- versus over-forecasting. It may classify a forecast as decision-ineligible; it must not invent purchasing quantities or money outcomes.
5. Expose calibration period, sample, freshness, drift/change status, baseline result and limitation in the same contract used by chart, table, detail, export and report. Frontend cannot recompute confidence or calibration.

### Tests

- Perfect forecast, valid zero demand, all-zero observed denominator, missing actual, missing forecast, wrong cutoff/scope, stale/partial horizon, sparse/new/no-history, stockout-censored demand, baseline worse/equal/better and NaN/Infinity.
- Calibrated versus uncalibrated interval fixtures; no interval must not become a fake coverage percentage.
- Asymmetric-loss fixtures proving a point forecast cannot become an action without an explicit backend policy and eligibility.
- Table/chart/detail/export/report parity, invalid chart dimensions `0`/`-1`, clear Serbian states and no browser console warning/error in focused rendering tests.
- Focused backend/frontend tests, analytics guardrail, changed builds and queue/planning validators.

### Acceptance

- Forecast confidence or recommendation appears only with measured, cohort-specific, fresh and baseline-comparable evidence.
- A forecast can be numerically accurate yet decision-ineligible when availability, sample, uncertainty or loss-policy evidence is missing; that distinction is visible everywhere.
- Zero/missing denominators, stale data and partial horizons never become zero error or calibrated certainty.

### Dependencies

- `RQ142` measured evaluation and `RQ147` evidence registry must be DONE; `RQ149` provides inventory availability/economic basis where replenishment interpretation is requested.
- This prompt does not replace `RQ142` and must not be promoted early.

---

## RQ151 - Remove raw unknown action warning codes from user-facing messages

Status: DONE
Priority: P1
Type: frontend/tests
Feature family: analytics-action-safe-messaging
Parallel-safe: no, action message mapping has one page owner
Owner: Codex
Promotion note: 2026-09-05 - extracted from the independently executable safe-messaging slice of `RQ136`/`RQ145`; no forecast, Shopify, vendor-comparison, live-worker or recommendation-formula scope is included.
Commit suggestion: `fix(analytics): map unknown action warnings safely`

### Problem

`AnalyticsActionsPage` maps known outcome warning codes to Serbian copy but falls back to rendering the raw backend code when the code is unknown. That violates the user-facing text safety rule and makes backend vocabulary changes visible as opaque technical strings. This can be fixed locally without changing action semantics or waiting for live runtime proof.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx:258` returns `OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code`.
- The same page already has an explicit safe-label pattern for known action, quality and recommendation codes.
- `RQ136` and `RQ145` leave cross-surface message parity as broader follow-up, but this unknown-code leak is a bounded page-owned defect.

### Scope

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` warning/reason/status presentation helpers and the nearest page tests.
- Only user-facing mapping and regression coverage; preserve backend values in technical/audit payloads where an existing channel requires them.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ136` and `RQ145` safe-messaging sections
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`

### Do

1. Replace raw-code fallback in every user-visible outcome warning/reason/status path owned by this page with clear Serbian copy that states the data or measurement limitation.
2. Preserve known mappings and backend-owned status/action semantics; do not invent a new recommendation or confidence rule.
3. Keep raw codes only in an explicitly technical/audit path if one already exists; never expose them in cards, tooltips, toasts, exports or reports owned by this page.
4. Add a regression test for an unknown warning code, plus known-code, empty, stale/degraded and failed-summary states.

### Tests

- Focused `AnalyticsActionsPage` specs for unknown and known warning codes, empty measured outcomes, stale/degraded summary and summary failure.
- Verify no raw backend code appears in rendered user-facing text.
- `npm run check:analytics-guardrails`, typecheck/build when shared mapping changes, `git diff --check` and queue validators.

### Acceptance

- Unknown backend warning/reason/status codes never appear verbatim in user-facing analytics action text.
- Known Serbian labels remain unchanged.
- Empty, stale, degraded and failed states remain distinct and do not become success, zero or actionable copy.
- No backend score, confidence, recommendation status or formula is recreated in React.

### Dependencies

- No runtime dependency; this is a bounded presentation-mapping repair and must not wait for `STAB16`, forecast materialization or Shopify work.
- Reuse the existing backend state and shared formatters; do not make `RQ136` a second owner.

### Completion note

- Date: 2026-09-05
- Status: DONE
- Completion: 100% of the bounded unknown outcome-warning mapping repair.
- Changed files: `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-09-05-RQ151-evidence.md`.
- Contract/runtime behavior changed: unknown outcome warning codes now render a clear Serbian limitation message and never appear verbatim in user-facing summary text; known mappings and backend action semantics are unchanged.
- Checks run: failing-first unknown-code test reproduced the defect; focused RQ151 regression passed; full `AnalyticsActionsPage.spec.tsx` passed 20/20; `npm run check:analytics-guardrails` passed; `npm run build` passed; `git diff --check` passed; queue/planning validators passed before delivery.
- Checks not run: backend tests/build, live database/refresh worker, browser console smoke, and full cross-route export/report parity; no backend/runtime contract changed in this prompt.
- Run log: `.ai/runs/2026-09-05-RQ151-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main; implementation commit `7c6c46b75f118aecbb7643e7df11fc145f8e82f7` was pushed to `origin/main`.
- Main commit SHA: `7c6c46b75f118aecbb7643e7df11fc145f8e82f7`.
- Main verification: `git merge-base --is-ancestor 7c6c46b75f118aecbb7643e7df11fc145f8e82f7 origin/main` passed; current main contains the delivered SHA.
- Missed: broader unknown reason/status mappings outside this page remain in `RQ136`/`RQ145`; forecast/Shopify/live-worker work remains excluded.
- Follow-up: `RQ152` is promoted as the next bounded numeric-state prompt; `RQ153` remains WAITING.
- Residual risk: unknown codes in other analytics pages can still require the broader safe-messaging parity prompt.
- Next: execute `RQ152`; keep `STAB16` before live proof.

---

## RQ152 - Preserve unknown and missing evidence in derived intelligence builders

Status: DONE
Priority: P1
Type: frontend/contract/tests
Feature family: analytics-derived-numeric-state
Parallel-safe: no, derived signal semantics have one owner
Owner: Codex
Commit suggestion: `fix(analytics): preserve derived intelligence evidence states`

### Problem

`analyticsIntelligenceDerived.ts` still contains legacy derived builders that convert non-finite values to zero, use synthetic denominator guards, and calculate approximate revenue, stock value, depletion risk or reorder-related outputs from fallback-filled signals. Even where the current merge path keeps a legacy backend result primary, these builders remain a future regression path and their numeric contracts are not proven.

### Evidence

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:23-25` returns `0` from `round` for non-finite input.
- `:184-196`, `:231-258`, `:321-328` and `:356-370` use synthetic denominator guards or fallback numeric values while constructing derived outputs.
- `RQ139` explicitly records this file as a remaining residual, while `RQ143` requires frontend-derived ranking/reorder semantics to remain non-actionable without backend evidence.

### Scope

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts` and its focused service tests only.
- Define state-preserving return behavior for unknown, missing, insufficient and valid-zero inputs; keep these builders non-authoritative and non-actionable.
- No forecast calibration, Shopify integration, backend formula rewrite, recommendation engine, or live refresh work.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ139` completion note and `RQ143` residual evidence
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`
- the DTO contracts imported by this service

### Do

1. Inventory each derived output's unit, numerator, denominator, source fields, valid-zero rule and minimum evidence before changing types.
2. Reject NaN/Infinity and missing denominators as unknown/unavailable or omit the derived item; never convert them to a trusted zero, positive scenario or healthy status.
3. Preserve measured zero stock, zero sales, zero revenue and zero discount where their denominators and source evidence are valid.
4. Ensure derived values cannot be used to create recommendation/action/ranking semantics; existing backend-owned results remain authoritative.
5. Add failing-first tests for empty, null, valid zero, missing denominator, NaN, Infinity and mixed valid/invalid signal sets.

### Tests

- Focused `analyticsIntelligenceDerived` tests for all listed numeric counterexamples and mixed valid/unknown rows.
- Prove no derived fallback creates a positive reorder/depletion/action value from missing evidence.
- `npm run check:analytics-guardrails`, typecheck/build, `git diff --check` and queue validators.

### Acceptance

- Unknown/missing/non-finite derived evidence remains unavailable or explicitly degraded, never a valid zero.
- Valid zero remains zero.
- Derived builders do not create actionable recommendation, confidence or ranking semantics.
- Existing primary backend data paths remain unchanged.

### Dependencies

- RQ139's bounded numeric vocabulary is accepted as the semantic baseline; unresolved RQ139 live/parity residuals are not pulled into this prompt.
- Queue order: promote after `RQ151` is complete; do not promote forecast/Shopify or live-worker work under this prompt.

### Completion note

- Date: 2026-09-05
- Status: DONE
- Completion: Derived intelligence builders now preserve missing/unknown numeric evidence instead of converting it to trusted zero, reject non-finite and invalid inputs, preserve valid zero values, and keep derived reorder output non-actionable.
- Changed files: `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`; `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-09-05-RQ152-evidence.md`.
- Contract/runtime behavior changed: category derivation requires measured inventory velocity and finite non-negative price; price bands reject invalid/overflow evidence without contaminating valid aggregates; aging keeps missing cost as `null` and reports an unavailable critical value when evidence is incomplete; depletion keeps empty risk totals as `null` and preserves measured zero risk; derived reorder remains empty/non-actionable. Backend-owned primary merge paths are unchanged.
- Checks run: focused `analyticsIntelligenceDerived` suite passed 11/11; `npm run check:analytics-guardrails` passed including encoding, analytics guardrails and typecheck; `npm run build` passed; `git diff --check` passed.
- Checks not run: backend build/tests, live database/schema/migration/refresh worker, browser console smoke and cross-route export/report parity; this prompt is limited to the frontend derived service and its focused tests.
- Run log: `.ai/runs/2026-09-05-RQ152-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `4b47affc4b3b81ccf9591d080d648c47972141df`.
- Main verification: `git merge-base --is-ancestor 4b47affc4b3b81ccf9591d080d648c47972141df origin/main` passed; current `main` contains the delivered implementation commit.
- Missed: no forecast calibration, Shopify, backend formula, recommendation engine or live refresh work was included.
- Follow-up: `RQ153` is promoted to `READY` for the offline route lineage matrix; `STAB16` remains the owner of live provider/worker/browser proof.
- Residual risk: derived result types are local nullable extensions of legacy DTO shapes and are not yet a cross-route trust metadata contract; broader parity and runtime proof remain outside this prompt.
- Next: execute `RQ153`.

---

## RQ153 - Build the offline analytics route lineage matrix

Status: DONE
Priority: P1
Type: audit/docs/tests
Feature family: analytics-lineage-static-matrix
Parallel-safe: no, the matrix is the single cross-screen inventory owner
Owner: Codex
Commit suggestion: `docs(analytics): map offline route lineage`

### Problem

`RQ141` correctly requires a complete route/source/cache/refresh matrix, but its live refresh and broad contract scope cannot be completed honestly while `STAB16` is unresolved. The static portion can be extracted and completed independently: map the current React page, client, endpoint, DTO, service/query, schema and existing tests, explicitly marking runtime-only facts as unproven.

### Evidence

- `RQ137` and `RQ140` completion notes identify remaining cross-route period, scope, parity and live-refresh gaps.
- `RQ141` requires a matrix for `/analytics`, products, supplier, inventory, actions, decision-board, data-quality, reports and sales/trend/forecast/nivelacija families.
- `STAB16` owns provider worker registration and live refresh/browser proof; static code inspection must not claim that evidence.

### Scope

- A versioned offline matrix under `docs/qa/` covering every requested route and named analytics family.
- Static code/test mapping for React page/component, API client, endpoint, DTO, backend service, SQL/EF query, table/view/migration, cache identity/invalidation and refresh owner.
- For each row, record requested/effective/observed period, scope, generated time, last successful refresh, freshness, quality, empty/partial/error state, recommendation allowance and limitation; mark unknown/runtime-only values explicitly.
- No production calls, schema mutation, forecast calculation or Shopify work.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ137`, `RQ139`, `RQ140` and `RQ141`
- `docs/qa/` period, production and data-reliability evidence notes
- route/page/client/endpoint sources named by `RQ141`

### Do

1. Build the matrix from current code and nearest tests, with file/line references and an explicit confirmation basis.
2. Separate confirmed static ownership from unproven runtime freshness, refresh success, migration presence and browser behavior.
3. Identify duplicate or conflicting source owners and create narrowly bounded follow-up references without editing their formulas.
4. Add a lightweight completeness check or contract test so new required routes cannot silently disappear from the matrix.

### Tests

- Matrix completeness/static validation for all required routes and families.
- `git diff --check` and queue/planning validators.
- No live/runtime claim without `STAB16`; no forecast metric validation under this prompt.

### Acceptance

- The offline matrix covers every requested route/family and every required trust field.
- Each field is confirmed by source/test or explicitly marked unproven with owner and next proof.
- The document cannot be read as proof of live refresh, production schema or browser console behavior.

### Dependencies

- Reuse `RQ137`, `RQ139` and `RQ140` vocabulary; do not wait for their live residuals to complete the static inventory.
- `STAB16` remains the owner of live provider/worker/browser proof; this prompt is documentation/static validation only.

### Completion note

- Date: 2026-09-05
- Status: DONE
- Completion: Created the versioned offline route lineage matrix for all requested analytics routes and sales/trend/forecast/pre-post families, including static ownership, trust fields, duplicate owners and explicit runtime proof boundaries.
- Changed files: `docs/qa/ANALYTICS_ROUTE_LINEAGE_MATRIX_2026-09-05.md`; `scripts/check-analytics-lineage-matrix.mjs`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-09-05-RQ153-evidence.md`.
- Contract/runtime behavior changed: no product runtime behavior changed. The matrix and checker make unproven period, scope, freshness, quality, cache, refresh, schema and browser facts explicit; the checker prevents required routes/families or trust fields from disappearing silently.
- Checks run: `node scripts/check-analytics-lineage-matrix.mjs` passed with 17 analytics route/family rows and all required trust fields; `git diff --check` passed; agent-instruction, queue and planning validators passed before delivery.
- Checks not run: frontend/backend build/tests, live database/schema/migration/404/refresh, deployed worker proof, browser console/theme/chart smoke and export/table/chart/report runtime parity; these are explicitly outside this static prompt and remain unproven. The repository encoding check was run but failed on six pre-existing mojibake findings in unrelated historical `docs/qa` files; the new matrix was not among them.
- Run log: `.ai/runs/2026-09-05-RQ153-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `bb4f821a171f6e46c0d177e6cadea7f3d604fa95`.
- Main verification: `git merge-base --is-ancestor bb4f821a171f6e46c0d177e6cadea7f3d604fa95 origin/main` passed; current `main` contains the delivered matrix/checker commit.
- Missed: no production call, schema mutation, forecast calculation, Shopify work or runtime claim was made.
- Follow-up: keep `RQ141`/`RQ145`/`RQ146` and live `STAB16` work waiting behind their declared dependencies; no next RQ prompt is promoted.
- Residual risk: static lineage can identify owners and gaps but cannot prove deployed response parity, applied migrations, cache contents, refresh success or browser behavior.
- Next: `STAB16` for live provider/worker/browser proof; then promote the next dependency-satisfied RQ prompt.
- Prompt defect / scope repair: `RQ153` was correctly narrowed to the offline/static half of `RQ141`; live proof was intentionally not pulled into this task.

---

## RQ154 - Keep Daily Sales unknown numeric evidence unavailable

Status: DONE
Priority: P0
Type: frontend/contract/tests
Feature family: daily-sales-numeric-state
Parallel-safe: no, Daily Sales chart and summary state has one owner
Owner: Codex
Commit suggestion: `fix(analytics): preserve daily sales unknown values`

### Problem

The Daily Sales page has several local numeric fallbacks that can turn unknown, missing, empty-window or non-finite evidence into a trusted zero. The most visible cases are chart tooltip formatters, the seven-day rolling average and `safeDivide`. The API client also declares all row and metadata numbers as non-null, so a partial JSON response can bypass the intended unavailable state at the type boundary.

### Evidence

- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:327-332` returns `0` for an empty rolling window and adds every accessor result without rejecting unknown/non-finite values.
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:1651`, `:1690`, `:1738` and `:1791` use `Number(value ?? 0)` in Recharts tooltip formatters.
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:347-349` returns zero from `safeDivide` when the denominator is zero, and `:865-921` uses `?? 0` for trust/quality metadata without proving that metadata is present.
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts` declares row and metadata measures as required numbers even though runtime partial/null payloads are not represented by the client contract.
- The nearest `DailySalesStatsPage.spec.tsx` tests title and controls only; it does not fail for null, missing denominator, NaN, Infinity or a genuine zero alongside unavailable evidence.

### Scope

- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts` only if additive nullable DTO typing is required
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.spec.tsx`, `DailySalesStatsPage.premium.spec.tsx` or a focused helper spec
- The Daily Sales table, trend chart, shift-mix chart, weekday chart, supplier concentration and local summary/quality cards for the same response

Do not change backend business formulas, recommendation scoring, cache/refresh behavior, SQL, migrations, forecast logic or the broad cross-route parity owned by `RQ145`.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `RQ139`, `RQ144`, `RQ152` and `RQ153` completion notes
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts`
- nearest Daily Sales tests and shared analytics formatters

### Do

1. Add failing-first tests for an empty successful response, `null`/missing row values, a genuine measured zero, missing denominators, `NaN`, `Infinity` and a partial metadata payload.
2. Make the smallest state-preserving change so unknown/missing/insufficient/non-finite values render as the established unavailable text and are not included as zero in rolling averages, anomaly selection, shares or summaries.
3. Preserve a genuine valid zero as `0`; do not infer unknown from zero and do not remove a valid zero from charts or tables.
4. Keep empty success distinct from request error and keep existing stale/partial/fallback trust metadata visible.
5. Prove that the same daily row retains the same value/state in table, chart tooltip, local detail/export adapter and summary where those adapters exist. Do not create frontend recommendation or confidence semantics.
6. If the API DTO must become nullable, make the change additive and document the runtime assumption; do not claim that typing alone proves backend payload correctness.

### Tests

- Focused Daily Sales tests that fail before the fix for null/missing, valid zero, missing denominator, NaN and Infinity.
- Empty response remains a user-facing empty state and not an error or a zero-valued chart.
- Partial/fallback metadata remains visibly degraded/unavailable; no query time is shown as last refresh.
- `npm run test -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx` or the smallest equivalent focused set.
- `npm run check:analytics-guardrails`, typecheck/build if types change, `git diff --check` and queue/planning validators.

### Acceptance

- Null, missing, unknown, insufficient, NaN and Infinity never render or calculate as trusted zero on the scoped Daily Sales surfaces.
- A genuine measured zero remains zero.
- A zero denominator produces unavailable/degraded state, not a ratio of zero.
- Empty success, partial/fallback and request error remain visibly distinct with safe user-facing messages.
- Focused regression tests reproduce the old behavior before the fix and pass after it.
- No backend decision, recommendation status, confidence or cross-route parity claim is invented by this prompt.

### Dependencies

- None for the scoped frontend contract/test repair; reuse the numeric-state vocabulary established by `RQ139` and `RQ152`.
- `RQ145` remains the owner of complete table/chart/detail/export/report parity across all analytics routes.
- `STAB16` remains the owner of deployed worker, live refresh and browser-console proof; this prompt must not infer those facts.
- Do not promote forecast, Shopify, vendor comparison or recommendation-ranking work under this prompt.

### Completion note

- Date: 2026-09-06
- Status: DONE
- Completion: Daily Sales now preserves unknown, missing, insufficient and non-finite numeric evidence as unavailable across summaries, rolling averages, shares, anomaly selection, table/export adapters and chart tooltips; valid measured zero remains zero and a zero denominator remains unavailable.
- Changed files: `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`; `Klijent/clientapp/src/services/dailySalesStatsApi.ts`; `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.numericState.spec.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-06-RQ154-evidence.md`.
- Contract/runtime behavior changed: additive nullable Daily Sales DTO typing and page-owned state-preserving rendering/calculation only. Backend formulas, recommendation status, confidence, cache/refresh and SQL were not changed.
- Checks run: focused Daily Sales tests passed 12/12; `npm run check:analytics-guardrails` passed; `npm run typecheck` passed; `npm run build` passed; `git diff --check` passed.
- Checks not run: backend build/tests, live database/schema/migration/404/refresh, browser console/theme/chart smoke and cross-route export/report parity; those remain owned by `RQ145`, `RQ146` and `STAB16`.
- Run log: `.ai/runs/2026-09-06-RQ154-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `568aaf7de34a3915cfd67b7fbf537a1d6697f0c7` contains the implementation validated by this run.
- Main verification: current `main` contained `568aaf7de34a3915cfd67b7fbf537a1d6697f0c7` before the queue/evidence synchronization commit; final remote verification is recorded in the delivery commit evidence.
- Missed: no backend decision or recommendation semantics were invented; Dashboard trend work was tracked separately under `RQ155`.
- Residual risk: runtime payloads can still violate the additive frontend DTO assumption until backend/runtime proof is completed by the broader queue owners.
- Next: `RQ157` is already claimed by another local agent; do not steal that claim.
- Follow-up: preserve the active `RQ157` claim and lock; `RQ145`, `RQ146` and `STAB16` remain separate owners for parity/runtime proof.

---

## RQ155 - Keep unknown Dashboard trends visible and non-ranked

Status: DONE
Priority: P1
Type: frontend/tests
Feature family: dashboard-trend-unknown-visibility
Parallel-safe: no, Dashboard top-row ranking has one owner
Owner: Codex
Agent: local-session-ivan
StartedAtUtc: 2026-09-06T07:51:00Z
Commit suggestion: `fix(analytics): keep dashboard unknown trends visible`

### Problem

Dashboard top gainers and losers use `trendPct ?? 0` for both filtering and sorting. A missing trend is therefore silently treated as neutral and excluded from both lists. This makes an incomplete ranking look complete and hides the distinction between a measured zero trend and no comparable trend evidence.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:868-878` filters and sorts `topAdvanced.byRevenue` with `(row.trendPct ?? 0)`.
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx` covers positive and negative trends but has no null/unknown trend assertion.
- Existing RQ139/RQ143 contracts require unavailable metrics to stay unavailable and backend-owned ranking/decision semantics not to be recreated in the frontend.

### Scope

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- nearest Dashboard table/ranking regression spec and shared analytics display helper only if needed

Do not change the backend trend formula, backend ordering contract, recommendation status, score, confidence, forecast, inventory ranking or weekday/hour zero-fill behavior in this prompt.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `RQ139`, `RQ143` and `RQ145`
- `AnalyticsDashboard.tsx` and `AnalyticsDashboard.tableSystem.spec.tsx`

### Do

1. Add a failing-first fixture containing a positive trend, negative trend, genuine `0` trend and `null`/missing/non-finite trend.
2. Keep only finite measured positive values in gainers and finite measured negative values in losers; do not rank unknown values as zero.
3. Make unknown trend evidence visible in the existing table/detail context as the established unavailable state or an explicit safe count/message, without inventing an actionable recommendation.
4. Preserve backend-provided ordering/decision fields and keep export/table values consistent for the same row.

### Tests

- Dashboard regression test proving `null`, missing, NaN and Infinity are not silently ranked as neutral zero.
- Regression test proving genuine zero remains a measured neutral value and is not mislabeled as unavailable.
- Existing table/export parity test remains green.
- Focused frontend test, analytics guardrails, typecheck/build if changed, `git diff --check` and queue/planning validators.

### Acceptance

- Unknown/non-finite trend is visible as unavailable/degraded and is absent from gainers/losers ranking.
- Genuine zero trend remains visible as measured neutral zero.
- No frontend ranking, action, confidence or recommendation decision is created beyond the backend contract.
- No raw backend code is shown to the user.

### Dependencies

- Queue order is after `RQ154`; no backend dependency is required for the bounded page-owned fix.
- `RQ143` remains the owner of end-to-end backend decision/ranking ownership.
- `RQ145` remains the owner of complete cross-route parity and safe messaging.
- `STAB16` remains the owner of live browser/refresh evidence.

### Completion note

- Date: 2026-09-06
- Status: DONE
- Completion: Dashboard gain/loss lists now include only finite measured positive/negative trends, exclude null/missing/`NaN`/`Infinity`, show a measured zero as `Bez promene 0%`, and keep unknown trend evidence visible as `Nema trenda` in the existing top-product table/detail context. CSV export emits an empty trend cell for unknown/non-finite values instead of `NaN` or `Infinity`.
- Changed files: `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-06-RQ155-evidence.md`.
- Contract/runtime behavior changed: frontend display/filtering/export hardening only; backend ordering, formulas, recommendation, score and confidence ownership remain unchanged.
- Checks run: focused Dashboard regression suite passed 4/4; combined focused Daily Sales/Dashboard suite passed 16/16; `npm run check:analytics-guardrails` passed; `npm run typecheck` passed; `npm run build` passed; `git diff --check` passed.
- Checks not run: backend build/tests, live database/schema/migration/404/refresh, browser console/theme/chart smoke and complete cross-route report/export parity; these remain outside this prompt.
- Run log: `.ai/runs/2026-09-06-RQ155-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `568aaf7de34a3915cfd67b7fbf537a1d6697f0c7` contains the implementation validated by this run.
- Main verification: current `main` contained `568aaf7de34a3915cfd67b7fbf537a1d6697f0c7` before the queue/evidence synchronization commit; final remote verification is recorded in the delivery commit evidence.
- Missed: no backend ranking or actionability semantics were created; PDC `RQ157` remains a separate active claim.
- Residual risk: full cross-route parity and live browser/refresh proof remain unproven under `RQ145`/`STAB16`.
- Next: preserve the already-active `RQ157` claim and its lock.
- Follow-up: `RQ157` remains active for the separate Product Decision backend null-baseline/coverage repair.

---

## RQ156 - Keep unknown pre/post coverage distinct from measured zero

Status: DONE
Priority: P1
Type: frontend/tests
Feature family: pre-post-coverage-unknown-state
Parallel-safe: no, supplier/category pre/post wording has one presentation owner
Owner: Codex
Agent: Codex
StartedAtUtc: 2026-09-06T06:12:16Z
Commit suggestion: `fix(analytics): distinguish unknown pre-post coverage`

### Problem

Supplier, Color and Shoe Type pages coalesce a missing pre/post revenue coverage percentage to zero before selecting the display branch. A response with unknown coverage can therefore follow the same branch as measured zero coverage. Existing tests include some null payloads, but they do not prove that the user-facing state and any detail/export value preserve unknown versus true zero.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:363,428` use `(prePostNivelacijaRevenueCoveragePct ?? 0) <= 0`.
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx:204` and `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx:293` use the same null-to-zero branch.
- `SupplierSalesStatsPage.premium.spec.tsx` provides null pre/post fixtures, while Color tests primarily prove confidence/reliability do not fall back to coverage; no shared null-versus-zero coverage assertion spans the three pages.
- `RQ140` owns the causal/comparability contract and `RQ145` owns broad parity; this prompt is only a bounded presentation-state repair.

### Scope

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- nearest existing page tests; shared display helper only if it remains presentation-only

Do not change SQL, pre/post formulas, comparable-cohort selection, coverage calculation, backend recommendation status, confidence/reliability or live refresh behavior.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `RQ139`, `RQ140`, `RQ145` and the pre/post contract notes
- the three page implementations and their nearest specs

### Do

1. Add failing-first tests with coverage `null`/missing, measured `0`, positive coverage, invalid non-finite coverage and an explicit low-signal note.
2. Branch on unknown before numeric comparison so unknown coverage renders the established unavailable/low-signal explanation rather than a measured zero-coverage claim.
3. Preserve true `0%` coverage as a measured zero only when the backend explicitly supplies finite zero and the page labels it unambiguously.
4. Keep pre/post impact, units and recommendation status backend-owned; the frontend may only map the supplied state and safe explanation.
5. Verify table/detail/export values on each affected page do not turn unknown coverage into zero.

### Tests

- Focused Supplier, Color and Shoe Type tests for null, genuine zero, positive, NaN and Infinity coverage.
- Existing `prePostSignalNote` and low-signal behavior remains unchanged.
- Export/detail parity tests for the affected coverage field where those adapters exist.
- Focused frontend tests, analytics guardrails, typecheck/build if changed, `git diff --check` and queue/planning validators.

### Acceptance

- Unknown/missing/non-finite pre/post coverage is visibly unavailable or low-signal, never measured `0%`.
- Genuine finite `0%` remains distinguishable from unknown.
- No pre/post formula, causal claim, recommendation, confidence or reliability is recomputed in the frontend.
- User-facing messages are safe and explain the limitation without raw backend codes.

### Dependencies

- Queue order is after `RQ155`; this bounded UI test/fix does not wait for live database proof.
- `RQ140` remains the owner of causal comparability and backend pre/post semantics.
- `RQ145` remains the owner of complete cross-surface parity.
- `RQ146` and `STAB16` retain schema, migration, refresh and deployed-runtime proof.

---

### Completion note

- Date: 2026-09-06
- Status: DONE
- Completion: Supplier revenue/units and Color/Shoe Type pre/post presentation now keep unknown, invalid and measured-zero coverage states distinct; non-finite impact values fail closed.
- Changed files: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`, `Klijent/clientapp/src/pages/__tests__/PrePostCoveragePresentation.spec.tsx`, `.ai/runs/2026-09-06-RQ156-evidence.md`
- Contract/runtime behavior changed: Presentation-only mapping now requires finite non-negative coverage; missing/null/NaN/Infinity/negative coverage renders unavailable, finite `0%` renders explicit measured zero coverage, and non-finite impact cannot be formatted as a decision metric. Backend formulas, recommendations, confidence/reliability and refresh behavior were not changed.
- Checks run: `npm run test:run -- src/pages/__tests__/PrePostCoveragePresentation.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/ShoeTypeSalesStatsPage.spec.tsx` (35 passed); `npm run check:analytics-guardrails` (pass); `npm run build` (pass); `git diff --check` (pass).
- Checks not run: Backend build/tests, live browser/console and deployed database/refresh proof were not run because RQ156 is explicitly frontend-only and those surfaces belong to RQ140/RQ146/STAB16.
- Run log: `.ai/runs/2026-09-06-RQ156-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 7a3cc04080790a170b5452facbdc7afbd468946f
- Main verification: `git ls-remote origin refs/heads/main` returned `99919292484fad80bc92ebc0d92df0e83190f45c`; implementation SHA is an ancestor of current `main`.
- Missed: No export adapter is owned by these page helpers; broad table/chart/detail/export/report parity remains RQ145 scope.
- Follow-up: RQ160 is the current READY inventory-surface prompt; RQ145 owns broad parity and RQ146/STAB16 own runtime/schema/refresh proof.
- Residual risk: Live deployed data and browser console behavior remain unverified outside this bounded frontend prompt.
- Prompt defect / scope repair: Added direct helper exports solely to make the three presentation decisions regression-testable; no business logic was moved to the frontend.

---

## RQ157 - Preserve missing Product Decision baseline and coverage evidence

Status: DONE
Priority: P0
Type: backend/contract/tests
Feature family: pdc-baseline-coverage-state
Parallel-safe: no, Product Decision backend is the owner of recommendation evidence
Owner: Codex
Agent: local-session-ivan
StartedAtUtc: 2026-09-06T08:00:00Z
CompletedAtUtc: 2026-09-06T08:20:00Z
Commit suggestion: `fix(analytics): preserve missing pdc evidence`
Evidence: `.ai/runs/2026-09-06-rq157-pdc-baseline-coverage-evidence.md`
Evidence state: synchronized

### Completion note

- Missing previous-revenue baseline no longer synthesizes `trendPct=100`; `ComputeTrendPct` returns null for missing/zero previous.
- Null `TrendPct` / `MarginPct` fail closed to `INSUFFICIENT_DATA` with `insufficient_history` in PDC reasoning.
- Null margin/split coverage in `AnalyticsDecisionRecommendationEngine` no longer coalesce to measured zero; split null → `missing_split_coverage` / `insufficient_data`.
- Focused tests: ProductDecisionReasoningHelperTests, AnalyticsDecisionRecommendationEngineTests, ProductDecisionCenterBuilderIntegrationTests — 26 passed.
- Next READY: RQ160 after RQ158 and RQ159 completed their inventory contracts.

### Problem

Product Decision Center can turn missing evidence into measured values. A missing previous-revenue baseline can be represented as zero and produce `trendPct=100` for a product with positive current revenue. Null trend, margin or coverage can also enter decision/reasoning paths as zero, allowing a recommendation or reason to be produced without the required denominator evidence.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5702-5709` defaults a missing previous-revenue dictionary value to `0` and emits `100%` growth when current revenue is positive.
- `Application/Analytics/ProductDecisionReasoningHelper.cs:59-62,99` uses `(input.TrendPct ?? 0m)` and `(input.MarginPct ?? 0m)` in decision and reason logic.
- `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs:36-38,49,108-109` maps missing margin/split coverage to zero; unknown split coverage can therefore avoid its warning and quality downgrade.
- Existing engine/reasoning tests do not prove that an otherwise actionable row with null trend, margin or split coverage fails closed.

### Scope

- `Application/Analytics/ProductDecisionReasoningHelper.cs`
- `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` only the Product Decision Center builder and its baseline/coverage mapping
- `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`
- `Api.Tests/ProductDecisionReasoningHelperTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`

Do not change frontend ranking, forecast logic, Shopify/vendor integrations or unrelated margin-basis policy. `RQ148` remains the owner of the wider sales-margin measurement basis.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `RQ143`, `RQ145`, `RQ148` and the current PDC response/meta contract
- the listed backend implementations and nearest tests

### Do

1. Add failing-first tests for missing previous baseline, a true previous revenue of zero, null trend, null margin and null split/margin coverage.
2. Preserve unavailable/insufficient state when a denominator or baseline is absent; never synthesize `+100%`, `0%` margin or `0%` coverage from missing evidence.
3. Keep a true finite zero distinct from null/missing/invalid evidence. Non-finite values must fail closed at the contract boundary.
4. Set backend-owned recommendation status/allowed state, data quality and reason consistently when decision-required evidence is missing. Do not let unknown coverage silently pass because it was coalesced to zero.
5. Keep response metadata and safe user-facing reason mapping compatible with existing consumers.

### Tests

- Failing-first unit and integration tests for missing baseline, true zero baseline, missing margin/trend/coverage, valid zero and non-finite values where the DTO path permits them.
- Assert no actionable recommendation is allowed when required evidence is unavailable, and that no fake confidence/reliability is emitted.
- Assert empty, partial/fallback and error metadata remain distinct from measured zero.
- Run focused backend tests, analytics guardrails, `git diff --check` and the selected build/test commands from `docs/ai/VALIDATION_SELECTOR.md`.

### Acceptance

- Missing or invalid baseline never becomes `trendPct=100`.
- Missing trend, margin or coverage is unavailable/insufficient and cannot influence a measured decision as zero.
- A genuine finite zero remains a valid measured zero where the domain contract permits it.
- Backend remains the sole owner of recommendation, score, confidence/reliability, reason and allowed status.
- No raw backend codes or misleading numeric fallbacks are exposed to users.

### Dependencies

- Queue order is after `RQ154`-`RQ156`; keep this prompt `WAITING` while `RQ154` is the sole `READY` item.
- `RQ143` remains the end-to-end decision/ranking ownership gate.
- `RQ145` remains the complete table/chart/detail/export/report parity gate.
- `RQ148` remains the broader sales-margin/returns measurement-basis follow-up.
- `STAB16` remains the live refresh/browser proof owner.

---

## RQ158 - Keep null inventory stock evidence unavailable

Status: DONE
Priority: P0
Type: backend/contract/frontend/tests
Feature family: inventory-null-stock-state
Parallel-safe: no, inventory status has one backend/frontend contract owner
Owner: Codex
Agent: local-session-ivan
StartedAtUtc: 2026-09-06T08:18:00Z
CompletedAtUtc: 2026-09-06T08:25:00Z
Commit suggestion: `fix(analytics): preserve null inventory stock state`
Evidence: `.ai/runs/2026-09-06-rq158-inventory-null-stock-evidence.md`
Evidence state: synchronized

### Completion note

- Null quantity is never counted as OOS; measured zero remains OOS.
- Low-stock requires known quantity (and known minimum on balance endpoint).
- List/detail preserve nullable quantity/minimum; missing quantity fails closed for signals.
- Estimated value stays unavailable without quantity/cost; measured zero quantity is true zero capital.
- Frontend `getStockState` / `buildInventoryRow` render unknown vs measured zero distinctly.
- Tests: InventoryStockEvidenceTests + inventory fake-zero specs + list/signal inventory filters — passed.
- Next READY: RQ160.

### Problem

Inventory quantity and minimum-stock fields are nullable, but the aggregate handler, endpoint projections and frontend utility coalesce null to zero. This counts unknown quantity as measured out-of-stock and can label positive stock as stable when the minimum threshold is missing.

### Evidence

- `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusHandler.cs:22-27` uses `(Kolicina ?? 0)` for total, low-stock and OOS counts.
- `Api/Endpoints/InventoryEndpoints.cs:43-51,208` repeats null-to-zero balance and detail projections.
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts:214-231` maps null quantity and minimum to zero before stock-status and coverage logic.
- Existing inventory tests cover true zero and insufficient evidence, but do not cover nullable quantity/minimum in aggregate, list, detail and UI state together.

### Scope

- `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusHandler.cs`
- `Api/Endpoints/InventoryEndpoints.cs` inventory balance/list/detail projections only
- `Api/Dtos/InventoryListItemDto.cs` and `Api/Dtos/InventoryExperienceDtos.cs` only if an additive state field is needed
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- nearest backend and frontend inventory regression tests

Do not add forecast calculations or change forecast endpoints. Do not invent historical demand from a stock snapshot.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ149`, `RQ145`, `RQ146` and the inventory DTO/meta contract
- `Api.Tests/InventorySignalCalculatorTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.fakeZeroValue.spec.ts`
- the listed handler, endpoint and utility

### Do

1. Add failing-first fixtures for empty data, null quantity, true quantity zero, positive quantity with null minimum, valid minimum zero/positive and missing cost evidence.
2. Preserve null/missing quantity as unknown or insufficient; never count it as OOS or as a measured total of zero.
3. Preserve true quantity zero as measured OOS when the backend has an authoritative zero.
4. Keep missing minimum from becoming a stable-stock classification; make the limitation visible and recommendation status fail closed when the decision requires it.
5. Keep estimated inventory value unavailable when required quantity or cost evidence is missing. Preserve empty/error/partial metadata and table/detail parity.

### Tests

- Backend handler/endpoint tests for null versus true zero quantity and minimum, including OOS and low-stock counts.
- Frontend utility/page tests for null, true zero, valid positive stock, invalid non-finite numeric values and missing cost.
- Export/detail parity assertions for the affected inventory fields where adapters exist.
- Run focused inventory tests, analytics guardrails, `git diff --check` and the selected frontend/backend validation.

### Acceptance

- Null/missing quantity is never displayed or counted as valid OOS zero.
- Null/missing minimum is never treated as a valid threshold or stable-stock proof.
- Genuine zero quantity remains distinguishable and is classified according to the backend contract.
- Backend owns inventory status and recommendation allowance; frontend only renders supplied state.
- Empty is a valid empty state, while partial/fallback/error/stale states are visible and not converted to zero.

### Dependencies

- Queue order is after `RQ157`; keep this prompt `WAITING` behind the single `READY` invariant.
- `RQ149` remains the owner of inventory economics and availability-censored demand evidence.
- `RQ145` remains the parity owner, and `RQ146` the runtime schema/404/migration owner.
- `STAB16` retains live refresh/browser proof.

---

## RQ159 - Correct inventory decision summary counts and wording

Status: DONE
Priority: P1
Type: frontend/contract/tests
Feature family: inventory-decision-summary-counts
Parallel-safe: no, the summary card and inventory balance contract must share one metric meaning
Owner: Codex
Agent: local-session-ivan
StartedAtUtc: 2026-09-06T08:26:00Z
CompletedAtUtc: 2026-09-06T08:28:00Z
Commit suggestion: `fix(analytics): align inventory decision summary counts`
Evidence: `.ai/runs/2026-09-06-rq159-inventory-decision-summary-evidence.md`
Evidence state: synchronized

### Completion note

- Removed `lowStockCount - outOfStockCount` subtraction; cards show separate current OOS and current low-stock counts.
- Removed `P1 OOS 7d` label; wording is current-snapshot only.
- Null counts render `Nije dostupno`; measured zero remains `0`.
- Focused DecisionSummaryBar tests pass (5).
- Next READY: RQ160.

### Problem

`DecisionSummaryBar` subtracts the out-of-stock count from the low-stock count even though the backend low-stock predicate excludes OOS rows. It also labels current counts as `P1 OOS 7d`, although no seven-day OOS-risk measurement is supplied. This can create negative or semantically false decision counts.

### Evidence

- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.tsx:25-26` computes `lowStockCount - outOfStockCount`.
- `GetInventoryStatusHandler` defines low stock as positive quantity at or below threshold and OOS separately.
- `Klijent/clientapp/src/pages/InventoryPage.tsx:1278-1283` passes current balance counts directly to the summary.
- `DecisionSummaryBar.spec.tsx` covers trust copy but not count arithmetic or the seven-day label.

### Scope

- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.tsx`
- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.spec.tsx`
- `Klijent/clientapp/src/pages/InventoryPage.tsx` wiring and metric labels only if required
- additive backend DTO field only if a measured seven-day risk already exists and is authoritative

Do not invent a seven-day risk from current counts, a snapshot or a forecast. If no authoritative seven-day metric exists, remove or relabel the card to describe current stock state.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ149`, `RQ145`, `RQ143` and inventory metric definitions
- `DecisionSummaryBar.tsx`, `InventoryPage.tsx` and the nearest specs

### Do

1. Add a failing-first fixture with `lowStockCount=10` and `outOfStockCount=5`; prove the current implementation is wrong/negative or mislabeled.
2. Align the card with the backend metric definitions: show separate current low-stock and current OOS counts, or consume an explicitly backend-measured seven-day risk field.
3. Treat null counts as unavailable, not zero; prevent negative derived counts.
4. Keep recommendation/action eligibility backend-owned and preserve the same values in card, table, chart, details and export.
5. Use safe user-facing wording that states current versus observed-window semantics.

### Tests

- Unit tests for low-only, OOS-only, both, zero, null and partial counts.
- Regression test asserting no negative count and no `7d` label for a current snapshot.
- Table/card/export parity test if the same count is exported.
- Focused frontend tests and analytics guardrails; no forecast test is a substitute for measured inventory risk.

### Acceptance

- No subtraction of mutually exclusive low-stock and OOS counts.
- No current snapshot count is labelled as seven-day risk.
- Null/partial counts render unavailable/degraded state rather than valid zero.
- The card uses the backend metric meaning and cannot create a different decision from the backend.

### Dependencies

- Queue order is after `RQ158` because nullable stock semantics must be settled first.
- `RQ143` remains the recommendation/action ownership gate.
- `RQ145` remains the cross-surface parity gate.

---

## RQ160 - Remove synthetic inventory health trend or make it observed

Status: READY
Priority: P0
Type: frontend/backend-contract/tests
Feature family: inventory-health-observed-series
Parallel-safe: no, health score provenance needs one metric owner
Owner: Codex
Commit suggestion: `fix(analytics): stop fabricating inventory health trend`

### Problem

`InventoryPage` computes a local weighted health score from one current snapshot and fabricates seven sparkline points by applying fixed drift. The result looks like an observed historical trend although no historical observations or backend-owned score are supplied.

### Evidence

- `Klijent/clientapp/src/pages/InventoryPage.tsx:543-556` calculates `inventoryHealthScore` locally and creates seven `healthTrendPoints` from fixed drift.
- Existing page tests mock the summary bar but do not assert that a single snapshot cannot produce an observed trend.
- The backend inventory contract does not expose a provenance-bearing historical health series for this page.

### Scope

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- inventory types/API only if an additive backend-owned historical score/series is already supported by the owning service
- nearest InventoryPage tests and metric/explainability documentation if needed

Do not introduce a forecast, heuristic trend or frontend recommendation formula. Prefer removing the score/sparkline and stating snapshot-only evidence when an authoritative observed series is unavailable.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ143`, `RQ147`, `RQ149`, `RQ145` and current inventory DTOs
- `InventoryPage.tsx` and nearest page tests

### Do

1. Add a failing-first test proving one snapshot cannot yield seven observed trend points.
2. Either consume a backend-owned historical score/series with requested/effective/observed period, source, freshness and quality metadata, or remove the score/sparkline and render an explicit snapshot-only limitation.
3. Preserve valid zero versus unknown semantics and guard non-finite values.
4. Do not let the frontend recompute a score, confidence or recommendation that the backend owns.
5. Keep all remaining inventory values aligned across card, table, chart, detail, export and report surfaces.

### Tests

- Page tests for one snapshot, empty data, true zero, null and non-finite inputs.
- Regression test that no synthetic seven-point series is rendered without observed backend points and provenance.
- Theme and chart initial-size tests remain green, including width/height `0` and `-1` behavior.
- Focused frontend tests, analytics guardrails and build/type validation selected by the canonical validator.

### Acceptance

- A single snapshot is never presented as an observed historical trajectory.
- Any displayed health score/series has backend ownership and explicit period/source/freshness/quality.
- Otherwise the UI clearly says snapshot-only/no historical trend instead of fabricating points.
- No forecast or frontend decision logic is introduced.

### Dependencies

- Queue order is after `RQ159`; keep `RQ160` `WAITING` until its metric ownership is available.
- `RQ147` remains the KPI evidence registry owner.
- `RQ149` remains the inventory economic/availability evidence owner.
- `RQ145` and `STAB16` retain parity and live proof ownership.

---

## RQ161 - Fail closed on Analytics Details periods and unknown trends

Status: WAITING
Priority: P1
Type: frontend/contract/tests
Feature family: analytics-details-period-state
Parallel-safe: no, details period and ranking display must match shared analytics period semantics
Owner: Codex
Commit suggestion: `fix(analytics): validate details periods and unknown trends`

### Problem

`AnalyticsDetails` clamps a reversed date range to one day and can produce `NaN` for invalid dates. It also ranks and direction-labels missing trends as zero. Users can therefore see plausible per-day values for an invalid period and a neutral/up trend for unavailable evidence.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx:39-45` uses `Math.max(1, ...)`; invalid dates produce `NaN` and reversed periods become one day.
- `AnalyticsDetails.tsx:235-244` ranks gainers/losers with `(x.trendPct ?? 0)`.
- `AnalyticsDetails.tsx:413,491` uses `(row.trendPct ?? 0) >= 0` for direction styling.
- `Klijent/clientapp/src/pages/__tests__/analyticsIndicatorRegression.spec.ts` covers missing summary and valid zero but not reversed/invalid periods or non-finite detail trends.

### Scope

- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx`
- `Klijent/clientapp/src/pages/__tests__/analyticsIndicatorRegression.spec.ts`
- nearest Analytics Details tests and shared period helper only if the change remains a compatibility-preserving validation fix

Do not change backend ranking ownership, forecast logic, Shopify/vendor work or cross-route parity contracts owned by `RQ137`/`RQ145`.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ137`, `RQ143`, `RQ145` and shared analytics period semantics
- the listed page and nearest regression tests

### Do

1. Add failing-first tests for reversed, invalid, empty and valid date ranges.
2. Fail closed for invalid/reversed periods with a clear user-facing message and no fetch or no derived KPI, according to the existing page contract.
3. Keep exact inclusive day count for valid ranges and reject non-finite date-derived values.
4. Exclude null/missing/non-finite trends from gain/loss ranking and direction labels; retain true finite zero as neutral.
5. Preserve backend-owned decisions and safe wording; do not expose raw error codes.

### Tests

- Period tests for valid one-day/multi-day, reversed and invalid dates, with fetch behavior asserted.
- Trend tests for null, missing, NaN, Infinity, negative, positive and true zero.
- Empty, partial/fallback and error response tests remain distinct.
- Focused frontend tests, analytics guardrails, build/type validation and `git diff --check`.

### Acceptance

- Invalid/reversed period cannot produce plausible KPI data or a one-day fallback.
- Valid period day count is exact and finite.
- Unknown/non-finite trends are visibly unavailable and not ranked or direction-labelled.
- Genuine zero trend remains neutral.
- No frontend decision, confidence or recommendation is recomputed.

### Dependencies

- Queue order is after `RQ160`; keep this prompt `WAITING` behind the single `READY` item.
- `RQ137` remains the period-lineage owner.
- `RQ143` remains the backend decision/ranking owner.
- `RQ145` remains the parity and safe-messaging owner.

---

## RQ162 - Keep partially missing sell-through denominator evidence unavailable

Status: WAITING
Priority: P0
Type: backend/tests
Feature family: inventory-sellthrough-denominator-state
Parallel-safe: no, denominator semantics must be shared by inventory list, detail and decision payloads
Owner: Codex
Commit suggestion: `fix(analytics): block partial inventory sell-through denominators`

### Problem

`InventorySignalCalculator.CalculateSellThrough` only blocks when both denominator inputs are null, then converts either single missing input to zero. If the contract requires both opening stock and inbound units, a partial denominator can therefore produce a plausible sell-through ratio, status and confidence from incomplete evidence. This is separate from RQ158, which covers null stock/minimum rendering.

### Evidence

- `Api/Endpoints/InventorySignalCalculator.cs:137-151` rejects only the both-null case and calculates with `openingStockUnits ?? 0` plus `inboundUnits ?? 0`.
- `Api.Tests/InventorySignalCalculatorTests.cs:99-114` covers both inputs missing, but has no regression for exactly one missing input.
- `Api/Endpoints/InventoryEndpoints.cs:131-145`, `332-345` and `1441-1455` wire the signal into inventory decision payloads, so a calculator contract change must be checked at all call sites.

### Scope

- `Api/Endpoints/InventorySignalCalculator.cs`
- `Api/Endpoints/InventoryEndpoints.cs` only if caller evidence wiring must change
- `Api.Tests/InventorySignalCalculatorTests.cs`
- nearest inventory endpoint/integration tests for response parity

Do not touch forecast, trend, Shopify/vendor work or unrelated inventory null-stock behavior owned by `RQ158`.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ149`, `RQ158` and the inventory stock-cover/sell-through contract
- the calculator and all current callers/tests listed above

### Do

1. Add failing-first tests for opening-only missing, inbound-only missing, both missing, both zero, valid positive denominator with genuinely zero sold units, and negative/non-finite boundary inputs where the DTO boundary permits them.
2. Confirm whether both denominator components are required by the established business contract. If one component is intentionally optional, document that rule and prove it instead of applying a blanket null block.
3. When required evidence is partial, return a null ratio, `insufficient_data`, a clear reason and `recommendationAllowed=false`; never substitute zero.
4. Preserve a genuine zero denominator and genuine zero sold-units result as distinct states.
5. Keep score, confidence, reliability and recommendation ownership on the backend and verify list/detail/card parity.

### Tests

- Focused calculator tests for every missing/zero/non-finite counterexample.
- Inventory endpoint/integration parity tests for the same payload through list and detail paths.
- `git diff --check`, analytics guardrails and the narrow backend build/test selected by `VALIDATION_SELECTOR.md`.

### Acceptance

- A single missing required denominator component cannot produce a numeric sell-through ratio or actionable recommendation.
- A real zero remains visibly zero, while missing/insufficient evidence remains unavailable.
- Inventory list, detail, card and export payloads preserve the same backend-owned state.
- No frontend formula recreates the decision.

### Dependencies

- `RQ158` remains the owner of null inventory quantity/minimum semantics.
- `RQ149` remains the owner of inventory economic/availability evidence.
- `RQ145` remains the parity and safe-messaging owner.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ163 - Prevent absent post-nivelacija observations from becoming measured zero

Status: WAITING
Priority: P0
Type: backend/SQL/tests
Feature family: supplier-post-observation-state
Parallel-safe: no, supplier ratios, scores and recommendations share the post-observation contract
Owner: Codex
Commit suggestion: `fix(analytics): preserve supplier post observation state`

### Problem

The supplier decision SQL left-joins post-nivelacija observations and immediately coalesces missing `post_qty` and `post_revenue` to zero. That makes “no post observation matched” indistinguishable from a measured post-period zero, then feeds full-price share, markdown dependency, dead-stock rate, confidence and recommendation branches. Existing materialized-view tests prove that a coverage column exists in one cache family, but do not prove the direct supplier query and every article/detail path use it to gate decisions.

### Evidence

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:2794-2816` maps an unmatched `vw_vendor_sales_nivelacija` row to `post_qty_30d=0` and `post_revenue_30d=0`.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:2829-2867` aggregates those values into revenue shares, sell-through, markdown dependency and dead-stock metrics.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:2936-2979` coalesces derived ratios to zero and calculates confidence from the resulting rows.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:3011-3045` and `3058-3070` can still emit recommendation codes; `BuildScorecardTrustMetadata` at `2530-2588` does not include post-observation coverage in its recommendation gate.
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs:252-313` verifies coverage in windowed cache SQL, but `Api.Tests/SupplierDecisionHubContractTests.cs` has no absent-post-vs-real-zero regression.

### Scope

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api.Tests/SupplierDecisionHubContractTests.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- related supplier SQL/migration only if the existing view cannot expose an unambiguous observation state

Do not duplicate the full causal-comparability redesign owned by `RQ140`; do not touch trend, forecast, Shopify or unrelated supplier ranking formulas.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ140`, `RQ145`, `RQ147`, `RQ156` and the supplier decision schema/migration contract
- the direct and precomputed supplier query paths and their current tests

### Do

1. Add failing-first fixtures for no matching post observation, measured post quantity/revenue equal to zero, partial post fields, and complete post evidence.
2. Carry an explicit post-observation/coverage state through direct SQL, precomputed cache, DTO mapping and trust metadata; do not infer it from a coalesced numeric zero.
3. When required post evidence is absent or partial, keep ratios and confidence unavailable or conservatively bounded, set `recommendationAllowed=false` where required by the established contract, and expose a safe limitation reason.
4. Keep genuine measured zero valid and distinct from missing evidence.
5. Prove summary, quadrant/ranking, article detail, export and report parity; do not recalculate decision logic in the frontend.

### Tests

- SQL contract tests for explicit coverage/state and recommendation gating.
- Supplier contract tests for absent post, valid zero, partial and complete evidence.
- Parity tests across direct query and materialized-view/cache paths.
- `git diff --check`, analytics guardrails and focused backend validation.

### Acceptance

- Missing post observation never appears as measured zero or supports a trusted supplier recommendation.
- Genuine post zero remains a valid measured value.
- Confidence/reliability and actionability are absent/blocked when the evidence basis is insufficient.
- Every supplier surface exposes the same backend-owned state and safe explanation.

### Dependencies

- `RQ140` remains the owner of causal comparability and live refresh proof.
- `RQ156` remains the frontend unknown-coverage owner.
- `RQ145` and `RQ147` remain parity and metric-evidence owners.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ164 - Prevent null/non-positive purchase cost from becoming a complete 100% margin signal

Status: WAITING
Priority: P0
Type: backend/tests
Feature family: pre-nivelacija-cost-evidence
Parallel-safe: no, cost evidence drives pre-nivelacija score and scenario outputs
Owner: Codex
Commit suggestion: `fix(analytics): gate pre-nivelacija margin on cost evidence`

### Problem

The pre-nivelacija candidate builder sets missing purchase price to zero and accepts `PurchasePrice >= 0` as complete evidence. A null or zero cost can therefore enter the complete-evidence branch and produce a 100% gross margin, influencing score filtering and scenario values. This conflicts with the established positive-cost policy used by data-quality and margin helpers.

### Evidence

- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:258-266` coalesces `PurchasePrice` to zero and treats a non-negative purchase price as complete, so zero cost yields `grossMarginPct=100` when selling price is positive.
- `Api/Services/AnalyticsMarginPolicy.cs` and `Infrastructure/Services/AnalyticsDataQualityHealthService.cs:44,98` treat null/non-positive purchase cost as missing evidence.
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:268-294` passes the derived margin into filtering, score computation and scenario simulation.
- `Api.Tests/PreNivelacijaScoringServiceTests.cs` covers scenario arithmetic and an incomplete candidate DTO, but not the endpoint branch where `PurchasePrice=0` is classified complete.

### Scope

- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Api/Services/PreNivelacijaScoringService.cs` only if the contract boundary requires a service-level guard
- `Api.Tests/PreNivelacijaScoringServiceTests.cs`
- nearest pre-nivelacija endpoint/query-failure tests

Do not touch forecast, trend, Shopify or unrelated margin/return measurement owned by `RQ148`.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ139`, `RQ140`, `RQ148` and `AnalyticsMarginPolicy`
- the pre-nivelacija DTO, endpoint and scoring tests

### Do

1. Add failing-first tests for null, zero, negative, positive and genuinely zero-margin cost inputs.
2. Align completeness with the established positive-cost policy. If the business explicitly permits zero acquisition cost, create a separate documented evidence state instead of treating it as an ordinary margin denominator.
3. Do not derive 100% margin, confidence or scenario output from missing/invalid cost; return an unavailable/insufficient reason and block recommendation where required.
4. Preserve valid positive-cost arithmetic, including a genuine zero-margin result, and keep all score/scenario outputs backend-owned.
5. Verify candidate filtering, response DTO, export/report if present and user-safe messaging remain consistent.

### Tests

- Focused endpoint/scoring tests for all cost states and score/filter behavior.
- Regression proving missing cost cannot pass a margin floor or produce actionable confidence.
- Regression proving positive cost with equal selling price yields valid zero margin.
- Focused backend build/test and analytics guardrails.

### Acceptance

- Null, zero and negative cost are never silently converted into a 100% margin signal.
- Valid positive cost and genuine zero margin remain measurable.
- Recommendation/confidence is unavailable or blocked when cost evidence is insufficient.
- No frontend code recomputes margin or actionability.

### Dependencies

- `RQ148` remains the broader sales/margin measurement-basis owner.
- `RQ139` and `RQ140` remain owners of existing denominator and pre/post semantics.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ165 - Make Data Quality time boundaries and sale/article scope consistent

Status: WAITING
Priority: P0
Type: backend/SQL/tests
Feature family: data-quality-window-scope
Parallel-safe: no, health percentages and top-offender impact must describe the same population
Owner: Codex
Commit suggestion: `fix(analytics): align data quality window and scope semantics`

### Problem

Data Quality has two remaining consistency risks. The top-offender SQL and issue handler use only a lower bound for their “30d” sales window, so future-dated sales can enter the result. The health snapshot filters sales by article `DataOrigin`, while the canonical top-offender contract scopes sales by sale-header origin, so health and offender surfaces can describe different populations for the same `dataScope`.

### Evidence

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs:10-29` documents sale-header scope, but `TopOffendersSql:17-28` has `p.datum_prodaje >= @salesFromUtc` without an upper boundary.
- `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs:31-43` repeats the lower-only window in `sales_30d`.
- `AnalyticsDataQualityHealthService.CaptureAsync:124-147` bounds the date but filters `dataScope` using article origin only at `130-132`, creating a scope mismatch with sale-header-based revenue impact.
- Existing `Api.Tests/DataQualityIssuesHandlerTests.cs:245-370` proves basic imported/existing scope, while `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs` does not prove future-date exclusion and health/offender population parity together.

### Scope

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs`
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
- `Api.Tests/DataQualityIssuesHandlerTests.cs`
- `Api.Tests/DataQualityPostgresIntegrationTests.cs` only if relational boundary proof is needed

Do not change the established `RQ05`/`RQ06` dataScope definitions, frontend formulas, trend, forecast or Shopify behavior.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ05`, `RQ06`, `RQ118`, `RQ135` and `RQ144`
- the health service, issues handler and current scope tests

### Do

1. Add failing-first fixtures with in-window, boundary, future-dated and empty sales rows.
2. Define one explicit finite interval, preferably `[fromUtc, toExclusiveUtc)`, and apply it consistently to health, top offenders and issues.
3. Apply the canonical sale-header/article scope rule consistently, or expose a deliberate documented distinction if the two surfaces intentionally serve different populations.
4. Keep no-sales, valid zero revenue and unavailable denominator states distinct; do not turn future/scope-excluded rows into valid impact.
5. Prove percentage denominators, empty results, exports/reports and safe user-facing quality status remain aligned.

### Tests

- Unit/SQL contract tests for lower boundary, upper exclusive boundary and future dates.
- Integration tests for imported/existing/all scope across health and offender/issue surfaces.
- Tests for empty, valid zero and unavailable denominator states.
- `git diff --check`, analytics guardrails and focused backend validation.

### Acceptance

- Future-dated sales cannot enter a 30-day Data Quality result.
- Health, top-offender and issue results describe the same declared scope and interval.
- Empty and zero evidence remain distinct from query failure or unknown denominator.
- No raw backend scope/error code is exposed as user messaging.

### Dependencies

- `RQ05`, `RQ06` and `RQ118` remain the canonical scope owners; this is a bounded residual consistency fix.
- `RQ144` remains the health denominator owner.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ166 - Reject reversed action-timeline periods instead of silently swapping scope

Status: WAITING
Priority: P1
Type: backend/tests
Feature family: action-timeline-period-state
Parallel-safe: no, action timeline and export must preserve the same requested/effective period
Owner: Codex
Commit suggestion: `fix(analytics): fail closed on reversed action periods`

### Problem

The action timeline filter silently swaps a reversed period, and the product-decision timeline endpoint repeats that behavior. A caller that requests `from > to` can therefore receive a valid-looking timeline for a different effective period, while export metadata reports the swapped range instead of the requested invalid scope.

### Evidence

- `Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs:23-42` swaps `periodFromUtc` and `periodToUtc` when the request is reversed.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:5376-5392` repeats the swap before calling the projection.
- `Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs` and `Api.Tests/DecisionTimelineExportProjectionTests.cs` cover valid/outside-period behavior but have no reversed-period regression.

### Scope

- `Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` timeline filter path only
- `Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs`
- `Api.Tests/DecisionTimelineExportProjectionTests.cs`

Do not alter action outcome measurement, recommendation ownership, forecast/trend logic or unrelated dashboard period normalization.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ137`, `RQ145`, `RQ151` and the decision timeline export contract
- the filter, endpoint and export tests listed above

### Do

1. Add failing-first tests for valid one-day/multi-day, reversed, equal and invalid/non-finite period inputs.
2. Fail closed on a reversed/invalid period without swapping it into a different valid scope; preserve requested/effective period semantics in the response and export.
3. Keep valid inclusive period behavior unchanged and distinguish no events from invalid request.
4. Verify frontend/export/report messaging uses safe wording and does not expose raw validation codes.

### Tests

- Focused filter, endpoint and export tests for period state and parity.
- Empty/no-event versus invalid-period tests.
- `git diff --check`, analytics guardrails and focused backend validation.

### Acceptance

- Reversed or invalid input cannot return a plausible timeline for a swapped period.
- Valid periods remain unchanged and exports match the table/timeline effective period.
- Invalid, empty and error states remain distinct with user-safe messaging.

### Dependencies

- `RQ137` remains the shared period-lineage owner.
- `RQ145` remains the parity and safe-messaging owner.

---

## RQ167 - Do not serialize failed sales/inventory KPI responses as valid-looking zero values

Status: WAITING
Priority: P0
Type: backend/contract/tests
Feature family: analytics-error-kpi-state
Parallel-safe: no, error payload semantics must be consistent across core KPI consumers
Owner: Codex
Commit suggestion: `fix(analytics): keep failed KPI payloads unavailable`

### Problem

Several cached analytics failure branches return HTTP 200 bodies containing zero-valued sales or inventory KPIs together with an error meta object. Although the frontend may hide some of these values, the API response itself makes failure indistinguishable from a valid zero to exports, alternate clients, reports or future consumers. An existing failure test codifies this shape instead of asserting that failed numeric evidence is unavailable.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:150-196` returns `TotalRevenue=0`, transaction/unit counts and averages equal to zero for missing relation, timeout and database failure paths while `meta.success=false`.
- `Api.Tests/CachedAnalyticsFailureContractTests.cs:18-30` currently asserts zero KPI values for the inventory balance failure contract; no sales-summary failure test asserts null/unavailable KPI semantics.
- `Api/Endpoints/InventoryEndpoints.cs:66-78` has the same zero-valued error body for the non-cached inventory balance route.
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs:67-83` correctly distinguishes successful empty data from an error, so the new contract must preserve valid empty zeros while changing only failure payload semantics.

### Scope

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` sales summary and cached inventory balance error branches only
- `Api/Endpoints/InventoryEndpoints.cs` inventory balance error branch only
- `Api.Tests/CachedAnalyticsFailureContractTests.cs`
- nearest analytics DTO/serialization tests if nullable KPI fields are required

Do not change successful empty-result semantics, recommendation scoring, trend/forecast/Shopify behavior or unrelated list pagination payloads.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ139`, `RQ145`, the no-fake-zero invariant and `AnalyticsResponseMetaFactory`
- the cached failure and critical endpoint tests listed above

### Do

1. Add failing-first tests that distinguish a valid empty result (`success=true`, explicit empty reason, genuine zero totals) from a failed result (`success=false`, no numeric KPI evidence).
2. Make failed sales/inventory KPI fields nullable or remove them from the failure representation without breaking compatible consumers; do not encode unavailable as zero.
3. Preserve correlation, safe user-facing error text and HTTP compatibility according to the existing endpoint contract.
4. Verify frontend cards, table/detail, export and report consumers do not reinterpret failed nulls as zeros.

### Tests

- Failure tests for missing relation, timeout, database exception and cancellation behavior.
- Empty-success tests for zero sales and zero inventory scope.
- Serialization/parity tests proving error values are unavailable and valid zeros remain valid.
- Focused backend tests, analytics guardrails where frontend handling changes, and `git diff --check`.

### Acceptance

- A failed KPI response cannot be consumed as a valid zero by any client.
- A successful empty scope still exposes genuine zero totals with an explicit empty state.
- Error, empty and partial states remain distinct across cached and direct inventory/sales routes.
- No raw backend error code is used as user-facing copy.

### Dependencies

- `RQ139` remains the shared numeric-state owner; this is the residual error-payload contract.
- `RQ145` remains the cross-surface parity and safe-messaging owner.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ168 - Keep partial cost coverage out of confirmed top-product margin ranking

Status: WAITING
Priority: P0
Type: backend/SQL/tests
Feature family: top-products-margin-coverage
Parallel-safe: no, margin availability, ranking and trust labels share the same cost evidence
Owner: Codex
Commit suggestion: `fix(analytics): expose top-product margin coverage`

### Problem

The advanced top-products SQL marks margin as available when at least one sale line has a positive cost. The `SUM((price-cost) * quantity)` expression then ignores missing-cost rows, so a partially covered product can be labelled `good`, included in margin ranking and described as confirmed without a coverage percentage or limitation.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:3438-3463` sets `margin_impact` to null only when no row has a resolved positive cost; one known-cost row is enough to calculate a partial sum.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:3527-3560` maps any non-null partial sum to `marginQualityTier="good"`, `dataQualityStatus="good"` and `reasonCodes=["margin_available"]`.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:3564-3574` includes every non-null result in `byMarginImpact`, so partial evidence can affect ranking.
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs:174-285` tests already-materialized DTO trust fields, not a product with mixed known and missing sale-line costs.
- `AnalyticsMarginPolicy.BuildPositiveCostSql` and the existing `marginQuality` frontend vocabulary already distinguish confirmed/partial/estimated/no-data states, but this endpoint does not expose that distinction.

### Scope

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` advanced top-products SQL and DTO mapping
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- nearest backend contract tests and shared margin-quality mapping only if required for the additive payload

Do not redesign all sales/margin accounting owned by `RQ148`; do not touch trend, forecast, Shopify or unrelated supplier/pre-nivelacija margin paths.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ148`, `RQ147`, `AnalyticsMarginPolicy` and `src/utils/marginQuality.ts`
- the advanced top-products SQL and tests listed above

### Do

1. Add failing-first fixtures for all costs known, no costs known, mixed cost coverage, valid zero margin and negative/invalid cost.
2. Carry cost-covered revenue/units and a coverage status/percentage from SQL to the DTO; do not infer “good” from a non-null partial sum.
3. Exclude insufficient/partial rows from confirmed margin ranking or label/rank them only according to the explicit backend evidence tier.
4. Preserve valid fully covered zero margin and keep margin impact unavailable when no valid cost exists.
5. Prove dashboard table, margin tab, detail and export/report parity without frontend recalculation.

### Tests

- SQL/DTO tests for 100%, partial, 0% and invalid cost coverage.
- Ranking tests proving partial rows cannot appear as confirmed margin winners.
- Serialization and frontend display tests if the additive coverage fields are consumed.
- Focused backend tests, analytics guardrails, and `git diff --check`.

### Acceptance

- A product with mixed cost evidence is visibly partial/estimated, not confirmed good.
- Margin ranking uses only the evidence tier allowed by the backend contract.
- Fully covered genuine zero margin remains measurable; no-cost margin remains unavailable.
- All consumers show the same coverage and limitation state.

### Dependencies

- `RQ148` remains the broad financial-basis owner.
- `RQ147` remains the metric evidence registry owner.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ169 - Keep empty intake data from receiving a numeric readiness score or green label

Status: WAITING
Priority: P0
Type: backend/tests
Feature family: data-quality-empty-readiness
Parallel-safe: no, intake score, readiness status and report messaging must share one empty-data contract
Owner: Codex
Commit suggestion: `fix(analytics): fail closed on empty intake readiness`

### Problem

The pilot intake report calculates a numeric readiness score even when there are no articles or no import rows. `CalculateIntakeScore` clamps empty denominators to one and `ResolveReadiness` can then classify an empty dataset from the numeric score/freshness alone. This can present a clean or usable readiness label for a dataset with no decision evidence.

### Evidence

- `Api/Endpoints/DataQualityEndpoints.cs:149-153` clamps `totalArticles` and `rowsRead` to one, causing zero-count penalties to remain zero.
- `BuildPilotDataQualityIntakeReportAsync:509-620` passes empty counts into `CalculateIntakeScore` and then resolves readiness instead of forcing an insufficient state.
- `Api/Endpoints/DataQualityEndpoints.cs:622-640` can return a readiness score and label alongside `meta.emptyReason="no_import"`.
- `Api.Tests/AnalyticsDataQualityConsistencyTests.cs:113-143` covers a critical insufficient-signal case but has no zero-article/zero-import readiness regression.

### Scope

- `Api/Endpoints/DataQualityEndpoints.cs`
- `Api.Tests/AnalyticsDataQualityConsistencyTests.cs`
- `Api.Tests/AnalyticsReportsContractTests.cs` only if report serialization/state mapping changes
- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx` only if it currently renders a numeric empty score instead of backend state

Do not change the separate traffic health score contract owned by `RQ144`, refresh worker ownership, trend, forecast or Shopify behavior.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ144`, `RQ147`, the pilot intake report contract and current consistency tests
- the report builder and frontend intake component

### Do

1. Add failing-first cases for zero articles, zero import rows, no import batch, valid populated zero-issue data and populated critical data.
2. Define empty intake as `insufficient_data` with no trusted numeric readiness score, or an explicit non-decision sentinel that existing DTOs can represent without looking green.
3. Keep valid populated data with zero detected issues measurable and distinguish it from no evidence.
4. Ensure readiness, recommendation-block count, health score and report/export messaging do not silently substitute zeros for missing evidence.

### Tests

- Unit tests for empty, valid zero-issue, partial and critical intake states.
- Backend/report serialization tests for score/status/meta parity.
- Frontend report tests if rendering changes, plus analytics guardrails and `git diff --check`.

### Acceptance

- No articles/import evidence cannot produce a green or decision-ready readiness score.
- Populated data with genuinely zero issues remains a valid zero-risk result.
- Health, intake report, UI and export distinguish empty, insufficient, partial and error states.

### Dependencies

- `RQ144` remains the health denominator owner.
- `RQ147` remains the evidence-tier owner.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ170 - Reject invalid pilot-intake report periods instead of silently swapping or defaulting them

Status: WAITING
Priority: P1
Type: backend/contract/tests
Feature family: data-quality-report-period-state
Parallel-safe: no, report period, query URL and exported metadata must remain identical
Owner: Codex
Commit suggestion: `fix(analytics): validate pilot intake report periods`

### Problem

The pilot intake report period resolver silently swaps reversed dates and silently substitutes the default 30-day period for invalid date text. The response can therefore be internally consistent while describing a period the user did not request.

### Evidence

- `Api/Endpoints/DataQualityEndpoints.cs:1533-1545` swaps `fromUtc` and `toUtc` when reversed instead of returning an invalid-period state.
- `Api/Endpoints/DataQualityEndpoints.cs:1547-1559` returns null for invalid date text, which makes the caller fall back to the default period.
- `Api/Endpoints/DataQualityEndpoints.cs:644-660` emits the resolved period and readiness metadata as if it were the requested report scope.
- `Api.Tests/AnalyticsReportsContractTests.cs` covers report shape, but no test asserts reversed/invalid period rejection or requested/effective period distinction.

### Scope

- `Api/Endpoints/DataQualityEndpoints.cs` pilot-intake period parsing and response metadata
- `Api.Tests/AnalyticsReportsContractTests.cs`
- `Klijent/clientapp/src/pages/PilotIntakeReportPage.tsx` only if the page must render the explicit invalid-period state

Do not duplicate `RQ166` action-timeline validation or change valid default-period behavior when parameters are genuinely absent; do not touch trend, forecast or Shopify.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ137`, `RQ145`, `RQ166` and the pilot intake report contract
- the resolver, report endpoint and current report tests

### Do

1. Add failing-first tests for absent dates, valid equal/range dates, reversed dates, invalid dates and non-finite/ambiguous input.
2. Preserve the default period only when both dates are absent; reject or explicitly mark invalid user-supplied dates without querying a substituted period.
3. Preserve requested, effective and observed period truth in report DTO, export and stable query URL.
4. Keep empty valid periods distinct from invalid request and backend error states with safe user-facing messages.

### Tests

- Focused backend report tests for all period cases and metadata parity.
- Export/query-link tests if those projections use the report period.
- Frontend report state tests if rendering changes, plus analytics guardrails and `git diff --check`.

### Acceptance

- Reversed or invalid user-supplied dates cannot silently produce a report for another period.
- Absent dates still use the documented default period.
- Report, export, stable link and UI display the same requested/effective/observed period state.

### Dependencies

- `RQ137` remains the shared period-lineage owner.
- `RQ145` remains the parity and safe-messaging owner.
- Keep this prompt `WAITING` behind the single `READY` item.
- Keep this prompt `WAITING` behind the single `READY` item.

---

## RQ176 - Keep inventory snapshot query time separate from source freshness

Status: WAITING
Priority: P1
Type: backend/contract/frontend/tests
Feature family: inventory-snapshot-freshness-provenance
Parallel-safe: no, freshness and refresh ownership must have one inventory signal contract
Owner: Codex
Commit suggestion: `fix(inventory): expose snapshot freshness provenance`

### Problem

Inventory alerts, rebalance and size-curve responses expose `GeneratedAtUtc`, but the handlers set it to `DateTime.UtcNow` while reading the snapshot. `InventoryPage` then compares those query timestamps as secondary-panel freshness. A successful query or cache hit must not look like a successful source refresh.

### Evidence

- `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs:90`, `GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs:94` and `GetInventorySizeCurve/GetInventorySizeCurveHandler.cs:103` set `GeneratedAtUtc` to the request time.
- `Klijent/clientapp/src/pages/InventoryPage.tsx:667-675` collects those values as `secondaryPanelTimestamps`, and `:714-725` presents them as panel freshness.
- `InventoryAlertListDto`, `RebalanceSuggestionListDto` and `InventorySizeCurveListDto` do not expose source snapshot freshness or last successful refresh.
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md` explicitly distinguishes response generation from snapshot freshness; this prompt applies the same rule to non-forecast inventory signals.

### Scope

- The three inventory snapshot handlers, DTOs/types and cached routes for alerts, rebalance and size curve.
- `InventoryPage` secondary-panel freshness display and the nearest cache/refresh metadata source.
- Existing snapshot relation/migration/view evidence only as needed to prove or expose source freshness; no new forecasting or Shopify work.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `RQ141`, `RQ145`, `RQ146`, `RQ151`
- `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- the three inventory snapshot handlers, `InventoryPage.tsx`, `InventorySnapshotContractTests.cs` and `docs/analytics/ANALYTICS_CACHE_POLICY.md`

### Do

1. Add a failing-first contract proving `GeneratedAtUtc` is response generation only and cannot be used as source freshness.
2. Expose snapshot freshness/last-successful-refresh and freshness status from a proven source, or return `unknown` when source lineage is unavailable.
3. Keep successful empty, missing relation, stale and partial states distinct and visible.
4. Make the page compare primary refresh only with proven secondary source freshness; never use current query time as a substitute.
5. Keep cache hit, failed refresh and missing-refresh-history semantics explicit.

### Tests

- Backend contract tests for known snapshot timestamp, unknown timestamp, stale, missing relation, partial and successful empty results.
- Frontend tests proving query time is not rendered as last refresh and that unknown freshness is visible.
- Cache/refresh test proving a cache read does not advance last successful refresh and a failed refresh does not publish a new timestamp.
- `dotnet test` focused inventory snapshot tests, affected frontend tests, analytics guardrails, changed-project builds and `git diff --check`.

### Acceptance

- Inventory signal panels never label response generation time as source freshness or last successful refresh.
- Known, stale, unknown, partial, missing and empty states remain distinct in DTO, page and trust messaging.
- No forecast, Shopify or external connector scope is pulled into this prompt.

### Dependencies

- `RQ141` remains the broad lineage owner; `RQ146` owns full runtime schema/refresh proof.
- `RQ64`-`RQ71` and `RQ99` remain completed null/count/reader foundations.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ177 - Preserve missing, empty and partial size-curve states in the panel

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: size-curve-empty-error-state
Parallel-safe: yes, bounded to size-curve panel projection
Owner: Codex
Commit suggestion: `fix(inventory): preserve size-curve empty and unavailable states`

### Problem

The size-curve panel uses the same branch and copy for a missing snapshot relation and a successful empty result. It also drops the backend warning. This makes a schema/source failure look like a valid SKU with no data and hides degraded evidence.

### Evidence

- `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs:102-110` returns successful empty with `SnapshotAvailable=true` and an empty warning.
- `:117-125` returns missing relation with `SnapshotAvailable=false` and a missing-snapshot warning.
- `Klijent/clientapp/src/components/inventory/SizeCurvePanel.tsx:60-63` combines `!snapshotAvailable` and `items.length === 0`, renders identical text and does not render `sizeCurve.warning`.
- `Api.Tests/InventorySnapshotContractTests.cs` covers the backend states, but no panel test protects their user-facing distinction.

### Scope

- `SizeCurvePanel.tsx` and its focused frontend spec.
- Types/API adapter only if needed to preserve warning/state fields.
- No size-curve formula, SQL materializer or chart redesign.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ139`, `RQ144`, `RQ145`, `RQ151`
- `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- `SizeCurvePanel.tsx`, `SizeCurveVisualization.tsx`, `InventorySnapshotContractTests.cs` and existing inventory null-evidence tests

### Do

1. Add failing-first tests for missing relation, successful empty, partial warning and populated complete data.
2. Render distinct Serbian copy for unavailable source, valid empty result and partial/missing evidence.
3. Preserve warning text through the panel without exposing raw backend codes.
4. Keep true zero share and missing share distinct in cards, chart tooltips and detail view.

### Tests

- Focused `SizeCurvePanel` tests for all four states.
- Chart tests for valid zero, null, NaN/Infinity and initial width/height `0`/`-1`.
- Dark/light/soft-gray DOM/theme assertions and no console warning/error assertions.
- Analytics guardrails, focused frontend test and `git diff --check`.

### Acceptance

- Missing relation is not shown as a normal empty SKU result.
- Successful empty remains an empty state, not an error.
- Partial/missing evidence and backend warning are visible in user-safe language.
- Valid zero remains visible as zero; missing remains unavailable in every size-curve projection.

### Dependencies

- `RQ64`/`RQ71` own backend null and boolean evidence semantics.
- `RQ145` owns broader parity; this prompt only repairs the size-curve panel state projection.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ178 - Add backend-owned actionability and safe copy to inventory signal snapshots

Status: WAITING
Priority: P1
Type: backend/contract/frontend/tests
Feature family: inventory-snapshot-safe-actionability
Parallel-safe: no, snapshot rows need one actionability and reason vocabulary
Owner: Codex
Commit suggestion: `fix(inventory): harden snapshot signal actionability`

### Problem

Inventory alerts and rebalance rows preserve nullable evidence, but their DTOs do not carry a shared backend-owned `recommendationAllowed`/status contract. The UI still renders confidence slots and raw signal/reason strings. An incomplete suggestion can therefore look operationally actionable, and internal codes can reach users.

### Evidence

- `InventoryAlertsFeed.tsx:77` renders a confidence slot even when `confidenceScore` is null, and `:83` renders raw `alertType`.
- `RebalancingTable.tsx:98` renders `item.reason` directly; `RebalanceSuggestionDto` has no actionability/status field.
- `InventoryAlertListDto`, `RebalanceSuggestionListDto` and `InventorySizeCurveListDto` only expose list warning text, not a shared row-level evidence/actionability contract.
- Backend handlers correctly preserve null evidence and emit warnings, so the remaining defect is the boundary between preserved evidence and user-facing actionability/copy.

### Scope

- Alert and rebalance DTOs, handlers and their inventory panels; size-curve reason-code mapping only if it shares the same contract.
- Backend-safe reason/status labels and `recommendationAllowed` semantics for snapshot rows.
- Tests for no action/confidence display when evidence is insufficient; no formula rewrite or forecast/Shopify work.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ143`, `RQ145`, `RQ147`, `RQ151`
- `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- snapshot DTOs/handlers, `InventoryAlertsFeed.tsx`, `RebalancingTable.tsx`, inventory action tests and `InventorySnapshotContractTests.cs`

### Do

1. Add failing-first fixtures for complete actionable signal, null confidence, null expected impact, unknown reason, stale/partial warning and true zero impact.
2. Define one backend-owned row state: status, recommendationAllowed, data quality and safe reason/label.
3. Do not display confidence/reliability or executable/recommendation action when the backend denies actionability or evidence is unavailable.
4. Map raw alert/reason codes to clear Serbian copy; keep technical codes only in an audit/debug channel.
5. Reuse the same row payload in panel, detail, export and report where those consumers exist.

### Tests

- Backend DTO/handler tests for true zero versus null, stale/partial/fallback, unknown code and recommendationAllowed=false.
- Frontend tests for no confidence slot/action on blocked rows, safe Serbian copy, empty/error distinction and parity with detail/export.
- Theme tests for dark/light/soft-gray and no console warning/error assertions.
- Focused backend/frontend tests, analytics guardrails, affected builds and `git diff --check`.

### Acceptance

- Backend owns row actionability, status, confidence eligibility and reason; frontend only presents them.
- Blocked or incomplete snapshot rows cannot show a recommendation action or numeric confidence.
- No raw backend code is user-facing.
- Valid zero values remain valid zeros, while missing/unknown values stay unavailable.

### Dependencies

- `RQ143`/`RQ145` remain broad owners; this prompt is the bounded inventory snapshot slice.
- `RQ64`-`RQ71` remain the completed nullable evidence foundation.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ179 - Do not mark supplier footwear data fresh from response generated time

Status: WAITING
Priority: P1
Type: frontend/contract/tests
Feature family: supplier-footwear-freshness-state
Parallel-safe: yes, bounded to supplier footwear freshness projection
Owner: Codex
Commit suggestion: `fix(analytics): fail closed on supplier footwear freshness`

### Problem

`SupplierFootwearAnalyticsPage` marks the screen `fresh` whenever `data.generatedAt` exists and no warning flag exists. That field proves a response was generated, not that the source data was refreshed successfully. The page can therefore show a fresh signal while `lastRefreshAtUtc` is unknown.

### Evidence

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:575-576` uses `data.generatedAt ? "fresh" : "unknown"` as a fallback.
- `:655-656` repeats the same logic in the visible trust header.
- `:575` separately passes `data.meta?.lastRefreshAtUtc`, so the freshness badge and last-refresh value can contradict each other.
- The response is loaded through `vendorSalesNivelacijaApi.ts:207-221`; `generatedAt` belongs to the response payload, not a proven refresh-run record.

### Scope

- `SupplierFootwearAnalyticsPage.tsx`, vendor pre/post response metadata adapter and focused page tests.
- Only freshness/refresh lineage and safe UI state; no trend, forecast, Shopify or pre/post formula changes.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ137`, `RQ140`, `RQ141`, `RQ145`
- `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- `SupplierFootwearAnalyticsPage.tsx`, `vendorSalesNivelacijaApi.ts`, pre/post metadata types and nearest specs

### Do

1. Add failing-first tests for known refresh, missing refresh, stale/partial response, fallback and response-only generated time.
2. Use only proven `lastRefreshAtUtc`/freshness metadata for a `fresh` label; otherwise show `unknown` or the backend-declared degraded state.
3. Keep `generatedAt` available as generation metadata but never use it as last refresh or freshness proof.
4. Ensure embedded and standalone trust headers use the same state.

### Tests

- Focused page tests for fresh-with-refresh, generated-only-unknown, stale, partial, fallback and empty states.
- Metadata parity test for header, table/detail snapshot and export/report if they consume the same payload.
- Analytics guardrails, focused frontend test, build and `git diff --check`.

### Acceptance

- Supplier footwear cannot appear fresh solely because the HTTP response has a generated timestamp.
- Missing refresh history is visibly unknown, not current time and not fresh.
- Standalone and embedded surfaces agree on freshness and limitation state.

### Dependencies

- `RQ141` remains the broad lineage owner and `RQ140` the pre/post comparability owner.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ180 - Remove frontend reconstruction of backend-owned pre/post aggregate denominators

Status: WAITING
Priority: P1
Type: frontend/contract/tests
Feature family: pre-post-aggregate-owner-parity
Parallel-safe: no, pre/post aggregate denominator must have one owner
Owner: Codex
Commit suggestion: `fix(analytics): preserve pre-post aggregate owner parity`

### Problem

The pre/post vendor page uses a frontend fallback sum of absolute row changes when the backend total is unavailable, then uses that reconstructed value for shares, concentration cards, charts and export/table projections. This creates a second owner for a backend-owned aggregate and can diverge when rows are partial, filtered or not comparable.

### Evidence

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx:630-634` computes `rows.reduce((sum, item) => sum + Math.abs(item.changeRevenue), 0)` as a fallback.
- `:646-649` derives row share from that frontend denominator.
- The same denominator drives concentration/top-five calculations around `:733-745`, `:847-894` and `:942-963`, plus the table/export value around `:151-155`.
- `Api/Endpoints/AllEndpoints.cs` already builds `VendorSalesNivelacijaTotalsDto.AbsoluteChangeRevenue` and vendor-level absolute values.

### Scope

- `ProdajaPrePostNivelacijePage.tsx`, pre/post trust adapter and affected focused specs.
- Table/chart/detail/export parity for the absolute-change aggregate and share.
- No change to causal comparability, coverage formula, backend recommendation score, forecast, trend or Shopify behavior.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ140`, `RQ143`, `RQ145`, `RQ156`
- `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- `ProdajaPrePostNivelacijePage.tsx`, `vendorSalesNivelacijaApi.ts`, `VendorSalesNivelacijaModels.cs`, `AllEndpoints.cs` and existing pre/post specs

### Do

1. Add failing-first parity fixtures where backend aggregate differs from a naive row sum because of partial/non-comparable rows, filters or rounding.
2. Make the backend aggregate and row shares the single source of truth; if the authoritative field is absent, render unavailable rather than recompute business arithmetic in React.
3. Keep valid zero aggregate distinct from missing aggregate.
4. Prove the same state/value in KPI cards, table, chart, detail snapshot, export and report.
5. Keep frontend sorting/display-only behavior separate from business aggregation.

### Tests

- Frontend tests for valid zero, null/missing aggregate, partial/non-comparable rows, rounding mismatch and non-finite values.
- Table/chart/detail/export/report parity tests proving no consumer reintroduces the fallback sum.
- Backend contract assertion that the authoritative aggregate is present or explicitly unavailable.
- Analytics guardrails, focused frontend/backend tests, affected builds and `git diff --check`.

### Acceptance

- React never reconstructs the backend-owned absolute-change denominator or recommendation aggregate.
- Missing backend aggregate is visible as unavailable, not a plausible locally recomputed number.
- All pre/post consumers use one value/state and preserve valid zero.

### Dependencies

- `RQ140` owns comparable cohort and causal semantics; `RQ156` owns coverage unknown/zero; `RQ143` owns backend decision ownership; `RQ145` owns parity.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ171 - Add GMROI metric roadmap and prevent premature UI exposure

Status: OBSOLETE
Priority: P2
Type: frontend/contract/tests
Feature family: analytics-metric-roadmap
Parallel-safe: yes
Owner: Analytics

Obsolete reason: GMROI is a future metric roadmap item, not an existing analytics screen/function in the requested scope.

Commit suggestion: `fix(analytics): add GMROI roadmap guard and test coverage`

### Problem

Repository contains a frontend TODO to add GMROI when the backend exposes a stable metric and DTO fields (`Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`). If the UI surfaces GMROI before backend contract stabilizes, visualizations or exports may show misleading or incomplete KPI values.

### Evidence

- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts:497` contains a `TODO(analytics-methodology): Dodati GMROI` marker.
- No active prompt currently covers GMROI introduction or a backend DTO contract rollout for GMROI in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

### Scope

- Frontend metric definitions and UI rendering guards (`Klijent/.../analyticsMetricDefinitions.ts`, `Klijent/clientapp/src/pages/*`).
- Backend DTO contract for KPI fields (if/when proposed).
- Tests for metric presence and graceful absence handling.

### Do

1. Add a guarded metric registration in `analyticsMetricDefinitions.ts` that only enables GMROI when the backend DTO explicitly exposes `gmroi` and `gmroi_basis` fields.
2. Add focused unit and integration tests that confirm UI renders no GMROI card/column when backend DTO lacks fields, and renders correct formatted GMROI when DTO present.
3. Draft a short acceptance checklist for backend owners to add the DTO fields before enabling the metric globally.

### Tests

- Frontend unit tests for metric definitions.
- API contract test or mock that toggles `gmroi` fields.

### Acceptance

- UI must not display GMROI unless backend DTO provides explicit fields.
- Tests must fail when UI attempts to render GMROI without DTO support.

### Dependencies

- Coordination with backend DTO owner and `REQ`/analytics roadmap owners.

---

## RQ172 - Hardening embedding parameterization and DB binding for similarity queries

Status: WAITING
Priority: P1
Type: backend/security/tests
Feature family: embedding-service-hardening
Parallel-safe: no
Owner: Platform

Commit suggestion: `fix(embeddings): use proper pgvector param binding and avoid NpgsqlDbType.Unknown`

### Problem

Embedding code currently serializes embedding arrays into a string and binds them as `NpgsqlDbType.Unknown` when executing similarity queries (`Infrastructure/Services/EmbeddingService.cs`, `Api/Endpoints/AllEndpoints.cs`). This can cause runtime DB parameter type errors, parsing edge-cases, and injection/formatting issues across Postgres/vector operators. Mock/random embeddings are also used in some runtimes (`MockEmbeddingService`) which, if mis-configured, may leak into production.

### Evidence

- `Infrastructure/Services/EmbeddingService.cs` creates `embeddingStr = "[" + string.Join(",", embedding) + "]"` and binds with `NpgsqlDbType.Unknown`.
- `Api/Endpoints/AllEndpoints.cs` contains a `TODO: Generate embedding vector when Python service is deployed`.
- GenAI/embedding readiness prompts note mock service and quarantine work, but this specific DB-parameterization risk is not separately queued in analytics reliability prompts.

### Scope

- `Infrastructure/Services/EmbeddingService.cs`
- `Api/Endpoints/AllEndpoints.cs` and any code paths that pass embedding parameters into SQL queries
- Integration test that exercises similarity queries using pgvector and parameter binding

### Do

1. Replace ad-hoc string serialization with proper pgvector/JSONB binding using typed parameters or `NpgsqlDbType.Array`/custom mapping for vector types, or use `pgvector` parameterization utilities.
2. Add integration tests that run the similarity SQL against a test Postgres (pgvector enabled) to assert query success and correct parameter typing.
3. Confirm mock embedding service cannot be selected in production (validate runtime config and startup guards).

### Tests

- Integration test: generate a deterministic embedding, bind it as a typed parameter, run similarity query, assert rows and no DB type errors.
- Startup test: ensure production config rejects `UseMock=true` or fails closed.

### Acceptance

- Similarity queries succeed without parameter type warnings or runtime failures locally and in CI integration (pgvector-enabled test).
- Production startup does not accidentally use mock embeddings.

### Dependencies

- Coordination with DB/infra team for pgvector parameterization guidance.

---

## RQ181 - Do not expose an executable action CTA for blocked Decision Board cards

Status: WAITING
Priority: P0
Type: frontend/contract/tests
Feature family: decision-board-blocked-action-cta
Parallel-safe: no, Decision Board actionability presentation has one owner
Owner: Codex
Commit suggestion: `fix(analytics): hide blocked decision board action cta`

### Problem

The Executive Decision Board renders the primary action link for every card. A card with `recommendationAllowed=false` and no existing workflow action still receives the generic `Dodaj u akcije` label from the frontend action-state mapper. This makes a blocked or insufficient signal look executable even though the backend has explicitly denied recommendation actionability.

### Evidence

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:257-260` returns `Dodaj u akcije` for the `none` action state without considering `recommendationAllowed`.
- `:1109-1125` maps the backend `recommendationAllowed` field but still assigns the unconditional action CTA.
- `:1271-1274` always renders the primary `Link` to `card.actionHref`, including for blocked cards.
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx` already exercises blocked inventory cards and action links, but does not assert that a blocked card cannot render an executable action CTA.
- `Api/Dtos/DecisionBoardDtos.cs:40-67` and `Api/Endpoints/DecisionBoardEndpoints.cs` already carry backend-owned `RecommendationAllowed`; this prompt must not recreate decision scoring.
- Recent history (`41790622`, `08abe2bf`, `785b88b8`, `df538b3b`) hardened trust/confidence gating but did not prove primary CTA suppression in the active card renderer.

### Scope

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx`
- `Api/Dtos/DecisionBoardDtos.cs` and `Api/Endpoints/DecisionBoardEndpoints.cs` only if a backward-compatible actionability/label field is required to preserve one contract
- Existing shared safe-copy mapping only if needed for the blocked-card explanation

Do not change backend ranking, score, confidence calculation, action lifecycle, or the broad RQ143 ownership contract. Do not remove the safe `Otvori izvor`/data-quality review navigation.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ143`, `RQ145` and the current Decision Board trust contract
- `ExecutiveDecisionBoardPage.tsx`, `ExecutiveDecisionBoardPage.spec.tsx`, `DecisionBoardDtos.cs` and `DecisionBoardEndpoints.cs`

### Do

1. Add a failing-first DOM regression fixture with `recommendationAllowed=false`, no existing open/closed action and an action route in the payload.
2. Fail closed for `false`, `null` and omitted `recommendationAllowed`; do not infer permission from confidence, impact, status, or the existence of `actionHref`.
3. Hide the executable action CTA or replace it with an explicitly non-executable review/data-quality affordance. Keep source navigation and the blocked reason visible.
4. Preserve the existing allowed-card behavior and existing-action labels for cards whose backend state permits them.
5. Verify that card details, source navigation, export/report links and bulk/action surfaces do not reintroduce the blocked action label on this Decision Board path.

### Tests

- Failing-first focused page tests for `recommendationAllowed=false`, `null`, omitted, `true`, open action and closed action.
- Assert blocked cards have no link/button named `Dodaj u akcije`, while the safe source/data-quality link remains available.
- Assert no raw backend reason code is rendered and blocked state remains visible in light/dark/soft-gray themes if the existing test harness supports theme variants.
- Run the nearest frontend specs, `npm run check:analytics-guardrails`, affected frontend build/typecheck, `git diff --check` and queue/planning validators.

### Acceptance

- A blocked, unknown or insufficient Decision Board card never exposes an executable `Dodaj u akcije` affordance.
- An allowed card preserves its backend-owned actionability and workflow state.
- The frontend does not recalculate score, confidence, recommendation status or permission.
- Users can still open the source and understand why the action is unavailable.

### Dependencies

- `RQ143` remains the broad backend decision/actionability owner.
- `RQ145` remains the complete cross-surface parity and safe-messaging owner.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ182 - Preserve unknown pre/post coverage in backend DTOs and aggregate calculations

Status: WAITING
Priority: P0
Type: backend/contract/frontend/tests
Feature family: pre-post-coverage-backend-null-state
Parallel-safe: no, pre/post coverage state has one backend contract owner
Owner: Codex
Commit suggestion: `fix(analytics): preserve unknown pre-post coverage state`

### Problem

The authoritative pre/post SQL views return `NULL` coverage when a window has no observed sales days, but the vendor-level endpoint converts `coveragePre30` and `coveragePost30` to `0` before creating article DTOs. It then calculates aggregate averages over those fallback zeros and serializes non-nullable coverage fields. Missing observation can therefore appear as measured zero coverage in vendor, data-quality, detail, table, chart and report consumers.

### Evidence

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:101-103` and `:160-162` intentionally return `NULL` when `COUNT(DISTINCT s.day) = 0`.
- `Api/Endpoints/AllEndpoints.cs:3558-3559` reads nullable evidence, but `:3577-3578` assigns `coveragePre30Evidence ?? 0m` and `coveragePost30Evidence ?? 0m`.
- `:3720-3721` computes `avgCoveragePre30` and `avgCoveragePost30` as ordinary averages over those fallback-filled values, and `:3722` counts missing coverage as low coverage.
- `Api/Models/VendorSalesNivelacijaModels.cs:25-26,80-81,107-108` declares affected coverage fields as non-nullable, while `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts:32-33,101-102,115-116` has an inconsistent partial nullable contract.
- Existing `SupplierDecisionSchemaSqlTests` prove missing sales-window/baseline semantics, but no test proves that missing coverage remains `null` through endpoint serialization and aggregate calculations.
- Git history (`570a31e8`, `a84d8a42`, `29a5943a`) hardened comparability and trust gating but left the coverage reader/DTO coalescing path unchanged.

### Scope

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql` only if the explicit null/valid-zero source contract needs clarification
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Models/VendorSalesNivelacijaModels.cs`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts` and the nearest pre/post adapters/pages required for type and parity correctness
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs` plus focused endpoint/contract tests and nearest pre/post frontend specs

Do not change the causal/comparability formula, recommendation score, confidence calibration, forecast/trend logic or the separate frontend-only RQ156 display owner.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ140`, `RQ145`, `RQ156` and the pre/post contract notes
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `AllEndpoints.cs`, `VendorSalesNivelacijaModels.cs`, `vendorSalesNivelacijaApi.ts` and the existing schema/contract tests

### Do

1. Add failing-first tests for no observed days (`NULL`), explicit finite zero from an authoritative source, positive coverage, mixed known/unknown rows, empty analyzed sets and non-finite client payloads where the JSON boundary permits them.
2. Preserve unknown coverage as nullable/unavailable from SQL reader through article, vendor, totals and data-quality DTOs. Do not use `0` as a missing-value sentinel.
3. Compute coverage averages and low-coverage counts only from known finite coverage values; if no value is known, return an explicit unavailable state rather than `0%`.
4. Keep true finite zero distinguishable from missing/unknown. Do not manufacture a true zero merely because no observation exists.
5. Align TypeScript DTOs and all table/chart/detail/export/report adapters with the backend nullability and ensure recommendation/actionability remains blocked when required evidence is unavailable.

### Tests

- Backend SQL contract test for null-on-no-observation and valid finite-zero preservation.
- Focused endpoint/DTO tests for null, true zero, positive, mixed and empty coverage states, including aggregate average/count behavior.
- Frontend adapter/page parity tests proving the same unknown/zero state in KPI, table, chart, detail, export and report projections.
- Non-finite guard tests where the boundary can receive `NaN`/`Infinity`; invalid values must become unavailable, never zero.
- Run focused backend/frontend tests, `npm run check:analytics-guardrails`, affected builds, `git diff --check` and queue/planning validators.

### Acceptance

- Missing pre/post observation never serializes as a valid `0` coverage value.
- A true finite zero remains valid and visibly distinct from unavailable coverage.
- Aggregate coverage averages/counts exclude unknown values and become unavailable when there is no known basis.
- Backend recommendation/actionability remains conservative when coverage evidence is missing.
- All consumers preserve the same null/zero/positive state without frontend recomputation or silent fallback.

### Dependencies

- `RQ140` owns comparable cohort and causal semantics.
- `RQ156` owns the bounded frontend branch/message repair for supplier/category pre/post screens.
- `RQ145` owns complete table/chart/detail/export/report parity.
- `RQ146` and `STAB16` retain runtime schema, migration, refresh and deployed proof.
- Keep this prompt `WAITING` while `RQ154` is the sole `READY` item.

---

## RQ183 - Journal-derived opening stock for sell-through denominator integrity

Status: WAITING
Priority: P1
Type: backend/contract/tests
Feature family: inventory-opening-stock-proof
Parallel-safe: no
Owner: Analytics

Commit suggestion: `fix(analytics): prove opening stock derivation or reject stale inventory assumptions`

### Problem

Cached inventory list and Product Decision Center infer `openingStockUnits = currentStock - sum(all DnevnikPromena quantities)` and feed that into sell-through/stock-cover calculations. If journal movements are incomplete, out-of-order, or fail to record, a plausible sell-through ratio is produced from an unreliable denominator.

### Evidence

- `CachedAnalyticsEndpoints.cs:629`, `:5785`, `:7604-7659` perform the reconstruction.
- No test asserts that all valid movements exist before using the derived opening stock.
- No contract exposes the derivation method, calculation uncertainty or required journal completeness.

### Scope

- Backend: journal movement completeness proof and opening-stock derivation confidence
- Cached inventory DTO: add `openingStockConfidence` or `isOpeningStockDerived` flag
- Tests for missing/partial journal, out-of-order entries and concurrent writes

### Do

1. Define journal completeness requirements (date range, movement type coverage).
2. Add a backend check that fails or marks the opening-stock as unavailable when journal is incomplete or uncertain.
3. Expose `isOpeningStockDerived` and `confidence` in the DTO.
4. Add tests for incomplete journals and concurrent write scenarios.

### Tests

- Backend: incomplete journal → `isOpeningStockDerived=false` or unavailable state.
- Integration: concurrent journal writes and inventory-list query; assert derivation method is consistent.

### Acceptance

- Opening stock is never silently derived from an unverified journal.
- Uncertainty is visible in sell-through confidence and recommendation state.

### Dependencies

- `RQ141` owns lineage; this is bounded to opening-stock proof.

---

## RQ184 - Fixed 30-day divisor for inventory velocity miscalculation

Status: WAITING
Priority: P1
Type: backend/tests
Feature family: velocity-divisor-accuracy
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(analytics): use actual elapsed days or active selling span for velocity divisor`

### Problem

Sales are counted over `UtcNow.AddDays(-30)`, but `avgDailySalesUnits` always divides by `30m`, not actual elapsed days or observed selling span. This understates velocity for sparse sellers and overstates days-of-cover, skewing replenishment/slow-stock signals.

### Evidence

- `CachedAnalyticsEndpoints.cs:596`, `:631` hardcode `30m` divisor.
- No test validates velocity against actual day count or selling span.

### Scope

- `CachedAnalyticsEndpoints.cs` inventory-list velocity calculation
- Focused backend tests

### Do

1. Replace fixed `30m` with actual elapsed days between window start and `UtcNow`.
2. Add tests comparing expected velocity with a known sales-per-day input.
3. Document and enforce window definitions (end = `UtcNow`, start = 30 days prior).

### Tests

- Unit: 30-day window, 10 units total → expect 10/30 ≈ 0.33 units/day.
- Unit: 10-day elapsed window, 10 units → expect 10/10 = 1.0 units/day.

### Acceptance

- Velocity divisor matches actual elapsed days in the query window.
- Stock-cover and replenishment signals reflect correct daily run-rate.

---

## RQ185 - "Velocity per day" label with active-selling-days divisor confusion

Status: WAITING
Priority: P1
Type: backend/frontend/contract/tests
Feature family: velocity-active-days-semantics
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(analytics): clarify velocity metric definition: calendar days vs active selling days`

### Problem

Velocity is computed as `units / COUNT(active sale days)` or `units / active_days`, while DTOs/labels expose `velocity_units_per_day`. Intermittent sellers look much faster than they are; rankings, stock comparisons, and dashboard insights overstate daily run-rate.

### Evidence

- `CachedAnalyticsEndpoints.cs:3202` (quick insights), `:3497` (top products advanced SQL).
- Label says "per day" but denominator is active selling days, not calendar days.

### Scope

- Backend: velocity calculation method and active-day counting
- DTO fields: clarify semantics or use separate fields for active-days-based vs calendar-based metrics
- Frontend labels and dashboard context

### Do

1. Choose one definition: either active-selling-days velocity or calendar-day velocity.
2. Update DTOs and labels to clarify which metric is being returned (e.g., `velocityUnitsPerActiveSalesDay` vs `velocityUnitsPerCalendarDay`).
3. Add tests validating the definition against known input (e.g., item sold on 5 out of 30 days, 100 units total → either 100/5 or 100/30).

### Tests

- Backend: verify active-day count logic and velocity calculation.
- Frontend: mock both metrics and assert label matches calculation method.

### Acceptance

- Velocity metric definition is unambiguous and matches label.
- Intermittent and continuous sellers are ranked with correct run-rate understanding.

---

## RQ186 - Product Decision lost-sales formula ignores velocity (static stock-gap risk)

Status: WAITING
Priority: P1
Type: backend/contract/tests
Feature family: pdc-lost-sales-arithmetic
Parallel-safe: no
Owner: Analytics

Commit suggestion: `fix(pdc): lost-sales estimate must incorporate velocity or mark unavailable`

### Problem

`lostSalesEstimate` is `stockGap * avgUnitPrice` without incorporating velocity/demand. Fast movers with the same stock gap get the same "lost sales" RSD as slow movers, misranking opportunity and inflating portfolio totals.

### Evidence

- `CachedAnalyticsEndpoints.cs:5712-5715`, `:5808`.
- Formula lacks a time/velocity factor.

### Scope

- PDC lost-sales calculation and risk confidence
- Backend DTO: clarify lost-sales semantics or expand formula
- Tests for velocity impact

### Do

1. Incorporate `velocityUnitsPerDay` into lost-sales: e.g., `lostSalesEstimate = stockGap * avgUnitPrice * daysInWindow / (window.Days or 30)`.
2. Or mark lost-sales unavailable when velocity is missing/unreliable.
3. Add tests proving fast movers have higher estimated loss than slow movers with same stock gap.

### Tests

- Unit: same stock gap, different velocities → different loss estimates.
- Backend: lost-sales ranking changes when velocity is added to formula.

### Acceptance

- Lost-sales RSD reflects both stock gap and demand velocity.
- Fast movers are correctly ranked higher-opportunity than slow movers.

### Dependencies

- `RQ03` owns lost-sales unavailability validation; this is the arithmetic formula.

---

## RQ187 - Cache write time published as LastRefreshAtUtc on cache hits

Status: WAITING
Priority: P1
Type: backend/frontend/contract/tests
Feature family: cache-meta-freshness-truth
Parallel-safe: no
Owner: Analytics

Commit suggestion: `fix(analytics): distinguish cache creation time from data refresh timestamp`

### Problem

`ApplyStaleCacheWarning` always sets `meta.LastRefreshAtUtc = metadata.CreatedAtUtc` (cache entry creation), even on fresh cache hits. Dashboard/PDC can show a believable "last refresh" that is only cache population time, masking stale underlying data.

### Evidence

- `CachedAnalyticsEndpoints.cs:2634-2656`; callers at `:137`, `:308`, `:1526`, `:2110`.
- No distinction between "cache populated" and "underlying data refreshed."

### Scope

- `AnalyticsCachePolicy` and cache wrapper
- All cached analytics endpoints
- Cache metadata contract (DTO)

### Do

1. Add a separate `data.RefreshAtUtc` field that tracks the actual data source refresh timestamp (from worker/migration, not cache write).
2. Use `meta.CacheCreatedAtUtc` for cache diagnostics only.
3. Return `data.RefreshAtUtc` as the authoritative freshness signal to clients.
4. Add tests proving old-data cache hits do not update the refresh timestamp.

### Tests

- Unit: cache hit on 2-hour-old data → assert `RefreshAtUtc` is 2 hours old, not `now`.
- Integration: refresh worker populates cache; assert both timestamps are tracked separately.

### Acceptance

- Dashboard/PDC freshness display uses true data refresh time, not cache write time.
- Cache hits do not reset the refresh timestamp.

### Dependencies

- `RQ141` owns broad lineage; this is cache metadata semantics.

---

## RQ188 - Price-intelligence discount depth encodes missing list price as 0%

Status: WAITING
Priority: P2
Type: backend/tests
Feature family: price-intelligence-validity
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(price): reject invalid list price and mark discount depth unavailable`

### Problem

SQL view encodes `WHEN pp.list_price <= 0 THEN 0::numeric` for `discount_depth`. Invalid/missing list price becomes a measured zero discount, not an unknown signal.

### Evidence

- `Database/Analytics/Intelligence/023_price_intelligence_v1.sql:109-112`.

### Scope

- SQL view: replace silent zero with NULL or explicit unavailable marker.
- Consumer queries that use discount_depth: add null-safety tests.

### Do

1. Change `WHEN pp.list_price <= 0 THEN 0::numeric` to `WHEN pp.list_price <= 0 THEN NULL::numeric`.
2. Add a test asserting discount_depth is NULL for invalid list prices.
3. Update downstream consumers to handle NULL (render unavailable, not as 0% markdown).

### Tests

- SQL: verify NULL discount_depth when list_price <= 0.
- Backend: consumers render unavailable, not zero, for NULL discount.

### Acceptance

- Unknown list price does not become measured zero discount.
- Downstream price/markdown analytics treat missing list price correctly.

### Dependencies

- Pricing/ML owners for consumer impact.

---

## RQ189 - Demand acceleration hardcodes 1.0 sentinel for new demand

Status: WAITING
Priority: P2
Type: backend/tests
Feature family: demand-acceleration-new-product-state
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(intelligence): mark new demand distinctly from measured acceleration`

### Problem

When prior 7-day rolling units = 0 and current > 0, `demand_acceleration` is fixed to `1.0` instead of NULL or explicit "new demand" state. Sparse/new SKUs get a moderate score indistinguishable from true +100% relative change, affecting intelligence ranking and derived frontend depletion logic.

### Evidence

- `Database/Analytics/Intelligence/021_product_demand_signals_v1.sql:198-207`.

### Scope

- SQL view: replace hardcoded `1.0` with NULL or add a separate `is_new_demand` flag.
- Frontend consumers of `demand_acceleration`: update to handle NULL or new-demand state.

### Do

1. Replace `1.0` sentinel with NULL for new demand.
2. Or add `demand_state` enum: NEW_DEMAND, ACCELERATING, STABLE, DECELERATING.
3. Add tests proving new-demand products render distinctly.

### Tests

- SQL: zero prior → NULL or NEW_DEMAND state.
- Frontend: mock NULL acceleration; assert new-product copy/icon render.

### Acceptance

- New demand is visually/semantically distinct from measured acceleration.
- Rankings correctly prioritize new vs accelerating products.

### Dependencies

- `RQ152` owns derived-builder semantics; this is the specific new-demand state.

---

## RQ190 - Forecast provenance freshness aggregated optimistically

Status: WAITING
Priority: P1
Type: backend/contract/tests
Feature family: forecast-snapshot-freshness-aggregation
Parallel-safe: no
Owner: Analytics

Commit suggestion: `fix(forecast): aggregate row freshness conservatively; preserve partial-trust states`

### Problem

Row freshness falls back to `issue_time_utc`; list-level freshness uses MAX across rows and can fall back to `DateTime.UtcNow` when marking trusted materialization. Mixed-trust snapshot batches can appear uniformly fresh/trusted while many rows lack real snapshot freshness metadata.

### Evidence

- `GetInventoryForecastHandler.cs:81`, `:88-92`, `:119-126`.

### Scope

- Forecast handler: snapshot freshness aggregation logic
- Forecast DTO: expose row-level and batch-level freshness separately
- Tests for mixed-trust scenarios

### Do

1. Aggregate row freshness conservatively: use MIN (oldest row) instead of MAX.
2. Add a `trustedRowCount` / `totalRowCount` ratio to the batch-level freshness.
3. Preserve and expose row-level freshness when meaningful.
4. Render partial-trust state (e.g., "75% of forecast rows refreshed 2h ago, 25% from 5h ago").

### Tests

- Unit: mixed-age rows → batch freshness reflects oldest row.
- Frontend: render partial-trust messaging.

### Acceptance

- Forecast freshness reflects actual data staleness, not optimistic MAX.
- Partial-trust states are visible.

### Dependencies

- `RQ141` owns lineage; `RQ176` owns inventory snapshot query-time; this is forecast-specific aggregation.

---

## RQ191 - Frontend percent clamp hides negative backend signals

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: frontend-numeric-safety
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(frontend): render unavailable for out-of-bounds confidence, not clamped zero`

### Problem

`normalizePercent()` clamps with `Math.max(0, Math.min(100, value))` before formatting confidence/reliability. Negative or >100 backend values display as `0%` instead of unavailable/negative, weakening trust messaging.

### Evidence

- `analyticsQuality.ts:21-24`, `:26-35`.

### Scope

- Frontend formatter: replace clamp with boundary check + unavailable rendering.
- Tests for out-of-bounds values.

### Do

1. Change clamp logic: if `value < 0` or `value > 100`, return unavailable marker instead of clamping to 0 or 100.
2. Add tests for negative, >100, and null inputs.
3. Document expected range as 0–100, with unavailable as a third state.

### Tests

- Unit: -10 → unavailable; 150 → unavailable; 0 → "0%"; 100 → "100%".
- Frontend: render null/error icon for out-of-bounds confidence.

### Acceptance

- Out-of-bounds confidence values are visible as unavailable/error, not silent zeros.
- Trust messaging is accurate.

### Dependencies

- `RQ139` owns numeric states; this is formatter safety.

---

## RQ192 - Supplier ML return rate coalesces missing to 0%

Status: WAITING
Priority: P2
Type: backend/tests
Feature family: ml-feature-missing-encoding
Parallel-safe: yes
Owner: Analytics

Commit suggestion: `fix(ml): preserve missing return rate as NULL, not zero feature value`

### Problem

`return_rate` uses `NULLIF(units_30d, 0)`, but outer `COALESCE(a.return_rate, 0)` turns no-sales/missing return data into `0`. Training/ranking features treat "unknown returns" as best-case 0% return rate, biasing ML scores and success labels.

### Evidence

- `015_AddSupplierMlRanking.sql:294`, `:357`.

### Scope

- SQL view: replace outer COALESCE(0) with NULL preservation or explicit missing-flag.
- ML training: add missing-value indicators to feature set.
- Tests for missing-data encoding.

### Do

1. Remove outer `COALESCE(..., 0)` or replace with `NULL`.
2. Or add a separate `has_return_data` boolean feature.
3. Add tests proving no-sales suppliers are encoded as missing-return, not zero-return.

### Tests

- SQL: no return data in window → NULL or has_return_data=FALSE.
- ML: missing-return feature prevents bias toward "perfect suppliers."

### Acceptance

- ML features distinguish "unknown returns" from "measured zero returns."
- Training is not biased by missing data.

### Dependencies

- ML/scoring owners; this is feature encoding correctness.

---

## RQ193 - Inventory page cross-panel async race condition

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: analytics-async-ordering
Parallel-safe: no
Owner: Frontend

Commit suggestion: `fix(analytics): add request sequencing to prevent cross-panel races`

### Problem

Inventory page fires 6–7 parallel requests without a monotonic sequence check. Slow response from an earlier filter state can land after a newer filter state, overwriting fresher data across panels.

### Evidence

- `InventoryPage.tsx:288–461` uses shared `cancelled` flag only, no sequence ID.

### Do

1. Add monotonic `requestSequence` counter; increment on filter change.
2. Tag requests with sequence ID; discard responses if stale.
3. Add tests for rapid filter changes.

### Acceptance

- KPI cards, table rows, and panels show consistent snapshot after filter changes.

---

## RQ194 - Analytics Details missing in-flight guard for parallel requests

Status: WAITING
Priority: P1
Type: frontend/tests
Feature family: analytics-details-async-safety
Parallel-safe: no
Owner: Frontend

Commit suggestion: `fix(analytics-details): add request sequencing`

### Problem

`AnalyticsDetails.tsx` fires 10+ parallel requests without sequence check. Rapid period changes mix summary/daily/top/advanced from different loads.

### Evidence

- `AnalyticsDetails.tsx:151–184` (`load()` fires all requests unguarded).

### Do

1. Add monotonic `requestSequence` counter; increment on period/filter change.
2. Discard responses if stale.

### Acceptance

- Trend cards and tables reflect one consistent period after rapid changes.

---

## RQ195 - Pilot Readiness multi-signal load can mix reload generations

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: pilot-readiness-async-consistency
Parallel-safe: no
Owner: Frontend

Commit suggestion: `fix(pilot-readiness): sequence signal loads`

### Problem

`PilotReadinessPage.tsx:loadSignals` uses `cancelled` only; nine tasks can mix across reload ticks.

### Do

1. Add request sequence tracking; discard stale responses.

### Acceptance

- Readiness cards show coherent state (all fresh or all stale).

---

## RQ196 - Inventory report schedules saved without validation

Status: WAITING
Priority: P1
Type: backend/frontend/tests
Feature family: report-schedule-validation
Parallel-safe: no
Owner: Inventory/Reports

Commit suggestion: `fix(reports): validate schedules before persistence`

### Problem

Schedules persist with no checks for empty/invalid recipients, timezone, or `RunAtLocalTime` format. Validation only happens at delivery time.

### Evidence

- `InventoryEndpoints.cs:603–633`, `InventoryReportScheduleService.cs:61–131`.

### Do

1. Add pre-persist validation: parse recipients, validate timezone, format.
2. Return 400 with error messages.
3. Add UI validation before save.

### Acceptance

- Users cannot save schedules that will silently fail.

---

## RQ197 - Scheduled inventory export has no row cap / tight completion window

Status: WAITING
Priority: P2
Type: backend/tests
Feature family: export-truncation-safety
Parallel-safe: yes
Owner: Reports/Export

Commit suggestion: `fix(export): add row cap and graceful truncation`

### Problem

Export loads all articles unbounded; completion waits ~90s. Large catalogs can time out mid-generation.

### Evidence

- `InventoryReportDeliveryService.cs:173–197, 199–220`.

### Do

1. Add configurable `MaxExportRows` (default 50000).
2. Add truncation warning footer.
3. Add tests for large catalogs.

### Acceptance

- Large exports complete with clear truncation messaging.

---

## RQ198 - Executive Decision Board hardcoded dataScope="all"

Status: WAITING
Priority: P1
Type: backend/frontend/contract/tests
Feature family: decision-board-datascope-override
Parallel-safe: no
Owner: Analytics/Decision Board

Commit suggestion: `fix(decision-board): pass user DataScope`

### Problem

Board calls `getDecisionBoardAggregate({ dataScope: "all" })` explicitly, overriding user preference.

### Evidence

- `ExecutiveDecisionBoardPage.tsx:1345`.

### Do

1. Remove hardcoded `"all"`.
2. Read user DataScope from context.
3. Pass it to backend.

### Acceptance

- Board respects user scope selection.

---

## RQ199 - Pre-nivelacija priority endpoint has no DataScope parameter

Status: WAITING
Priority: P1
Type: backend/contract/tests
Feature family: pre-nivelacija-datascope
Parallel-safe: no
Owner: Pre-nivelacija/Analytics

Commit suggestion: `fix(pre-nivelacija): add DataScope parameter`

### Problem

Endpoint accepts no `dataScope`; cache key omits scope. Imported and existing articles always mixed.

### Evidence

- `PreNivelacijaPriorityEndpoints.cs:46–77`.

### Do

1. Add `dataScope` query parameter.
2. Include scope in cache key.
3. Add SQL WHERE clause to filter by scope.

### Acceptance

- Priority list respects user scope selection.

---

## RQ200 - Product Decision Center search is client-only over capped backend rows

Status: WAITING
Priority: P1
Type: backend/frontend/contract/tests
Feature family: pdc-search-pagination-boundary
Parallel-safe: no
Owner: Product Decision/Search

Commit suggestion: `fix(pdc): move search to backend or extend cap`

### Problem

Backend fetch uses `top: 1200`; search runs only on returned rows. Products ranked below cap are unsearchable.

### Evidence

- `ProductDecisionCenterPage.tsx:768, 810–819`.

### Do

1. **Option A:** Add backend `search` parameter; filter there before top-1200.
2. **Option B:** Increase cap with frontend virtualization.
3. Add tests: search for tail SKU → found.

### Acceptance

- All PDC products are searchable.

---

## RQ201 - Daily Sales chart order diverges from table order

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: sales-stats-chart-table-parity
Parallel-safe: yes
Owner: Daily Sales/Analytics

Commit suggestion: `fix(daily-sales): align chart and table sort`

### Problem

Chart always uses ascending order; table uses user sort (date desc or user column). Chart and table show different orderings.

### Evidence

- `DailySalesStatsPage.tsx:484–518, 626–636, 1640–1663`.

### Do

1. Pass `sortConfig` to chart; sort by same order.
2. Add tests for sort consistency.

### Acceptance

- Chart and table row orders match.

---

## RQ202 - Daily Sales date sort uses local Date parsing (timezone drift)

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: date-timezone-safety
Parallel-safe: yes
Owner: Daily Sales/Analytics

Commit suggestion: `fix(daily-sales): use UTC date parsing`

### Problem

Date sort uses `new Date(row.date).getTime()`, which drifts across timezone boundaries.

### Evidence

- `DailySalesStatsPage.tsx:492`.

### Do

1. Parse dates as UTC: `new Date(row.date + 'T00:00:00Z').getTime()`.
2. Add tests for DST boundary cases.

### Acceptance

- Date boundaries sort correctly regardless of timezone.

---

## RQ203 - Inventory SKU detail ignores parent list scope and uses fixed 30-day window

Status: WAITING
Priority: P1
Type: backend/frontend/contract/tests
Feature family: inventory-detail-scope-consistency
Parallel-safe: no
Owner: Inventory Detail/Analytics

Commit suggestion: `fix(inventory-detail): pass list scope and period`

### Problem

Detail endpoint uses hardcoded `DateTime.UtcNow.AddDays(-30)` and `/30` divisor. Doesn't accept store/supplier/period from list view.

### Evidence

- `InventoryEndpoints.cs:268–334` (hardcoded window, no scope parameters).

### Do

1. Add `storeId`, `supplierId`, `fromDate`, `toDate` parameters.
2. Frontend passes them from list view.
3. Add tests for scope consistency.

### Acceptance

- Detail view signals match list-view values and respect filters.

---

## RQ204 - Analytics Details uses global inventory snapshot unrelated to selected period

Status: WAITING
Priority: P2
Type: frontend/contract/tests
Feature family: analytics-details-scope-parity
Parallel-safe: yes
Owner: Analytics Details

Commit suggestion: `fix(analytics-details): scope inventory status to period`

### Problem

Calls `getInventoryStatus(2, true)` with no period/store filters while sales series are period-scoped.

### Evidence

- `AnalyticsDetails.tsx:158, 215–227`.

### Do

1. Pass period/scope to `getInventoryStatus` call.
2. Backend filters inventory snapshot to requested period.

### Acceptance

- Detail page metrics are temporally consistent.

---

## RQ205 - Frontend 15s client cache not invalidated after refresh

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: client-cache-invalidation
Parallel-safe: yes
Owner: Frontend Caching

Commit suggestion: `fix(analytics): clear client cache after refresh`

### Problem

`analyticsApi.ts` caches `/api/analytics/cached/*` for 15–300s. No hook clears cache after worker refresh.

### Evidence

- `analyticsApi.ts:78–80, 353–361`.

### Do

1. Expose `invalidateAnalyticsCache()`.
2. Call after refresh completion.
3. Add tests: cache clears after refresh.

### Acceptance

- Users see updated numbers immediately after refresh.

---

## RQ206 - Partial nightly refresh treated as "last successful refresh"

Status: WAITING
Priority: P1
Type: backend/tests
Feature family: refresh-run-status-accuracy
Parallel-safe: no
Owner: Analytics Worker/Refresh

Commit suggestion: `fix(analytics): distinguish partial from successful refresh`

### Problem

`FindLatestSuccessfulRun` includes `"partial"` status. Dashboard can show green after incomplete MV refresh.

### Evidence

- `AnalyticsRefreshStatusService.cs:418–427`.

### Do

1. Exclude `"partial"` from success check.
2. Update UI to render partial as yellow/warning.

### Acceptance

- Dashboard clearly indicates partial/incomplete refreshes.

---

## RQ207 - Failed nightly refresh skips cache invalidation despite partial MV updates

Status: WAITING
Priority: P1
Type: backend/tests
Feature family: refresh-failure-cache-safety
Parallel-safe: no
Owner: Analytics Worker/Cache

Commit suggestion: `fix(analytics): invalidate cache even on failed refresh`

### Problem

When `errors.Count > 0`, worker returns before cache clear. Some views may have partially refreshed while API cache serves old aggregates.

### Evidence

- `NightlyAnalyticsRefreshWorker.cs:349–361 vs 382–393`.

### Do

1. Move cache invalidation outside success check; always run if any view completed.
2. Add tests for failure scenarios.

### Acceptance

- Cache is invalidated consistently after failures.

---

## RQ208 - Dashboard "per day" KPIs use local calendar day count (timezone drift)

Status: WAITING
Priority: P2
Type: frontend/tests
Feature family: period-timezone-boundary-safety
Parallel-safe: yes
Owner: Analytics Dashboard

Commit suggestion: `fix(dashboard): use UTC day count for per-day divisor`

### Problem

`selectedDays` derived from local `parseInputDate()` floor division. Backend boundaries are UTC. Divisor can be off by one near DST boundaries.

### Evidence

- `AnalyticsDashboard.tsx:644–648, 824–825`.

### Do

1. Compute day count from backend UTC timestamps.
2. Add tests for DST boundary cases.

### Acceptance

- Revenue/day and Transactions/day divisors match backend period length.

---

## RQ209 - Dual concurrent EF migration paths cause race condition

Status: WAITING
Priority: P1
Type: backend/infra/tests
Feature family: database-migration-orchestration
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(db): coordinate EF migrations to prevent concurrent race`

### Problem

In Development, `Program.cs:997-1014` calls `Database.Migrate()` while `DeferredStartupTasksHostedService` later runs `DatabaseInitializer.MigrateAsync()`. Concurrent attempts cause duplicate-relation errors or partial schema state.

### Evidence

- `Program.cs:997-1014` (sync migrate at startup)
- `DatabaseInitializer.cs:816` (async migrate in hosted service)

### Do

1. Remove sync migrate from Program.cs; rely only on DeferredStartupTasksHostedService.
2. Or add a lock/gate to ensure only one migration runs.
3. Add tests for startup with concurrent instance simulators.

### Acceptance

- Single migration runs per instance; no duplicate-table errors.

---

## RQ210 - Startup init silently skipped after lock timeout

Status: WAITING
Priority: P1
Type: backend/infra
Feature family: startup-readiness-gate
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(db): fail fast if startup lock cannot be acquired`

### Problem

If advisory startup lock not acquired within 120s, database init skipped with only warning. Instance serves traffic against unmigrated schema.

### Evidence

- `DatabaseInitializer.cs:53-58`.

### Do

1. If lock timeout, throw and fail startup hard instead of warning.
2. Or extend timeout / retry with exponential backoff.

### Acceptance

- Unmigrated schemas cause startup failure, not silent skip.

---

## RQ211 - Parallel SQL migrations without ordering guarantees

Status: WAITING
Priority: P1
Type: backend/infra
Feature family: migration-sequencing
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(db): sequence critical migrations with proper dependencies`

### Problem

Migrations 012, 017, 019 run in parallel via `Task.WhenAll`; failures logged but swallowed. Partial view creation leaves dashboards slow/incomplete.

### Evidence

- `DatabaseInitializer.cs:838-866`.

### Do

1. Add explicit dependency checks between migrations.
2. Fail hard on any migration error instead of swallowing.
3. Add tests for migration order/dependency safety.

### Acceptance

- All migrations complete or startup fails; no partial state.

---

## RQ212 - Migration failures swallowed; app runs on drifted schema

Status: WAITING
Priority: P1
Type: backend/infra
Feature family: migration-failure-safety
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(db): fail on EF migration errors instead of continuing`

### Problem

EF migration exceptions logged as warnings; init continues with "self-heal" SQL. Production runs on schema diverged from migration history.

### Evidence

- `DatabaseInitializer.cs:819-833, 2105-2108`.

### Do

1. Throw on any EF migration error; don't continue.
2. Remove "self-heal" fallback; let failure surface.

### Acceptance

- Migration errors prevent app startup; no drifted schema.

---

## RQ213 - EF migration Down() drops core analytics fact tables without backup

Status: WAITING
Priority: P1
Type: backend/infra
Feature family: migration-reversibility
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(migrations): archive or prevent irreversible rollbacks of fact tables`

### Problem

Rolling back SalesFacts migration drops both `SalesFacts` and `SalesLineFacts` with no archive. Mistaken rollback permanently destroys historical data.

### Evidence

- `20260110170000_AddSalesFacts.cs:84-90`.

### Do

1. Add archive/export step in `Down()`.
2. Or prevent down-migration for fact tables via guards.

### Acceptance

- Core fact tables cannot be accidentally dropped without explicit backup.

---

## RQ214 - Seed sales created without decrementing stock

Status: WAITING
Priority: P2
Type: backend/seed/tests
Feature family: seed-data-consistency
Parallel-safe: yes
Owner: Data/Seeding

Commit suggestion: `fix(seed): decrement article stock when creating seed sales`

### Problem

Seed creates up to 100 `SEED-*` sales from articles with `Kolicina > 0` but never updates `Artikli.Kolicina`.

### Evidence

- `TrendplusDbSeeder.cs:259-335`.

### Do

1. After each seed sale, decrement corresponding `Artikli.Kolicina`.
2. Add tests verifying final stock matches sales generated.

### Acceptance

- Seed data is internally consistent (inventory + sales = reality).

---

## RQ215 - Aggregate refresh delete+insert is non-transactional (CRITICAL)

Status: WAITING
Priority: P0
Type: backend/worker/tests
Feature family: aggregation-worker-atomicity
Parallel-safe: no
Owner: Analytics Worker

Commit suggestion: `fix(aggregation): wrap delete+insert in transaction`

### Problem

Category/Supplier/Gender/TopProducts refresh DELETEs a day's rows then INSERTs replacements in separate commands. If INSERT fails after DELETE, dashboards show zero/empty for that day.

### Evidence

- `AnalyticsAggregationWorker.cs:344-375, 387-420, 432-461, 474-508`.

### Do

1. Wrap DELETE + INSERT in single `BeginTransactionAsync()`.
2. Retry entire transaction on failure.
3. Add tests for mid-operation failure (DELETE succeeds, INSERT fails).

### Tests

- Simulate INSERT failure after DELETE → confirm transaction rolls back → data intact.

### Acceptance

- Aggregates are all-or-nothing; no partial deletions.

---

## RQ216 - Cache invalidated after partially failed aggregate refresh

Status: WAITING
Priority: P1
Type: backend/worker/tests
Feature family: aggregation-failure-cache-safety
Parallel-safe: no
Owner: Analytics Worker

Commit suggestion: `fix(aggregation): only invalidate cache after all tables succeed`

### Problem

Per-table refresh failures are caught individually, but cache invalidation still runs after loop, causing misses followed by incomplete data.

### Evidence

- `AnalyticsAggregationWorker.cs:203-215, 215-218`.

### Do

1. Only call `InvalidateAggregateBackedCachesAsync` if all tables succeeded.
2. Or add a "partial invalidation" state to warn UI.

### Acceptance

- Cache invalidated consistently with actual refresh success.

---

## RQ217 - Outbox worker has no row-level locking

Status: WAITING
Priority: P1
Type: backend/worker/tests
Feature family: outbox-concurrent-processing
Parallel-safe: no
Owner: Outbox/Worker

Commit suggestion: `fix(outbox): add FOR UPDATE SKIP LOCKED to prevent duplicates`

### Problem

Messages fetched with plain SELECT, not `FOR UPDATE SKIP LOCKED`. Multiple instances process same event concurrently; `SalesLineFacts` has no unique constraint, so duplicate facts inflate revenue/units.

### Evidence

- `OutboxProcessorWorker.cs:168-172`.

### Do

1. Use `FOR UPDATE SKIP LOCKED` to claim rows atomically.
2. Add unique constraint on `(SaleId, ProductId)` to prevent duplicates at DB level.
3. Add tests for concurrent processing.

### Tests

- Two workers process same message concurrently → only one succeeds; no duplicate facts.

### Acceptance

- Outbox projection is idempotent across concurrent workers.

---

## RQ218 - Access import auto-retry requeues without rolling back

Status: WAITING
Priority: P1
Type: backend/worker/tests
Feature family: import-retry-idempotency
Parallel-safe: no
Owner: Import/Access

Commit suggestion: `fix(import): compensate partial writes before retry`

### Problem

Failed batches reset to `pending` with progress zeroed but no rollback of partially imported rows. Retried imports duplicate sales/analytics facts.

### Evidence

- `AccessImportBackgroundWorker.cs:419-432`.

### Do

1. On failure, rollback/delete partial facts before requeuing.
2. Or use batch-level transaction.
3. Add idempotency check (detect+skip if re-imported).

### Acceptance

- Retried imports don't duplicate facts; data is consistent.

---

## RQ219 - Background worker crashes are silently ignored (CRITICAL)

Status: WAITING
Priority: P0
Type: backend/infra/monitoring
Feature family: worker-process-health
Parallel-safe: no
Owner: Infrastructure/Monitoring

Commit suggestion: `fix(host): fail host on worker exception or add external monitoring`

### Problem

`BackgroundServiceExceptionBehavior = Ignore` means unhandled exceptions terminate workers permanently with no host failure or alert. Analytics go stale with no surface.

### Evidence

- `Program.cs:64-67`; `Trendplus2\Program.cs:47`.

### Do

1. Change to `BackgroundServiceExceptionBehavior = ThrowAndStop` or add exception handler that logs +alerts.
2. Add external monitoring (e.g., health endpoint that checks worker status).
3. Add tests for exception propagation.

### Acceptance

- Worker death is visible and causes app restart or alert.

---

## RQ220 - Outbox messages dead-lettered with no automatic surfacing

Status: WAITING
Priority: P1
Type: backend/worker/monitoring
Feature family: outbox-dlq-observability
Parallel-safe: no
Owner: Outbox/Monitoring

Commit suggestion: `fix(outbox): surface dead-letter queue and enable auto-retry or alerts`

### Problem

Messages with `RetryCount >= 5` excluded forever; recovery requires manual `/api/outbox/retry-all-failed`. SLA mapper marks DLQ as "not instrumented."

### Evidence

- `OutboxProcessorWorker.cs:217-224`; `WorkerSlaEvidenceMapper.cs:13`.

### Do

1. Add background task to periodically alert on dead-lettered messages.
2. Or implement exponential-backoff DLQ with eventual flush to error log.

### Acceptance

- Dead-letter queue is visible; operators can detect and fix stuck events.

---

## RQ221 - Insight Studio endpoints return raw exception messages

Status: WAITING
Priority: P2
Type: backend/security
Feature family: error-response-sanitization
Parallel-safe: yes
Owner: API/Security

Commit suggestion: `fix(api): sanitize error responses in Insight Studio endpoints`

### Problem

Analytics endpoints return `Results.Problem(detail: ex.Message)` on failure. Postgres/SQL errors and table names can appear in API responses.

### Evidence

- `InsightStudioEndpoints.cs:137-140, 257, 336, 428, 532, 652, 756`.

### Do

1. Wrap exception details in `ProblemDetails` with generic message.
2. Log full exception for diagnostics; expose only `statusCode` and reason to client.

### Acceptance

- No internal SQL errors or table names visible in API responses.

---

## RQ222 - Daily vs dimensional aggregates disagree on orphan sales lines

Status: WAITING
Priority: P1
Type: backend/tests
Feature family: aggregate-consistency
Parallel-safe: no
Owner: Analytics Worker

Commit suggestion: `fix(aggregation): align join logic across daily and dimensional aggregates`

### Problem

`AnalyticsDailySummary` joins only headers/lines; category/supplier/gender INNER JOIN articles, excluding lines with missing products. Total revenue exceeds sum of breakdowns.

### Evidence

- `AnalyticsAggregationWorker.cs:311-312 vs 363-365`.

### Do

1. Use consistent join logic across all aggregates.
2. Document whether orphan lines are included or excluded.
3. Add tests proving total = sum of parts.

### Acceptance

- Dashboard total revenue = sum of category/supplier/gender breakdowns.

---

## RQ223 - Default SkipInvalidForeignKeys=true silently drops orphan import lines

Status: WAITING
Priority: P1
Type: backend/import/tests
Feature family: import-data-completeness
Parallel-safe: no
Owner: Import/Access

Commit suggestion: `fix(import): make invalid-FK handling explicit and auditable`

### Problem

Access import skips `prodaja_stavke` rows with missing parent headers when default flag is true. Analytics appear successful while data is incomplete.

### Evidence

- `AccessImportOptions.cs:28`; `AccessImportService.cs:4769, 4896-4898`.

### Do

1. Default to `SkipInvalidForeignKeys=false` (fail on invalid FKs).
2. Log count of skipped rows; fail import if count > 0.
3. Add tests for orphan detection.

### Acceptance

- Invalid FKs cause import failure or explicit warning; no silent data loss.

---

## RQ224 - Analytics DB connection silently falls back in production

Status: WAITING
Priority: P1
Type: backend/infra
Feature family: analytics-db-routing-safety
Parallel-safe: no
Owner: Infrastructure

Commit suggestion: `fix(config): fail startup if AnalyticsConnection missing in prod`

### Problem

Missing `AnalyticsConnection` falls back to `DefaultConnection` in non-dev. Staging/prod can query wrong database with no hard failure.

### Evidence

- `AnalyticsConnectionResolver.cs:71-87`.

### Do

1. In prod/staging, throw if `AnalyticsConnection` not configured.
2. Or use explicit `IsProduction` check in fallback guard.

### Acceptance

- Wrong database configuration causes startup failure, not silent fallback.

---

## RQ225 - UseSnapshotCost feature flag toggles live without validation

Status: WAITING
Priority: P1
Type: backend/config
Feature family: feature-flag-safety
Parallel-safe: no
Owner: Config/Analytics

Commit suggestion: `fix(config): validate snapshot-cost flag at startup or add runtime guards`

### Problem

Only `StorageOptions` validates at startup; snapshot cost flag read via `IOptionsMonitor.CurrentValue` with no validator. Flipping live switches margin calculations, causing KPI jumps.

### Evidence

- `AnalyticsSnapshotOptions.cs:7-9`; `AnalyticsCostSnapshotService.cs:408-410, 656`.

### Do

1. Add `ValidateOnStart` for snapshot cost options.
2. Or add warning log when flag changes at runtime.

### Acceptance

- Snapshot cost flag is stable; changes require restart or explicit audit log.

---

## RQ226 - Invalid nightly refresh schedule silently defaults

Status: WAITING
Priority: P2
Type: backend/config/worker
Feature family: worker-schedule-safety
Parallel-safe: yes
Owner: Worker/Config

Commit suggestion: `fix(config): fail or alert if nightly refresh schedule is malformed`

### Problem

Malformed `NightlyAnalyticsRefresh:RunAtUtc` values fall back to 00:10 UTC with no startup error. MV refreshes run at unintended times.

### Evidence

- `NightlyAnalyticsRefreshWorker.cs:794-802`; `WorkerRegistryService.cs:178-184`.

### Do

1. Add validation for `RunAtUtc` at startup.
2. Log actual scheduled time to confirm intent.

### Acceptance

- Malformed refresh schedule causes startup failure or explicit audit trail.

---

## RQ227 - Batch delete proceeds after archive quota failure

Status: WAITING
Priority: P1
Type: backend/operations
Feature family: cleanup-safety-gates
Parallel-safe: no
Owner: Data/Operations

Commit suggestion: `fix(cleanup): fail batch delete if archive quota exceeded`

### Problem

When archive insert fails (storage full), delete still proceeds. Irreversible data loss with no restore path.

### Evidence

- `AccessImportService.cs:3382-3386, 3771-3796, 3448-3457`.

### Do

1. If archive fails, halt delete; don't proceed.
2. Or add explicit "force delete despite archive fail" flag requiring admin approval.

### Acceptance

- Data cleanup doesn't proceed without successful archive backup.

---

## RQ228 - Insight Studio v1/v2 period handling treats local dates as UTC

Status: WAITING
Priority: P1
Type: backend/contract
Feature family: period-timezone-contract-consistency
Parallel-safe: no
Owner: Insight Studio/Analytics

Commit suggestion: `fix(api): align period parsing across Insight Studio v1/v2 and legacy endpoints`

### Problem

Both v1 (`/api/analytics/advanced`) and v2 (`/api/analytics/advanced/v2`) use `DateTime.SpecifyKind(value, DateTimeKind.Utc)` without converting from local time. Clients sending local dates get shifted boundaries; KPIs differ between Insight Studio and legacy screens.

### Evidence

- `InsightStudioEndpoints.cs:32-37`; `InsightStudioV2Endpoints.cs:34-39`.

### Do

1. Normalize all period parsing (v1, v2, legacy) to use same timezone logic.
2. Add explicit UTC vs local handling.
3. Add contract tests proving consistency.

### Acceptance

- Same local date produces same KPIs across all endpoints.

