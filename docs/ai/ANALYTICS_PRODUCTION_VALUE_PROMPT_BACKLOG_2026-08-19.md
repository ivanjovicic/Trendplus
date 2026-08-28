# Analytics Production Value Prompt Backlog - 2026-08-19

Status: planning-only backlog. Every item is `WAITING`; this document changes no
current `READY` pointer and is not claimable until the owning queue promotes it.
Remote `main` snapshot `2361627541983f68bcb506b37cbbabf74f6478da` has no current
execution READY: `P-UI-22`, `RQ96`, `RQ100`-`RQ105`, and `OBS10` are DONE;
`RQ106` remains WAITING. Each implementation must follow
`docs/ai/PROMPT_QUEUE_PROTOCOL.md`, preserve higher-priority BCI/STAB/RQ work, use a
local lock, create a run log, and include main deployment evidence.

Delivery warning: the audit workspace was 110 commits behind remote main after
`git fetch origin main`. Existing local React changes must be preserved on a scoped
branch, rebased/ported onto current main and retested. They must not be pushed as an
old-tree replacement.

## Priority order

| Candidate | Status | Suggested owner | Priority | Delivery target |
| --- | --- | --- | --- | --- |
| PROD-AN-01 | WAITING | STAB/RQ | P0 | main + production smoke |
| PROD-AN-02 | WAITING | PERF/OBS/RQ | P0 | main + production smoke |
| PROD-AN-03 | WAITING | RQ | P0 | main + production smoke |
| PROD-AN-04 | WAITING | RQ | P0 | main + production smoke |
| PROD-AN-05 | WAITING | RQ/P-UI | P1 | main + production smoke |
| PROD-AN-06 | WAITING | RQ/STAB | P1 | main + provenance evidence |
| PROD-AN-07 | WAITING | BCI/STAB | P1 | main + CI evidence |
| PROD-AN-08 | WAITING | DEX/P-UI | P2 | main + usability evidence |
| PROD-AN-09 | WAITING | STAB/OBS | P0 | main + production isolation evidence |
| PROD-AN-10 | WAITING | RQ/QDB | P1 | main + data-contract evidence |
| PROD-AN-11 | WAITING | RQ/OBS | P0 | main + reconciliation evidence |
| PROD-AN-12 | WAITING | STAB/QDB | P0 | main + migration/deploy evidence |
| PROD-AN-13 | WAITING | RQ/DEX | P0 | main + causal-evaluation evidence |
| PROD-AN-14 | WAITING | RL/RQ | P1 | main + measured-outcome evidence |

## PROD-AN-01 - Ship durable report flow and prove release parity

Status: WAITING

### Problem

Production Pilot Intake Report still treats browser state as canonical and expires on
reload, even though the backend provides a durable report query.

### Evidence

- Production `/analytics/reports/pilot-intake` rendered the expiry screen on 2026-08-19.
- Production `/api/analytics/reports/pilot-intake` returned a durable 24-row report and stable query URL.
- The exact stable-query API also returns 24 rows, but its matching UI URL renders a no-data state.
- Local uncommitted changes on the old base in `analyticsTableState.ts`,
  `SupplierDecisionReportActions.tsx`, `SupplierDecisionReportPage.tsx`, and
  `PilotIntakeReportPage.tsx` implement part of the intended flow; current remote
  main already contains newer overlapping analytics work.

### Scope

- Existing local report durability patch and its focused tests only.
- Release/deploy configuration or evidence files only if required to prove the same main commit is deployed.

### Read first

- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`
- `Klijent/clientapp/src/services/analyticsTableState.ts`
- `Klijent/clientapp/src/pages/PilotIntakeReportPage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

### Do

1. Preserve the local patch on a scoped branch, rebase/port it onto current remote main, and resolve behavior against the newer report implementation without overwriting remote work.
2. Keep browser payload storage as an explicit `preview=browser` fallback, not report source of truth.
3. For a valid durable query context, refetch and map the backend report after reload instead of rendering expiry or false no-data UI.
4. Add a payload-shape contract proving the API's 24 rows reach visible report sections and no-data guards use backend semantics.
5. Deploy the exact main commit and record its SHA, frontend deployment identity, and current production smoke.

### Tests

- Focused Supplier Decision Report, Pilot Intake Report, actions, and analytics table-state tests.
- `npm run typecheck` and `npm run build` in `Klijent/clientapp`.
- Production hard-reload smoke for both durable report URLs and one explicit preview-only expiry scenario.

