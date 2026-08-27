# Analytics Data Reliability Audit

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: documentation-only reliability audit

## Goal

Find possible inaccuracies, data-quality bugs and trust-contract gaps in Trendplus analytics. This audit is intentionally stricter than a normal code review: anything that can make weak/unknown data look confident, green, high-impact or business-ready is treated as a reliability risk.

No runtime code was changed in this pass.

## Reliability principles

1. Unknown must not become zero.
2. No-data must not become green.
3. Helper/fallback data must be visibly marked as helper/fallback.
4. Expected impact must only be attached to the recommendation type it belongs to.
5. Dashboard/board composition must not upgrade a blocked signal into an actionable decision.
6. The same filter set (`dataScope`, store, supplier, date range) must mean the same thing across analytics modules.
7. Aggregated totals must clearly say whether they describe all analyzed rows, returned top rows or visible UI rows.

## Findings

### R01 - Decision Board can assign lost-sales impact to product cards outside the intended recommendation type

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Product cards previously computed expected impact as `row.ExpectedImpactRsd ?? (row.LostSalesEstimate > 0m ? row.LostSalesEstimate : null)`.
- Product Decision Center itself calculates expected impact by recommendation type: lost sales for `REPLENISH`/`BOOST`; slow-stock capital for `MARKDOWN`/`DO_NOT_ORDER`; otherwise null.

Risk:

- A `FIX_DATA`, `INSUFFICIENT_DATA`, `MARKDOWN` or `DO_NOT_ORDER` row could receive `LostSalesEstimate` as expected impact in the board even when Product Decision Center intentionally left `ExpectedImpactRsd` null.
- This could pollute `impact` and `urgent` sections.

Classification: fixed in RQ01 (2026-08-04).

Fix:

- Board product cards now use only `row.ExpectedImpactRsd`.
- Lost-sales / slow-stock values are not reattached at board composition time.
- Regression coverage lives in `Api.Tests/DecisionBoardEndpointsTests.cs`.

Recommended prompt: RQ01 (DONE).

### R02 - Product Decision Center summary mixes top-limited counts with all-row money totals

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `sortedRows` is `rows.Take(top)`.
- Count KPIs in `Summary` are calculated from `sortedRows`.
- `LostSalesEstimate` and `SlowStockCapital` are accumulated over all `rows` before top-limiting.

Risk:

- The summary can show counts for visible/top rows but money totals for the entire analyzed set.
- If this is intended, the API contract must say “all analyzed rows”. If not, the numbers are inconsistent.

Classification: contract decided in RQ02 (2026-08-04).

Contract decision (before/after):

- BEFORE: same numeric split, undocumented.
- AFTER: numeric behavior unchanged; denominators are explicit.
  - Count KPIs (`ReplenishCount`, `MarkdownCount`, `HighPotentialCount`, `BadDataCount`) → `countDenominatorScope = returned_rows`.
  - Money totals (`LostSalesEstimate`, `SlowStockCapital`) → `moneyDenominatorScope = analyzed_rows`.
  - `IgnoredRowsCount` → `ignoredRowsMeaning = hidden_by_top_limit` (not bad data).
- Helpers: `BuildProductDecisionCenterSummary`, `BuildProductDecisionCenterRowWindow`.
- Tests: `ProductDecisionCenterSummaryDenominatorTests`, top-limit case in `ProductDecisionCenterBuilderIntegrationTests`.

Recommended prompt: RQ02 (DONE). RQ12 remains for richer ignored-row UX labeling if needed.

### R03 - Lost-sales validation can mark unavailable/unknown evidence as `good`

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- Lost-sales validation marks status as `good` when `lostSalesEstimate <= 0`.
- The lower-level snapshot can return `(0, 0m)` when connection/query evidence is unavailable.

Risk:

- “Cannot compute lost sales” can be displayed as “no significant lost sales”.
- This is high-risk for replenishment/OOS decisions.

Classification: fixed in RQ03 (2026-08-04).

Fix:

- Snapshot now returns `SourceStatus`: `view` | `fallback` | `unavailable` | `true_zero`.
- `unavailable` → `insufficient_data`, `LostSalesEstimate = null`.
- Trusted view zero → `true_zero` / `good`.
- Fallback zero → `warning` (not green).
- Contract doc: `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`.
- Tests: `Api.Tests/LostSalesValidationSourceStatusTests.cs`.
- Q80 should reuse this vocabulary (not invent a second model).

Recommended prompt: RQ03 (DONE).

