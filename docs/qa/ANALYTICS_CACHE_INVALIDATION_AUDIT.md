# Analytics Cache Invalidation Audit

Date: 2026-06-17
Scope: analytics cache invalidation after import and refresh

## Summary

This audit confirms that the main pilot-facing cache families already have explicit invalidation after:

- successful Access import completion
- Access import batch delete cleanup
- successful `NightlyAnalyticsRefreshWorker` completion
- successful `AnalyticsDataQualityHealthWorker` completion
- manual admin cache clear

The main remaining gap was:

- `AnalyticsAggregationWorker` refreshed aggregate tables every 5 minutes, but used to skip cache invalidation afterward
- that meant dashboard-family cached responses could stay stale until TTL expiry even when aggregate refresh had already finished
- this did not fake success, but it could delay visibility of newer aggregate data

## Final Decision

`AnalyticsAggregationWorker` now clears the smallest safe set after a successful aggregate refresh:

- `dashboard` family
- aggregate-backed dashboard cache prefixes:
  - `dashboard-bootstrap`
  - `dashboard-advanced`
  - `summary`
  - `daily`
  - `category`
  - `gender`
  - `supplier`
  - `top`
  - `top-advanced`

Reports are intentionally not cleared by this worker.

Why:

- the worker refreshes the aggregate tables that back the dashboard summary and supplier snapshot surfaces
- the dashboard bootstrap response composes separately cached sections, so clearing only the `dashboard` family would leave stale nested sections behind
- report output is versioned separately and does not directly depend on this worker's refresh path
- the blast radius stays smaller than `CoreFamilies`

## Input Gaps

- `docs/qa/STABLE_REPORT_URL_SMOKE.md` is missing, so earlier report-link/cache smoke expectations could not be reused

## Confirmed Source Points

- `AnalyticsCachePolicy.CoreFamilies` includes:
  - `dashboard`
  - `product-decision-center`
  - `supplier-decision-hub`
  - `inventory`
  - `data-quality`
  - `pre-post`
  - `pre-nivelacija-prioriteti`
  - `reports`
- `AccessImportService` clears `CoreFamilies` after successful import and batch-delete analytics cleanup
- `NightlyAnalyticsRefreshWorker` clears `CoreFamilies` after successful refresh
- `AnalyticsDataQualityHealthWorker` clears `data-quality` and `reports`
- report routes use versioned report cache keys through `ReportCacheVersion`
- action outcome summary currently has no dedicated `IAnalyticsCacheService` cache family in the inspected code

## Cache Family Map

| Family | Examples | Current invalidation path |
|---|---|---|
| `dashboard` | sales summary, top products, daily sales, category/gender/supplier summary, dashboard bootstrap, validation cards | import clear, nightly refresh clear, admin clear, TTL |
| `product-decision-center` | product decision overview and related bootstrap inclusion | import clear, nightly refresh clear, admin clear, TTL |
| `supplier-decision-hub` | supplier summary, ranking, quadrant, detail rowset | import clear, nightly refresh clear, admin clear, TTL |
| `inventory` | inventory overview, insights, alerts, forecast, rebalance | import clear, nightly refresh clear, admin clear, TTL |
| `data-quality` | data quality cached endpoint family and validation helpers | import clear, nightly refresh clear, data-quality worker clear, admin clear, TTL |
| `reports` | pilot intake report, supplier decision report | import clear, nightly refresh clear, data-quality worker clear, admin clear, report version bump |
| `action outcome summary` | `/api/analytics/actions/outcomes/summary` | no dedicated analytics cache family found; live query behavior |

## Trigger Matrix

### 1. Access import completed