### Acceptance

- A durable Pilot Intake or Supplier report URL survives hard reload and returns the same requested report context.
- Expiry copy appears only for explicit preview-only URLs without retrievable backend context.
- The deployed main SHA is evidenced; local success alone is insufficient.

### Dependencies

- Must be scoped around current uncommitted report work.
- Current workspace old base must not be pushed directly; all focused tests and production build must rerun after rebase.
- Do not bundle Product, Inventory, or score-semantic changes.

## PROD-AN-02 - Bound decision read latency and remove false loading zeros

Status: WAITING

### Problem

Product Decision Center and Executive Decision Board cannot support decisions while their backend reads exceed the client budget; Product additionally renders zero KPIs during loading.

### Evidence

- The repeated production calls returned HTTP 200, but decision-center took 16.2 seconds and returned 990,681 bytes while decision-board took 17.7 seconds.
- An earlier cold/read attempt exceeded 35 seconds and rendered unavailable/loading states.
- Product can render KPI zeroes while loading before the 1,200-row result arrives.

### Scope

- Decision Board and Product Decision Center backend query, cache, timeout, DTO, frontend loading/error state, and focused tests.
- Request tracing/metrics only where needed to establish the runtime bottleneck.

### Read first

- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`
- decision-board and cached product endpoint handlers/services
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- Executive Decision Board page and current performance/observability queue guidance

### Do

1. Measure query, serialization, transfer, cache, and dependency timings on production-like data before selecting a fix.
2. Establish summary-first and server-paginated response paths with correlation IDs and explicit unavailable/partial semantics.
3. Define response-size and p95 budgets; do not transfer the full decision dataset to populate above-the-fold cards.
4. Render skeleton/unknown/error states while a result is pending; render numerical zero only from a successful zero-result payload.
5. Preserve requested period, scope, freshness, fallback and data-quality context through cache and UI.

### Tests

- Focused backend endpoint latency/contract tests and cache-key tests.
- Frontend tests for loading, successful true-zero, timeout/unavailable, and partial-result states.
- Production smoke with recorded elapsed times for both endpoints.

### Acceptance

- Both routes meet an owner-approved response budget on production-like data, with p95 evidence recorded.
- No timeout, abort, unknown, or missing payload is displayed as a zero KPI or empty true-result table.
- The UI gives a recoverable next action and never invents a recommendation from stale data.

### Dependencies

- Requires RQ/PERF/OBS owner assignment and must not weaken correctness to hit a latency target.

## PROD-AN-03 - Enforce supplier report filter and provenance parity

Status: WAITING

### Problem

Supplier Decision Report can declare an effective period and scope that differ from the stable URL it returns.

### Evidence

- A production supplier report URL specified 2026-02-20 through 2026-08-19.
- Its payload declared 2011-01-20 through 2026-06-06, `all_time`, and `usedFallback: false`.

### Scope

- Supplier Decision Report query parsing, backend calculation, DTO/report metadata, frontend labels/export/print, and contract tests.

### Read first

- Supplier report endpoint, query DTO, calculation service, and report pages.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

### Do

1. Trace requested, normalized, effective and displayed filters through the endpoint and report renderer.
2. Require report ID, stable URL, root period fields, table data, export, and print to use one effective filter contract.
3. If fallback is unavoidable, declare `usedFallback: true`, fallback reason, requested and effective filters, and recommendation eligibility.
4. Fail closed when the effective range cannot be established.

### Tests

- Backend contract tests for bounded date, all-time, store/supplier scope, fallback, and no-data inputs.
- Frontend report tests for durable URL, visible period, export and print parity.
- Production smoke of one bounded and one all-time report.

### Acceptance

- A report cannot label itself bounded while calculating all-time data.
- Unknown/fallback provenance cannot look canonical or decision-safe.
- All rendered and exported report surfaces share the same verified effective context.

### Dependencies

- May follow PROD-AN-01 but must remain an independent backend correctness change.

## PROD-AN-04 - Guard inventory actions against invalid baseline and recommendation data

Status: WAITING

### Problem

Inventory replenishment recommendations present contradictory counts, negative OOS count, zero monetary value and low-confidence stock signals as operational actions.

### Evidence

- Production showed `P1 DOPUNI 11.885` and `P1 OOS 7D -11.885`.
- Twenty-four critical replenishment proposals carried zero RSD value and repeated opaque product codes.
- Detail cards exposed 55% snapshot confidence, unavailable stock coverage and critical zero sell-through while recommendation was marked allowed.
- Supplier/location labels include hash placeholders and duplicated visible names, so recommendation deduplication cannot be trusted from labels alone.

### Scope

- Inventory recommendation calculation, DTO validation, workflow creation/rendering, quantity/value/unit contracts, and focused tests.

### Read first

- `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`
- inventory insight/risk/recommendation endpoint handlers and workflow services
- Inventory page and its existing signal reliability tests
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`

