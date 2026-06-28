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

- Product cards compute expected impact as `row.ExpectedImpactRsd ?? (row.LostSalesEstimate > 0m ? row.LostSalesEstimate : null)`.
- Product Decision Center itself calculates expected impact by recommendation type: lost sales for `REPLENISH`/`BOOST`; slow-stock capital for `MARKDOWN`/`DO_NOT_ORDER`; otherwise null.

Risk:

- A `FIX_DATA`, `INSUFFICIENT_DATA`, `MARKDOWN` or `DO_NOT_ORDER` row can receive `LostSalesEstimate` as expected impact in the board even when Product Decision Center intentionally left `ExpectedImpactRsd` null.
- This can pollute `impact` and `urgent` sections.

Classification: likely bug.

Recommended prompt: RQ01.

### R02 - Product Decision Center summary mixes top-limited counts with all-row money totals

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `sortedRows` is `rows.Take(top)`.
- Count KPIs in `Summary` are calculated from `sortedRows`.
- `LostSalesEstimate` and `SlowStockCapital` are accumulated over all `rows` before top-limiting.

Risk:

- The summary can show counts for visible/top rows but money totals for the entire analyzed set.
- If this is intended, the API contract must say “all analyzed rows”. If not, the numbers are inconsistent.

Classification: suspicious; needs contract decision.

Recommended prompt: RQ02.

### R03 - Lost-sales validation can mark unavailable/unknown evidence as `good`

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- Lost-sales validation marks status as `good` when `lostSalesEstimate <= 0`.
- The lower-level snapshot can return `(0, 0m)` when connection/query evidence is unavailable.

Risk:

- “Cannot compute lost sales” can be displayed as “no significant lost sales”.
- This is high-risk for replenishment/OOS decisions.

Classification: likely bug / trust-contract bug.

Recommended prompt: RQ03.

### R04 - Data Quality health can look excellent/good when there is no revenue evidence

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- Missing-cost and unknown-supplier share percentages become `0` when `totalRevenue == 0`.
- Decision Board evaluates data-quality health from percentage/count penalties and can produce excellent/good-like score if there are no penalties.

Risk:

- A no-sales/no-revenue window can be interpreted as clean data quality rather than `insufficient_data`.

Classification: suspicious; needs test.

Recommended prompt: RQ04.

### R05 - `dataScope` semantics differ between analytics modules

Files:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

Observed:

- Some sales helpers filter `dataScope` by sale header `p.DataOrigin` / `pz.DataOrigin`.
- Product Decision Center first filters articles by `a.DataOrigin`, then sales by sale header `pz.DataOrigin`.
- Data Quality health filters the sales window using article `a.DataOrigin`.

Risk:

- The same dashboard request can mean different datasets depending on the section.
- Imported/existing comparisons can be inconsistent.

Classification: suspicious; high value to audit before any data trust claim.

Recommended prompt: RQ05.

### R06 - Data Quality top offenders 30d sales impact ignores sale-level dataScope

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- `sales_30d` CTE groups all sales by article without `dataScope` predicate.
- `quality_source` later filters articles by `a.DataOrigin`.

Risk:

- For `imported` or `existing` scopes, revenue impact can include sales rows outside the requested scope if sale header source and article source disagree.

Classification: likely bug or contract gap.

Recommended prompt: RQ06.

### R07 - Top offenders do not support missing-cost issue type even though health tracks missing-cost revenue

File: `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`

Observed:

- Health snapshot tracks `MissingCostRevenue` and `MissingCostRevenueSharePct`.
- Top offender issue type normalization supports `missingSupplier`, `missingShoeType`, `invalidName`; any other value defaults to `missingSupplier`.

Risk:

- UI/operator can see missing-cost risk but may not be able to drill down into the exact products causing it.
- If a caller passes `missingCost`, it silently returns missing supplier offenders.

Classification: likely product/data-quality bug.

Recommended prompt: RQ07.

### R08 - Decision Board supplier cards can still rank blocked supplier recommendations highly

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Supplier cards are still built from top grow/top risk suppliers even when `RecommendationAllowed=false`.
- Priority subtracts a penalty, but still adds revenue, confidence and recommendation bias.
- A separate blocker card is also added.

Risk:

- A blocked supplier signal can still appear as a high-ranking supplier card, which can look actionable if the UI emphasis is not strict enough.

Classification: suspicious; should be locked with tests.

Recommended prompt: RQ08.

### R09 - Analytics actions source state treats zero actions as `insufficient_data`

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Source state for `analytics-actions` is `insufficient_data` when `actions.Count == 0`.

Risk:

- A genuinely empty action list can be interpreted as a data-quality problem.
- This can produce unnecessary warnings and lower board trust.

Classification: suspicious; contract decision needed.

Recommended prompt: RQ09.

### R10 - Inventory card confidence is derived from workflow status, not evidence coverage

File: `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed:

- Inventory confidence level is mapped from workflow item status (`approved`, `deferred`, else `insufficient_data`).
- Data quality is `warning` except `pending` -> `insufficient_data`.

Risk:

- Workflow status may not reflect evidence quality, source freshness or calculation confidence.
- Inventory cards can be treated as equally reliable even when underlying velocity/stock evidence differs.

Classification: suspicious; needs evidence contract.

Recommended prompt: RQ10.

### R11 - Transaction stats may use line count where UI label implies item/unit count

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `AvgItemsPerTransaction` uses grouped row count (`g.Count()`), not sum of sold units.

Risk:

- If UI/product meaning is “items/units per receipt”, current value may count distinct sale lines instead of units.
- If the intended meaning is “lines per transaction”, label should be changed.

Classification: suspicious semantic bug.

Recommended prompt: RQ11.

### R12 - Product Decision Center confidence/impact summary should be tested against top limit and ignored rows

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

Observed:

- `TotalRows`, `AnalyzedRows`, `IgnoredRowsCount`, summary counts and summary totals are not obviously tied to the same denominator.

Risk:

- Operators may compare counts and totals incorrectly.
- “Ignored rows” can be interpreted as bad data even when rows are only hidden by `top`.

Classification: contract gap.

Recommended prompt: RQ12.

## Priority order

1. RQ01 - Decision Board expected-impact correctness.
2. RQ03 - Lost-sales unavailable vs true zero.
3. RQ04 - Data Quality no-revenue/no-data status.
4. RQ05/RQ06 - dataScope consistency and top-offender scope correctness.
5. RQ07 - missing-cost offender drilldown.
6. RQ08 - blocked supplier signal ranking.
7. RQ02/RQ12 - product summary denominator contract.
8. RQ09/RQ10/RQ11 - board/source-state and semantic polish.

## Checks to add across prompts

- Unit tests for every reliability contract.
- No broad formula rewrite without before/after fixture.
- Explicit `dataQualityStatus`, `sourceStatus`, `warningCode` or `emptyReason` whenever data is missing or helper/fallback is used.
- Keep `RecommendationAllowed=false` visually and programmatically separate from actionable recommendations.