`AccessImportService` clears `AnalyticsCachePolicy.CoreFamilies`, so this trigger is well covered.

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | Cleared explicitly after successful import | Low | Fresh dashboard/bootstrap after import | P0 covered |
| `product-decision-center` | Cleared explicitly after successful import | Low | Product decisions recompute from fresh source data | P0 covered |
| `supplier-decision-hub` | Cleared explicitly after successful import | Low | Supplier overview does not keep old cached rows | P0 covered |
| `inventory` | Cleared explicitly after successful import | Low | Inventory/OOS/dead-stock views do not keep old cache | P0 covered |
| `data-quality` | Cleared explicitly after successful import | Low | Data-quality surfaces do not keep stale cache from pre-import state | P0 covered |
| `reports` | Cleared via `reports` family inside `CoreFamilies`; report version bumps | Low | Old durable report cache keys become inactive | P0 covered |
| `action outcome summary` | No dedicated analytics cache layer found | Medium | Outcome summary freshness depends on underlying DB writes, not cache invalidation | P2 monitor |

### 2. Manual analytics refresh completed

There is no single universal manual refresh path. Manual run requests are worker-based.

- Manual run of `NightlyAnalyticsRefreshWorker` uses the same success path as scheduled nightly refresh and clears `CoreFamilies`
- Manual run of `AnalyticsDataQualityHealthWorker` uses the same success path as the scheduled data-quality worker and clears `data-quality` and `reports`
- Manual run of `AnalyticsAggregationWorker` now clears the dashboard family and aggregate-backed dashboard prefixes after successful refresh

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | Covered for nightly manual refresh and aggregation-worker refresh | Low | Dashboard/bootstrap can refresh immediately after aggregate refresh | P0 covered |
| `product-decision-center` | Covered when manual refresh means nightly worker | Low | Main product decision cache clears after real analytics refresh | P0 covered |
| `supplier-decision-hub` | Covered when manual refresh means nightly worker | Low | Supplier decision cache clears after real analytics refresh | P0 covered |
| `inventory` | Covered when manual refresh means nightly worker | Low | Inventory cache clears after real analytics refresh | P0 covered |
| `data-quality` | Covered when manual refresh means nightly or data-quality worker | Low | Trust surfaces refresh correctly when the proper worker is run | P0 covered |
| `reports` | Covered when manual refresh means nightly or data-quality worker | Low | Durable report caches rotate on successful refresh | P0 covered |
| `action outcome summary` | No dedicated analytics cache layer found | Medium | Manual refresh does not change summary freshness semantics beyond underlying data updates | P2 monitor |

### 3. Worker analytics refresh completed

This trigger splits into two different worker behaviors.

- `NightlyAnalyticsRefreshWorker`: clears `CoreFamilies` after success
- `AnalyticsAggregationWorker`: clears dashboard family plus aggregate-backed dashboard prefixes after successful refresh

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | Cleared after nightly worker and aggregation worker | Low | Sales summary, top products, daily-sales-derived dashboard sections refresh after the aggregate tables are rebuilt | P0 covered |
| `product-decision-center` | Cleared after nightly worker; aggregation worker does not target this family | Low | Product decisions are not directly backed by the aggregation-worker tables inspected here | P2 low |
| `supplier-decision-hub` | Cleared after nightly worker; aggregation worker writes supplier aggregate tables used by some cached summary routes without clear | Medium | Supplier summary cards that use aggregate tables can lag until TTL expiry | P1 follow-up |
| `inventory` | Cleared after nightly worker; aggregation worker does not target inventory caches | Low | No direct stale risk from aggregation-worker tables confirmed | P2 low |
| `data-quality` | Cleared after nightly worker; aggregation worker only logs a snapshot warning | Medium | Logged quality drift may not immediately change cached trust signals unless another invalidation path runs | P2 monitor |
| `reports` | Cleared after nightly worker; aggregation worker does not bump report version | Medium | Aggregate-only improvements do not rotate report cache unless nightly/import/data-quality invalidation also runs | P1 follow-up |
| `action outcome summary` | No dedicated analytics cache layer found | Low | No worker-side cache family to invalidate | P3 none |

### 4. Data quality recalculated