### Do

1. Define and enforce non-negative count semantics separately from signed delta semantics.
2. Require stable item/location identity, an observed or declared proxy baseline, actionable quantity, and non-misleading value before enabling approve/order-like actions.
3. Gate recommendations when stock coverage, velocity, snapshot confidence or value basis is unknown; present them as investigation signals instead.
4. Surface reason codes in readable Serbian and deduplicate identical proposed actions.

### Tests

- Backend tests for zero, missing, negative, duplicate, low-confidence and unavailable-baseline cases.
- UI tests proving disabled/investigation state and absence of approval controls for gated recommendations.
- Production smoke that does not mutate workflow state.

### Acceptance

- No visible count that means a count is negative.
- No actionable replenishment recommendation has zero/unknown value or unqualified baseline unless explicitly marked non-actionable.
- Recommendation state, detail card, table, workflow and export agree on quantity, currency, provenance and gating.

### Dependencies

- Must coordinate with existing `RQ96`; it must not fabricate historical stock to unblock actions.

## PROD-AN-05 - Make health, readiness, freshness and gating comparable

Status: WAITING

### Problem

Data Quality can present excellent health beside materially incomplete catalog/action readiness without explaining their distinct populations or operational consequence.

### Evidence

- Production Data Quality showed health 100/100 and green status together with readiness 77/100, 1,087 missing-cost rows, 12,344 insufficient signals and 656 ignored rows.
- Multiple pages pair a rendered timestamp with freshness `unknown`; refresh status has no completed timestamp and workers are disabled.

### Scope

- Data Quality/Trust DTOs, calculation metadata, AnalyticsTrustHeader, Data Quality, Pilot Intake, Supplier, Product and Inventory trust displays, plus focused tests.

### Read first

- Data Quality health service and Pilot Intake report builder.
- analytics refresh-status endpoint and metadata DTOs.
- `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader*`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md`

### Do

1. Define sales-impact health and catalog/action readiness as distinct named contracts with population, numerator, denominator, period, source and refresh metadata.
2. Show recommendation gating directly next to the affected decision, not only on a distant quality page.
3. Display unknown freshness as unknown; distinguish page-render time, source observation time, completed pipeline refresh time, and cache time.
4. Explain why empty offender lists coexist with non-zero issue counts and provide a drilldown/filter action.

### Tests

- Contract tests for true zero, unknown, partial, stale, no-sales, missing-cost and blocked recommendation cases.
- Component/page tests for coherent labels and accessible warning hierarchy.
- Production comparison smoke across Data Quality, Supplier, Product and Inventory.

### Acceptance

- A user can identify which score applies to sales impact versus catalog/action readiness without reading methodology.
- Unknown or absent freshness never appears fresh, green or complete.
- Cross-surface gating is consistent for the same effective context.

### Dependencies

- Depends on product/inventory payload contracts remaining fail-closed.
- P-UI presentation work cannot invent backend quality semantics.

## PROD-AN-06 - Reconcile runtime inventory semantics after RQ96

Status: WAITING

### Problem

`RQ96` is DONE on current remote main, but production inventory still mixes catalog
item count, stock units, aging buckets and recommendation provenance in ways that
are easy to misread. The remaining work is to prove runtime consumption and deploy
parity, not to recreate the observed snapshot foundation.

### Evidence

- Production inventory showed 12,422 "SKU" and 3,566 units, with every aging bucket in 0-30 days.
- Remote main marks `RQ96` DONE and includes observed snapshot contract evidence.
- Production still shows all 12,422 catalog items in the 0-30 aging bucket and allows low-confidence recommendations.

### Scope

- Runtime/API/UI reconciliation against the completed RQ96 contract; no duplicate snapshot foundation.
- Affected inventory DTO/UI labels, materializer/deploy evidence and tests.

### Read first

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` (`RQ96`).
- `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`.
- Inventory DTOs and page labels.