### R04 - Data Quality health can look excellent/good when there is no revenue evidence

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- Missing-cost and unknown-supplier share percentages become `0` when `totalRevenue == 0`.
- Decision Board evaluates data-quality health from percentage/count penalties and can produce excellent/good-like score if there are no penalties.

Risk:

- A no-sales/no-revenue window can be interpreted as clean data quality rather than `insufficient_data`.

Classification: fixed in RQ04 (2026-08-04).

Fix:

- Snapshot adds `HasRevenueEvidence` (`TotalRevenue > 0`).
- `EvaluateDataQualityHealth` returns `insufficient_data` (score 0) when there is no revenue evidence — never `excellent`/`good`.
- Decision Board shows a data-quality blocker and `no_revenue_evidence` warning code.
- Tests: `AnalyticsDataQualityHealthServiceTests`, `DecisionBoardDataQualityHealthEvaluationTests`.

Recommended prompt: RQ04 (DONE). RQ75 remains the DataQualityPage surface companion.

### R05 - `dataScope` semantics differ between analytics modules

Files:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Some sales helpers filter `dataScope` by sale header `p.DataOrigin` / `pz.DataOrigin`.
- Product Decision Center first filters articles by `a.DataOrigin`, then sales by sale header `pz.DataOrigin`.
- Data Quality health filters the sales window using article `a.DataOrigin`.
- Top-offender `sales_30d` CTE is unscoped while article membership is scoped.
- Inventory/Decision Board accept or show scope but often force `all`.

Risk:

- The same dashboard request can mean different datasets depending on the section.
- Imported/existing comparisons can be inconsistent.

Classification: audited in RQ05 (2026-08-04); runtime fixes deferred.

Evidence:

- Matrix + canonical rules: `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
- Contract test locking offender SQL split: `Api.Tests/DataScopeConsistencyContractTests.cs`

Recommended prompts: RQ06 (offenders), RQ05-F4 (lost-sales validation/bootstrap), RQ53/RQ54 (FE lineage), Q81 (SQL helpers). PDC dual-origin is explicitly covered by RQ119 and inventory, Decision Board, and journal-signal scope are resolved by RQ05-F2 / RQ05-Journal.

### R06 - Data Quality top offenders 30d sales impact ignores sale-level dataScope

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- `sales_30d` CTE grouped all sales by article without `dataScope` predicate.
- `quality_source` later filters articles by `a.DataOrigin`.

Risk:

- For `imported` or `existing` scopes, revenue impact can include sales rows outside the requested scope if sale header source and article source disagree.

Classification: fixed in RQ06 (2026-08-04) for top offenders.

Fix:

- `sales_30d` now filters by sale-header `DataOrigin` (RQ05 sales-revenue rule).
- Article membership remains article-origin scoped.
- `dataScope=all` unchanged (includes all headers).
- Residual resolved in RQ118: `GetDataQualityIssuesHandler` now uses the same sale-header scope.

Recommended prompt: RQ06 (DONE for offenders).

### R07 - Top offenders do not support missing-cost issue type even though health tracks missing-cost revenue

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- Health snapshot tracks `MissingCostRevenue` and `MissingCostRevenueSharePct`.
- Top offender issue type normalization supports `missingSupplier`, `missingShoeType`, `invalidName`; any other value defaults to `missingSupplier`.

Risk:

- UI/operator can see missing-cost risk but may not be able to drill down into the exact products causing it.
- If a caller passes `missingCost`, it silently returns missing supplier offenders.

Classification: fixed in RQ07 (2026-08-04) for top offenders.

Fix notes:

- `missingCost` top-offender filter uses article `NabavnaCena` null/≤0.
- Unknown issue types rejected (400 / throw); issues-list `Normalize` still silently defaults (R80 residual for list/UI tabs).

Recommended prompt: RQ07 (DONE for offenders).

### R08 - Decision Board supplier cards can still rank blocked supplier recommendations highly

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Supplier cards are still built from top grow/top risk suppliers even when `RecommendationAllowed=false`.
- Priority subtracts a penalty, but still adds revenue, confidence and recommendation bias.
- A separate blocker card is also added.

Risk:

- A blocked supplier signal can still appear as a high-ranking supplier card, which can look actionable if the UI emphasis is not strict enough.

Classification: fixed in RQ08 (2026-08-04).

Fix notes:

- Blocked cards: `insufficient_data` confidence/DQ, priority ≤40, ImpactScore 0, `signal_check` source key / summary.
- Excluded from `urgent` and `impact`; retained in `supplierRisk` + `blocker-supplier-trust`.

Recommended prompt: RQ08 (DONE).

### R09 - Analytics actions source state treats zero actions as `insufficient_data`

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Source state for `analytics-actions` is `insufficient_data` when `actions.Count == 0`.

Risk:

- A genuinely empty action list can be interpreted as a data-quality problem.
- This can produce unnecessary warnings and lower board trust.

Classification: fixed in RQ09 (2026-08-04).

Fix notes / contract:

- Successful empty load → status `good`, no `no_actions` warning; message explains healthy empty.
- `analytics_actions_unavailable` load warning → status `insufficient_data`.
- Cross-source "expected actions missing" not auto-warned (future).

Recommended prompt: RQ09 (DONE).

### R10 - Inventory card confidence is derived from workflow status, not evidence coverage

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Inventory confidence level is mapped from workflow item status (`approved`, `deferred`, else `insufficient_data`).
- Data quality is `warning` except `pending` -> `insufficient_data`.

Risk:

- Workflow status may not reflect evidence quality, source freshness or calculation confidence.
- Inventory cards can be treated as equally reliable even when underlying velocity/stock evidence differs.

Classification: resolved in RQ10 + RQ13 (2026-08-05).

Fix notes:

- Contract: `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
- RQ10: workflow-only fallback; no `medium`/`high` without evidence; warning `confidence_workflow_status_only`.
- RQ13: optional signal fields on `InventoryActionSuggestionDto`; board maps `ConfidenceLevel`/`ConfidenceScore`/`ReliabilityPct` from evidence when present; `RecommendationAllowed == false` caps at `insufficient_data`.

