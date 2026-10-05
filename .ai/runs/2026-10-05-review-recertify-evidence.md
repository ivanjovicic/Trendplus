# Review recertify 2026-10-05

Task ID: review-recertify-2026-10-05
Queue: direct-user-request
Date: 2026-10-05
Agent/tool: Cursor Cloud
Delivery target: main
Working branch / PR: `cursor/review-recertify-daily-e050`, direct delivery to main
Main commit SHA: pending post-push verification
Main verification: pending
Evidence state: implementation recorded; SHA sync follows push

## What was done

- Refetched `origin/main` at `dfdaf5a5` before edits. Today's C#/React commits through the RQ588 claim were already on that SHA, including the earlier Daily Sales half-open HTTP path, startup `[FromServices]` binding, and reorder revenue/cost split. During delivery, `origin/main` advanced to `4ed7012d` (RQ588 migration-discovery test). This branch was rebased onto that SHA with no conflicts.
- Recertified those areas against current code. Remaining defect: `DailySalesStatsService` still expanded an equal `from`/`to` into a one-day inclusive window unless `requestedToIsExclusive` was set. Direct service tests and receipt reconciliation still used that old window.
- Service now treats `requestedToUtc` as the exclusive end. Tests that previously passed the last included day now pass the next midnight. A new service test proves a sale exactly at `2026-06-16T00:00` is outside `[2026-06-15, 2026-06-16)`.
- Daily Sales meta sets `dateBoundaryConvention=half_open_utc` before source freshness. Freshness reads `meta.DateBoundaryConvention` before the context fallback, so a half-open label uses `DatumProdaje < requestedToUtc`.
- Date-only query strings still expand the named calendar day into that exclusive end, and the response convention is `half_open_utc` rather than a second inclusive label.
- Concrete Minimal API service parameters that the binder inferred as a body (`AnalyticsCacheAdminService`, `OperationsAnalyticsIntegrityRegistry`) are `[FromServices]`. `StartupReadinessState` was already `[FromServices]` on `main`. Narrow admin-repair hosts register the cache admin service.
- Inventory valuation aggregate returns `null` total value when stock exists but no unit cost is known. Insights tests expect `estimated_from_sale_cost` from the sale-line `NabavnaCena`, matching `InventoryValuationAndAgingPolicy`.
- Reorder presentation test covers revenue `null` with procurement cost `2000`: revenue stays `N/D`. `ReorderPlan` import restored so typecheck compiles.
- RQ49 marked DONE because the RQ591 field split plus this presentation proof meets its acceptance. RQ48 summary row corrected from READY to DONE to match its section. RQ588 remains the canonical IN_PROGRESS owner and was not taken.

## Files changed

- `Api/Services/DailySalesStatsService.cs`
- `Api/Services/OperationsSourceFreshnessService.cs`
- `Api/Endpoints/DailySalesStatsEndpoints.cs`
- `Api/Endpoints/AdminRepairEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Application/Analytics/InventoryValuationAndAgingPolicy.cs`
- `Api.Tests/DailySalesStatsServiceTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/DailySalesReceiptReconciliationTests.cs`
- `Api.Tests/AdminRepairAuthorizationTests.cs`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Api.Tests/InventoryValuationAndAgingPolicyTests.cs`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/pages/__tests__/insightStudioTrustPresentation.spec.ts`
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-05-review-recertify-evidence.md`

## Validation run

- Focused Api.Tests filter (Daily Sales service/integration/receipt, freshness, startup/admin readiness) -> pass, 96/96.
- Follow-up filter (admin repair, cache invalidate, critical routes, inventory insights/balance, valuation policy) -> pass, 23/23.
- Frontend focused vitest (trust presentation, derived metrics, supplier open-action keys, Daily Sales premium) -> pass, 49/49.
- `npm run typecheck` and `npm run check:analytics-guardrails` -> pass after restoring the missing `ReorderPlan` import and retargeting drifted baseline lines.
- Frontend production build -> pass (chunk-size warning only).
- After the RQ48 summary sync: `git diff --check`, `node scripts/check-prompt-queues.mjs` (709 tasks), `node scripts/check-agent-instructions.mjs` (14 files) and `node scripts/check-planning-architecture.mjs` (80 tasks) -> pass.
- Release re-run of Daily Sales, freshness, startup/admin readiness, admin repair, critical route mappings and valuation policy -> pass, 113/113.
- Full `Api.Tests` on this environment -> 1817 passed, 40 skipped, 23 failed before the binding/valuation repairs. After those repairs the named product failures in that set were re-run green. Remaining full-suite failures that were not product regressions of this pass:
  - RQ561 six-screen and nivelacija event normalization: PostgreSQL Testcontainers fixture is not available in this environment. The failure text is the missing container, not a Daily Sales period assertion.
  - `AdminDataSourceEndpointsTests`: `The ConnectionString property has not been initialized` (SqlClient). Environment.
  - `SimulateScenarios_WithReliableEqualCost_KeepsGenuineZeroMargin`: markdown expected margin is about -832 because the existing discount goes below an equal purchase price. Scorer commits predate this review and were not changed. Genuine zero highlight margin stays 0; the markdown assertion was not rewritten to hide the negative result.

## Validation not run

- Live Render/provider probes. Not a start gate for this repository-local recertification.
- Full `Api.Tests` was not repeated after the binding/valuation repairs and the clean rebase onto `4ed7012d`. The previously red product filters were re-run green (23/23). RQ588's new discovery test was not modified.

## Documentation impact

- RQ48 summary and detail status are both DONE.
- RQ49 summary and detail status are both DONE.
- Main queue primary remains RQ588 IN_PROGRESS.
- Supplemental UI READY pointer remains P-UI-39, with collision-safe lanes P-UI-40, P-UI-41, P-UI-47, P-UI-49 and P-UI-52.

## What was missed

- Date-only HTTP `toDate=2026-01-02` still includes that calendar day by expanding to the next exclusive instant. Timestamp `toDate=2026-01-02T00:00:00Z` stays exclusive. The stored convention for both is now `half_open_utc`. UI calendar `Do` stays inclusive and the client sends the next exclusive instant.

## Risks

- Any unpublished caller that passed an inclusive last day without adding one day will now drop that day. In-repo callers were updated.

## Post-close routing recovery

- Recovery base before this delivery: `4ed7012d`.
- Active queue headers scanned: main RQ queue, Operations, UI/table, Advanced, Legacy, Action Outcome, Executive/DQ, Nivelacija, Inventory signals, Test hardening, Supplier audit, Cross-surface, SQL, UI premium, Stabilization, Backend CI, Data source connector, Multitenancy, and `MASTER_ROADMAP.md`.
- No newly dependency-complete RQ prompt was promoted. RQ588 stays IN_PROGRESS under its existing claim.
- Historical audit snapshots (`docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md`, UX audit) still list RQ586/RQ587/RQ588 as READY at audit time. Those files are not the live router.

## Next

- Leave RQ588 to its existing claim.
- After push, record the exact `origin/main` SHA and the Actions state for that SHA.