### Do

1. Verify deployed materializers and APIs consume RQ96's observed-versus-reconstructed distinction.
2. Specify entity grain for every displayed inventory metric: catalog item, SKU/store line, physical unit, valued positive stock, and movement observation.
3. Expose source observation date and coverage for aging and valuation; do not imply all inventory is recent when the movement basis is incomplete.
4. Label catalog count and physical unit count distinctly in UI, table, export and report.

### Tests

- RQ96 focused SQL/backend provenance tests.
- Frontend tests for units, entity labels, unavailable aging basis and export parity.

### Acceptance

- Every inventory metric has a declared grain, unit, period and provenance.
- Observed, reconstructed, missing and mixed history remain distinguishable.
- The UI cannot conflate catalog rows with physical stock units.

### Dependencies

- `RQ96` is DONE; owner promotion is required for this production re-entry.
- If the deployed database migration/materializer is absent, route the release repair through STAB/QDB rather than rewriting frontend labels.

## PROD-AN-07 - Establish analytics main-release verification gate

Status: WAITING

### Problem

The broad analytics suite is not green, so local focused fixes can reach main without a dependable regression signal for shared analytics behavior.

### Evidence

- Prior `npm run test:analytics` execution had failures in Pilot Readiness, Executive Decision Board, analytics API contract/MSW AbortSignal handling, methodology registry and inventory freshness/router harnesses.
- Production has independently visible regressions in reports and decision screens.

### Scope

- Analytics test runner/harnesses, affected tests, CI command wiring and production read-only smoke script only.

### Read first

- Current analytics test failures and their specs.
- BCI/STAB queue guidance and `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.

### Do

1. Classify every existing analytics-suite failure as production bug, stale expectation, harness defect or environment dependency.
2. Repair production behavior before changing expectations; repair test harnesses without weakening assertions.
3. Add a read-only post-deploy smoke that checks readiness, refresh metadata, durable report URL, Board/Product response semantics and Inventory action gating.
4. Make the main pipeline fail on the audited P0 contracts once stable.

### Tests

- `npm run test:analytics`.
- Focused .NET tests for changed analytics endpoints.
- CI execution of the post-deploy smoke against approved production environment.

### Acceptance

- `npm run test:analytics` is green or every explicitly excluded external dependency has a bounded owner and non-silent CI status.
- The release gate detects report expiry regression, false loading zero, filter mismatch and unsafe inventory action regression.
- No test is deleted or weakened merely to produce green output.

### Dependencies

- BCI/STAB owns CI gate policy; coordinate before changing required checks.

## PROD-AN-08 - Turn valid signals into explainable weekly actions

Status: WAITING

### Problem

Even where values are real, users need to know the decision, evidence, uncertainty, expected impact and next drilldown in one place.

### Evidence

- Supplier Consolidated is the strongest live workspace but all suppliers are currently insufficient-data.
- Data Quality, Inventory and Product place quality, gating and action context on separate surfaces or use opaque reason codes.

### Scope

- Decision presentation and drilldown behavior only after PROD-AN-02 through PROD-AN-05 are accepted.
- Existing supplier, product, inventory and Data Quality pages; no new dashboard.

### Read first

- `AGENTS.md` analytics product goals.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.
- Current DEX/P-UI queue work and accepted payload contracts.

### Do

1. Present a single primary weekly action with reason, evidence window, estimated impact, confidence/provenance and blocking prerequisite.
2. Translate reason codes into concise Serbian explanations with a drilldown to the exact rows causing the block.
3. Keep non-actionable signals visibly separate from order/approval-ready actions.
4. Validate with real production-like datasets and non-mutating usability scenarios.

### Tests

- Page/component tests for action, blocked, unknown and true-zero states.
- Accessibility checks for status and reason text.
- Product-owner review with documented scenarios for supplier, product, inventory and data-quality users.

### Acceptance

- A user can state what to do, why, on which data, and what must be fixed when action is blocked.
- No display converts unknown, partial or low-confidence evidence into a positive action.
- Existing filters, exports and reports preserve the same decision context.

### Dependencies

- PROD-AN-02, PROD-AN-03, PROD-AN-04 and PROD-AN-05 accepted first.
- Current P-UI queue is complete; owner promotion must still follow `MASTER_ROADMAP.md`.

## PROD-AN-09 - Isolate synthetic tests from production decision data

Status: WAITING

### Problem

Production Executive Decision Board includes smoke-test actions in live counts and operator queues.

### Evidence

- Production rendered `Smoke Dashboard Final`, `Smoke`, and `Smoke Inventory Final` as open central actions.
- The rows are dated 2026-05-22 and contribute to the board's three open actions.

### Scope

- Action/outcome persistence, smoke-test fixtures, environment/tenant tagging, board queries, CI smoke setup and an owner-approved remediation script or runbook.

### Read first

- Central action and outcome entities, endpoints and materializers.
- Production smoke scripts and test fixtures that create action rows.
- Tenant/environment authority guidance and `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.