Recommended prompt: RQ13 (DONE); queue complete.

### R11 - Transaction stats may use line count where UI label implies item/unit count

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `AvgItemsPerTransaction` uses grouped row count (`g.Count()`), not sum of sold units.

Risk:

- If UI/product meaning is “items/units per receipt”, current value may count distinct sale lines instead of units.
- If the intended meaning is “lines per transaction”, label should be changed.

Classification: fixed in RQ11 (2026-08-05).

Fix notes:

- Contract: `docs/qa/TRANSACTION_STATS_SEMANTICS_CONTRACT.md`
- `avgItemsPerTransaction` = sale lines (matches dashboard *Stavki po transakciji*); behavior unchanged.
- Added `avgUnitsPerTransaction` = average sold units per receipt.

Recommended prompt: RQ11 (DONE).

### R12 - Product Decision Center confidence/impact summary should be tested against top limit and ignored rows

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `TotalRows`, `AnalyzedRows`, `IgnoredRowsCount`, summary counts and summary totals are not obviously tied to the same denominator.

Risk:

- Operators may compare counts and totals incorrectly.
- “Ignored rows” can be interpreted as bad data even when rows are only hidden by `top`.

Classification: fixed in RQ12 (2026-08-05); implementation landed in RQ02, contract locked here.

Fix notes:

- Contract: `docs/qa/PDC_IGNORED_ROWS_CONTRACT.md`
- `ignoredRowsMeaning = hidden_by_top_limit`; `badDataCount` uses returned rows only; money totals use analyzed rows.

Recommended prompt: RQ12 (DONE).

## Priority order

1. RQ01 - Decision Board expected-impact correctness. (DONE 2026-08-04)
2. RQ03 - Lost-sales unavailable vs true zero. (DONE 2026-08-04)
3. RQ04 - Data Quality no-revenue/no-data status. (DONE 2026-08-04)
4. RQ05/RQ06 - dataScope consistency and top-offender scope correctness. (RQ05 DONE; RQ06 DONE 2026-08-04; issues-handler residual RQ06-F1)
5. RQ07 - missing-cost offender drilldown. (DONE 2026-08-04; issues-list/UI residual R80)
6. RQ08 - blocked supplier signal ranking. (DONE 2026-08-04)
7. RQ09 - analytics actions empty-state contract. (DONE 2026-08-04)
8. RQ10 - inventory evidence confidence contract. (DONE 2026-08-04; wiring RQ13 DONE 2026-08-05)
9. RQ11 - transaction item/line/unit semantics. (DONE 2026-08-05)
10. RQ12 - PDC ignored/top rows contract. (DONE 2026-08-05)
11. RQ13 - wire inventory signal evidence onto board cards. (READY)

## Checks to add across prompts

- Unit tests for every reliability contract.
- No broad formula rewrite without before/after fixture.
- Explicit `dataQualityStatus`, `sourceStatus`, `warningCode` or `emptyReason` whenever data is missing or helper/fallback is used.
- Keep `RecommendationAllowed=false` visually and programmatically separate from actionable recommendations.
