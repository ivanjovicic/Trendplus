# Analytics dataScope Consistency Audit

Date: 2026-08-04  
Repo: `ivanjovicic/Trendplus`  
Prompt: RQ05  
Status: docs/tests matrix only — no runtime semantics change

## Goal

Make `imported` / `existing` / `all` semantics visible and testable across analytics modules before patching endpoints.

## NormalizeDataScope (current)

Multiple private copies exist (`CachedAnalyticsEndpoints`, `DecisionBoardEndpoints`, `SupplierDecisionHubEndpoints`, `AnalyticsDataQualityHealthService`, cache keys, etc.). Behavior is consistent:

- null / blank / unknown → `"all"`
- only `"imported"` and `"existing"` are special

Canonical predicates when applied:

- `imported` → `DataOrigin == "access"`
- `existing` → `DataOrigin == "existing" OR NULL OR ""`
- `all` → no origin predicate (sometimes uses unscoped MV/views)

## Prepared default (until a surface opts out in meta/UI)

If list / detail / export / action are presented as the same view, they must share the same `dataScope`, `storeId`, `supplierId`, date range and search scope. Intentional mixes must expose metadata.

## Canonical rules (proposed, not yet enforced)

| Metric family | Desired entity/sales rule |
|---|---|
| Sales revenue (dashboard totals, daily, breakdowns, velocity, Pareto, PDC sales/trend) | Sale header `ProdajaZaglavlja.DataOrigin` |
| Article quality (DQ orphans/issues/offender membership, PDC row eligibility, supplier article set) | Article `Artikli.DataOrigin` |
| Inventory stock (on-hand, insights, workflow) | Article `Artikli.DataOrigin`, or explicitly `forced_all` in meta/UI |
| Supplier scoring | Article origin for eligibility; period sales/returns should follow sales-header rule or document article-only |
| Lost-sales | OOS universe = article; recent velocity/price = sale header (dual OK if documented); validation must pass request scope |

## Matrix

| Surface | Endpoint/query | Entity scope | Sales scope | Store scope | Supplier scope | Current behavior | Desired behavior | Risk | Follow-up prompt |
|---|---|---|---|---|---|---|---|---|---|
| Cached dashboard sales summary / daily / category / gender / supplier / weekday / hour / payment | `CachedAnalyticsEndpoints` builders | n/a | sale header | yes | yes | Header `DataOrigin` | Keep sales-header | Low if UI labels “opseg prodaje” | — |
| Top products advanced / velocity / Pareto | same | article join for attrs | sale header | yes | yes | Header scope; view path when `all` | Keep sales-header | Medium (view forced-all) | Q81 |
| Lost-sales snapshot | `GetLostSalesSnapshotAsync` | article (OOS universe) | sale header (recent CTE) | yes | yes | Dual; validation often forced `all` | Dual OK + pass request scope | High on validation path | RQ05-F4 / Q81 |
| Lost-sales validation endpoint | `/validation/lost-sales` | forced all | forced all | no | no | Defaults `dataScope=all` | Honor request scope | High fake-green risk if scoped UI | RQ05-F4 |
| Product Decision Center | `BuildProductDecisionCenterAsync` | article | sale header | yes | yes | **Dual-origin with explicit provenance contract** | Expose requested/effective scope and provenance, or align sales to header with article membership | Medium | RQ119 |
| PDC inventory journal signals | `LoadInventorySignalWindowStatsFromJournalAsync` | journal `dataScope` | n/a | store | n/a | `dataScope` now filters `DnevnikPromena` rows and is threaded into the cache key | Keep journal scope aligned with request scope | Low | RQ05-Journal — resolved 2026-08-27 |
| Inventory insights / store comparison (cached) | `/inventory/insights` | article scope | article scope | yes | yes | `dataScope` now passed through to the builder and cache key | Keep article scope, preserve metadata only for genuine fallbacks | Low | RQ05-F2 — resolved 2026-08-27 |
| Decision Board inventory cards | `DecisionBoardEndpoints` | article scope | article scope | yes | yes | Inventory workflow now inherits article `dataScope` from the shared inventory builder | Keep the shared article scope contract | Low | RQ05-F2 — resolved 2026-08-27 |
| Decision Board PDC / supplier / DQ health | same | passed | passed | yes | yes | Scope forwarded | Keep | Low | — |
| Supplier decision hub rows | `BuildRowFilters` | article | period sales headers unscoped | yes | yes | Article eligibility; sales/returns indirect | Document or add header scope | Medium | Q81 |
| DQ health Capture | `CaptureAsync` | article (orphans + sales join) | via article | n/a | n/a | Revenue shares by article origin | Keep article for quality; document sales-by-article | Medium | — |
| DQ top offenders | `GetTopOffendersAsync` | article (`quality_source`) | sale header (`sales_30d`) | n/a | n/a | **RQ06 DONE**: header-scoped sales + article membership | Keep | Was P0 cross-scope leak | RQ06 DONE |
| DQ issues handler | `GetDataQualityIssuesHandler` | article | sale-header-scoped `sales_30d` | n/a | n/a | Same pre-RQ06 CTE pattern; RQ118 closed the residual | Keep the RQ06 sales-header rule | Resolved in RQ118 | RQ118 |
| ShoeType / Color list vs detail (FE) | clientapp pages | varies | varies | varies | n/a | List omits `dataScope`; detail URL includes it | Same query lineage | High UX | RQ53/RQ54 |
| Vendor / supplier pre-post pages (FE) | clientapp | API supports | API supports | API supports | — | Page may not pass store/scope | Pass or hide controls | Medium | RQ53/RQ54 |

## Top risks (evidence)

1. **DQ top-offender `sales_30d` unscoped** — **fixed in RQ06**: `TopOffendersSql` now filters sale-header `DataOrigin`. Contract: `DataScopeConsistencyContractTests`. Residual resolved in RQ118: `GetDataQualityIssuesHandler` now filters the same sale-header scope.
2. **PDC inventory journal signals** — `LoadInventorySignalWindowStatsFromJournalAsync` now filters journal rows by request `dataScope`, so the remaining gap is no longer this family.
3. **PDC dual-origin is explicit, not silent** — Product Decision Center now exposes requested scope plus dual-origin provenance on the response, and inventory/Decision Board now honor article scope as well.

## Follow-up prompts

| ID | Title | Depends on |
|---|---|---|
| RQ06 | DQ top-offender / issues `sales_30d` scope correctness | RQ05 (this audit) — **offenders DONE 2026-08-04**; issues handler residual closed by RQ118 |
| RQ05-F1 | PDC mismatched article vs sale-header origin contract/tests (+ optional align) | RQ05 — resolved by RQ119 |
| RQ05-F2 | Inventory + Decision Board apply article `dataScope` or explicit forced-all meta | RQ05 — resolved 2026-08-27 |
| RQ05-F3 | ShoeType/Color/Vendor list↔detail↔export scope lineage (overlaps RQ53/RQ54) | RQ05 |
| RQ05-F4 | Lost-sales validation/bootstrap honor request `dataScope` | RQ05 / RQ03 |
| Q81 | SQL helper store/supplier/`dataScope` consistency | Q69; reuse this matrix |

## Tests added in RQ05

- `Api.Tests/DataScopeConsistencyContractTests.cs` — locks current offender SQL split (unscoped `sales_30d` + scoped `quality_source`) so RQ06 cannot regress silently the wrong way without updating the contract.

## Non-goals (this prompt)

- No endpoint runtime filter rewrite
- No frontend routing/UI redesign
- No SQL formula weight changes