### Do

1. Trace the exact writer and identity of every production smoke row before changing data.
2. Add an authoritative synthetic/test marker or isolated tenant/environment and exclude it from production decisions, metrics, outcomes and learning datasets.
3. Prepare a reversible, audited quarantine or cleanup of existing artifacts; require explicit production owner approval before mutation.
4. Change production smoke to verify contracts without persisting user-visible business actions, or guarantee cleanup in the same run.

### Tests

- Two-tenant/environment negative tests proving synthetic rows cannot leak into live decision queries.
- Focused board/action/outcome tests for counts, queues and training/export exclusion.
- Read-only production smoke proving zero synthetic actions are visible after approved remediation.

### Acceptance

- Production action counts and queues contain no smoke/test fixtures.
- Test data is structurally isolated, not hidden by display-name matching.
- Any cleanup has backup, audit record, exact affected IDs and owner approval.

### Dependencies

- STAB/OBS and tenant/data owner approval are required before production data mutation.
- No cleanup is authorized by this planning prompt alone.

## PROD-AN-10 - Enforce canonical master-data identity and value contracts

Status: WAITING

### Problem

Duplicate visible store names, hashed supplier placeholders, opaque item identity and invalid/implausible acquisition values undermine grouping and action value across analytics.

### Evidence

- Production renders two `Komision` locations with the same visible name and an ambiguous comparison conclusion.
- Inventory exposes suppliers such as `Dobavljac #-1879980587` and critical replenishment actions with zero RSD value.
- Data Quality remains 100/100 because current health weighting does not capture these decision-critical master-data defects.

### Scope

- Canonical store/supplier/item identifiers, import mapping, acquisition-price validation, analytics DTO display identity, Data Quality checks and affected exports/tests.

### Read first

- Store, supplier, article and import mapping models/services.
- Inventory and supplier aggregation SQL/handlers.
- Data Quality health/readiness calculations.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.

### Do

1. Define canonical IDs and human-readable disambiguated labels independently; never use a display label or runtime hash as identity.
2. Trace duplicate stores and placeholder suppliers to source/import lineage and preserve raw-source provenance.
3. Define currency, minor/major unit, zero, missing and implausible acquisition-value behavior before aggregation or recommendation.
4. Gate affected decisions and expose repair drilldowns until identity/value validation passes.
5. Preserve compatibility for dedicated deployment and add migration/remapping evidence where IDs change.

### Tests

- Import and backend tests for duplicate labels, stable IDs, unknown suppliers, process/hash instability, zero/missing cost and unit conversion.
- Cross-surface tests for supplier, inventory, Data Quality, report and export identity/value parity.
- Production-like reconciliation against source row counts and monetary totals.

### Acceptance

- Locations and suppliers remain uniquely identifiable even when names are equal or missing.
- No runtime hash is presented as a durable supplier identity.
- Zero/missing/invalid acquisition value cannot silently produce an actionable recommendation or healthy score.
- UI, API, export and workflow use the same canonical identity and currency-unit contract.

### Dependencies

- QDB/RQ owner must decide source authority and migration strategy.
- Coordinate with PROD-AN-04 and PROD-AN-05; frontend must not invent canonical IDs or corrected costs.

## PROD-AN-11 - Reconcile one canonical analytics context across surfaces

Status: WAITING

### Problem

Dashboard, Pilot Readiness, Data Quality, Product and Supplier surfaces present
different totals, denominators and readiness scores without a shared effective
context/version contract.

### Evidence