`AnalyticsDataQualityHealthWorker` saves a snapshot and then clears `data-quality` and `reports`.

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | Not explicitly cleared by data-quality worker | Medium | Dashboard trust callouts can remain older than the newest quality snapshot until TTL/import/nightly clear | P2 monitor |
| `product-decision-center` | Not explicitly cleared by data-quality worker | Medium | Product decision trust chips can lag if only quality metadata changed | P2 monitor |
| `supplier-decision-hub` | Not explicitly cleared by data-quality worker | Medium | Supplier trust surfaces can lag if only quality snapshot changed | P2 monitor |
| `inventory` | Not explicitly cleared by data-quality worker | Medium | Inventory trust callouts can lag if only quality snapshot changed | P2 monitor |
| `data-quality` | Cleared explicitly | Low | Data-quality pages reflect new snapshot | P0 covered |
| `reports` | Cleared explicitly with report version bump | Low | Pilot intake and supplier report trust sections can regenerate from new snapshot | P0 covered |
| `action outcome summary` | No dedicated analytics cache layer found | Low | No separate cache family to clear | P3 none |

### 5. Report generated or regenerated

Current report generation reads cache by versioned key and fills cache on miss. It does not bump report version by itself.

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | Not involved | Low | No dashboard effect | P3 none |
| `product-decision-center` | Not involved | Low | No product decision family effect | P3 none |
| `supplier-decision-hub` | Not involved directly; supplier report reads supplier data and stores report output separately | Low | Main hub cache remains unchanged | P3 none |
| `inventory` | Not involved | Low | No inventory effect | P3 none |
| `data-quality` | Not invalidated by report generation alone | Medium | Generating a report does not refresh underlying quality caches | P3 acceptable |
| `reports` | Cached report is reused until import/nightly/data-quality/admin clear bumps version | Medium | Regenerate semantics rely on cache miss or explicit invalidation, not automatic version bump | P1 document clearly |
| `action outcome summary` | Not involved | Low | No effect | P3 none |

### 6. Action status or outcome updated

No dedicated analytics cache family was found for action outcome summary in the inspected backend code.

| Family | Current behavior | Missing invalidation risk | User impact | Priority |
|---|---|---|---|---|
| `dashboard` | No direct action-family invalidation found | Low | Dashboard action widgets may depend on fresh action reads elsewhere, but no broad cache path was confirmed here | P2 monitor |
| `product-decision-center` | Product decision page action counts are frontend-side optional calls, not a backend cache family here | Low | Main product recommendation cache is separate from action status lookup | P2 monitor |
| `supplier-decision-hub` | No direct action-family invalidation found | Low | Supplier pages may show stale action-adjacent counts only if another cache layer exists outside inspected scope | P2 monitor |
| `inventory` | No direct action-family invalidation found | Low | Inventory decision actions depend on live action endpoints rather than a confirmed analytics cache family | P2 monitor |
| `data-quality` | Not involved | Low | No data-quality effect | P3 none |
| `reports` | No direct action-update invalidation found | Medium | Reports that embed action state can remain stale until report family rotates or is regenerated after a miss | P2 monitor |
| `action outcome summary` | Live query; no explicit cache invalidation required | Low | Action updates should be visible on next query unless DB-side lag exists | P1 confirm in smoke |

## Highest-Risk Findings

1. Report generation does not rotate report cache version on its own; it still depends on import/nightly/data-quality/admin invalidation.
2. Data-quality recalculation refreshes `data-quality` and `reports`, but not other trust-bearing cache families that may display older trust context until TTL expiry.

## Recommended Minimal Follow-Up

1. Keep the regression test that covers successful and failed aggregation refresh invalidation behavior.
2. Consider a future smoke check that verifies the dashboard bootstrap refreshes immediately after the aggregation worker runs.
3. Add the missing `docs/qa/STABLE_REPORT_URL_SMOKE.md` or merge its expectations into the existing pilot smoke checklist if report-link smoke is still needed.