- Analytics Dashboard shows 686,400 RSD, 14 transactions and 140 units for its visible 31-day context.
- Pilot Readiness shows 836,350 RSD, 15 transactions and 145 units from dashboard bootstrap.
- Data Quality readiness is 77/100 while Pilot Readiness renders readiness 100 for an apparently related intake context.
- Product page renders 1,200 rows while readiness proves availability from `top=100`; supplier ordering also differs between surfaces.

### Scope

- Shared analytics request/effective-context metadata, cache keys/versioning, dashboard bootstrap, Pilot Readiness, Data Quality, Product, Supplier and focused reconciliation tests.
- One read-only reconciliation tool/report for production-like source fixtures.

### Read first

- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.
- Dashboard bootstrap, Pilot Readiness and Data Quality endpoint/services.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` completed scope/filter contracts.
- Current cache/materializer provenance contracts on remote main.

### Do

1. Define a canonical context stamp containing requested/effective period, whole-day semantics, data scope, store, supplier, source dataset version, materializer version, cache timestamp, source observation timestamp and row-limit semantics.
2. Return the stamp from every compared endpoint and preserve it through UI, report and export.
3. Build a deterministic reconciliation fixture that computes revenue, transactions, units, supplier ranking, readiness counts and product denominators once and compares every consumer.
4. When contexts differ, show the difference explicitly; never compare or blend them as if they were the same period.
5. Add a read-only production reconciliation command that emits differences without mutating caches or source data.

### Tests

- Backend integration tests using one shared sales/catalog/inventory fixture across all affected endpoints.
- Cache-key tests for period/scope/store/supplier/source-version/row-limit isolation.
- Frontend tests that render mismatch/partial states and preserve context in links, report and export.
- Production-like reconciliation with exact source-row and monetary-total evidence.

### Acceptance

- Equal context stamps produce equal revenue, transaction and unit totals across all applicable surfaces.
- Different contexts are visibly and machine-readably different.
- Readiness scores state their population, numerator, denominator and source version.
- Top/returned row limits cannot masquerade as total analyzed rows.

### Dependencies

- Owner-promoted RQ/OBS re-entry; completed RQ100-RQ105 are evidence, not proof that current production reconciles.
- Must precede Decision Pulse (`RQ106`) and UX work that republishes cross-surface totals.

## PROD-AN-12 - Repair dashboard schema drift and deployment compatibility

Status: WAITING

### Problem

Production Analytics Dashboard advanced metrics fail because the deployed database
does not provide the `p.DataOrigin` column expected by the query.

### Evidence

- Production UI reports PostgreSQL `42703: column p.DataOrigin does not exist`.
- Dashboard labels itself partial and advanced metrics are unavailable.
- Primary API readiness remains healthy, so generic `/ready` does not detect analytics schema incompatibility.

### Scope

- Advanced dashboard query/handler, owning migration/schema contract, deployment preflight/readiness diagnostics and focused tests.

### Read first

- Advanced dashboard endpoint/query and the model/table owning `DataOrigin`.
- Current database migration order and release scripts.
- Operational dashboard honesty contract delivered by `OBS10`.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.

### Do

1. Establish whether `DataOrigin` is a required migrated column, a stale query alias or an unsupported provider branch.
2. Apply the smallest forward-compatible migration/query repair; do not silently drop scope filtering.
3. Add an analytics schema compatibility preflight that reports required migration/version separately from generic API health.
4. Preserve partial/fail-closed UI semantics until compatibility is confirmed.
5. Record exact production migration and endpoint evidence for the deployed main SHA.

### Tests

- Provider-specific integration test executing the exact advanced query against the migrated schema.
- Migration-from-current-production-version test or documented dry-run.
- Readiness/diagnostic test for missing and present required schema.
- Production smoke for Dashboard advanced metrics and scope isolation.

### Acceptance

- No expected production query references a missing column.
- Scope/data-origin semantics remain explicit and tested.
- Analytics schema incompatibility is observable before users discover it in the Dashboard.
- Main deployment evidence identifies both application SHA and database migration/version.

### Dependencies

- STAB/QDB owner must confirm schema and deployment authority.
- Coordinate with PROD-AN-11 because removing `DataOrigin` filtering would create false reconciliation.

## PROD-AN-13 - Make price-leveling analysis causally honest

Status: WAITING

### Problem

Pre/Post Price Leveling displays large revenue changes and an elasticity value even
when availability, momentum, DiD/control evidence and most post-window coverage are
missing.

### Evidence

- Production reports -1,135,340 RSD and -91.6% with only 1% post-window coverage.
- Runtime states that rolling pre/post, momentum, OOS and DiD lookup are unavailable.
- Category is `N/A`; 10 of 11 suppliers are insufficient-data and the page still names strongest/weakest effects.

### Scope

- Price-leveling event contract, daily metric dataset, availability/OOS adjustment, control-group/DiD calculation, confidence/gating DTOs, page/report/export and focused tests.

### Read first

- Pre/Post Price Leveling page, API handler and SQL/materialized views.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.
- Existing nivelacija methodology and data-quality contracts.

### Do

1. Define the treatment event, immutable event date, pre/post windows and timezone/day boundaries.
2. Calculate daily units, revenue, realized price, cost coverage, margin contribution and profit proxy; do not evaluate only aggregate revenue.
3. Separate price effect from availability using in-stock days, opening/inbound stock, OOS exclusions and explicit availability coverage.
4. Introduce a documented control group and DiD/event-study result; record assumptions, sample size and pre-trend checks.
5. Define minimum pre/post coverage and confidence thresholds. When unmet, show descriptive association only and suppress elasticity, strongest-effect and causal recommendation language.
6. Preserve seasonality/day-of-week and category/supplier segmentation without using `N/A` as a ranked category.

### Tests

- Deterministic fixtures for price-only, availability-only, simultaneous price/OOS, no-event, insufficient-window, seasonality and control-group cases.
- Formula tests for ratio/percent units, margin/profit and DiD numerator/denominator.
- UI/export tests proving low coverage cannot display causal claims or actionable pricing guidance.
- Backtest against a historical production-like window with documented exclusions.

### Acceptance

- Price, availability and baseline/control effects are separately visible.
- No causal or elasticity claim is shown without sufficient post coverage and valid denominator/control evidence.
- True zero, no sale due to OOS, missing history and unavailable control remain distinct.
- Reported business impact includes margin/profit context, not revenue alone.

### Dependencies

- Requires canonical observed inventory/runtime provenance from completed RQ96 plus PROD-AN-06 verification.
- PROD-AN-11 context reconciliation must define the common period/source version.

## PROD-AN-14 - Restore real action measurement and outcome value

Status: WAITING

### Problem

Central Actions cannot demonstrate recommendation value because production
measurement statistics are missing, no outcome is measured and the current sample
contains only synthetic smoke rows.

### Evidence

- Production reports `missing_statistics`, four actions, one closed action, zero measured outcomes and 0% closed-action coverage.
- All four listed actions are named smoke artifacts.
- Existing RL/DT contracts correctly say acceptance is not success, but current production has no real measured sample.

### Scope

- Action execution/outcome capture, measurement-statistics projection/materializer, Central Actions API/UI and focused tests.
- Synthetic isolation itself remains owned by PROD-AN-09.

### Read first

- Completed RL09/RL10 and decision timeline/outcome contracts on current remote main.
- Central action/outcome entities, statistics projection and `AnalyticsActionsPage`.
- `docs/qa/ANALYTICS_PRODUCTION_VALUE_AUDIT_2026-08-19.md`.

### Do

1. After synthetic isolation, trace one real action from recommendation through accepted/rejected, executed, review-due, measured and outcome states.
2. Materialize `measurementStatistics` from authoritative executed+measured evidence only.
3. Define every rate with numerator, denominator, eligibility population, period and minimum sample size.
4. Add operator workflow for recording/verifying outcome without converting acceptance or closure into success.
5. Show the next missing step when statistics are unavailable and exclude synthetic actions from all rates/exports/learning.

### Tests

- Lifecycle tests for issued, accepted, executed, closed-unmeasured, measured-positive, measured-negative and rejected cases.
- Projection tests for zero eligible, missing statistics, small sample and synthetic exclusion.
- Frontend tests for unavailable, pending measurement and measured states.
- One production-like end-to-end fixture with no external mutation.

### Acceptance

- Central Actions can explain why statistics are unavailable or show rates derived only from eligible measured outcomes.
- At least one real fixture proves recommendation-to-measured-outcome lineage end to end.
- Acceptance, execution, closure and success remain distinct.
- Synthetic data cannot enter action counts, statistics, exports or learning.

### Dependencies

- PROD-AN-09 must isolate/quarantine smoke data first.
- Owner-promoted RL/RQ re-entry; completed RL contracts remain authoritative.
