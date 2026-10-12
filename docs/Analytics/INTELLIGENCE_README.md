# Analytics Intelligence Layer

## Overview

`analytics_intel` is the production SQL intelligence layer for the retail analytics platform.

It sits above the raw transactional and dimensional model and exposes:

- versioned semantic views for dashboards and downstream analytics
- materialized cache layers for low-latency reads
- explicit refresh ordering for nightly batch stability
- safe schema evolution patterns for long-lived PostgreSQL deployments

The layer is intentionally versioned with `_v1` suffixes so future changes can ship as additive `v2` objects instead of risky in-place rewrites.

## Physical Layout

SQL files live under `Database/Analytics/Intelligence/`.

- `020_create_intelligence_schema.sql`
- `021_product_demand_signals_v1.sql`
- `022_inventory_risk_signals_v1.sql`
- `023_price_intelligence_v1.sql`
- `024_trend_momentum_v1.sql`
- `025_observed_inventory_daily_snapshot_v1.sql`

The schema is `analytics_intel`.

Each signal domain provides:

- a live view: `analytics_intel.vw_*_v1`
- a materialized cache: `analytics_intel.mv_*_v1_cache` (observed daily stock in 025 is a durable table plus live join view, not an MV)
- a unique index so `REFRESH MATERIALIZED VIEW CONCURRENTLY` is available

## Source Model

The implementation is aligned to the actual tables present in this codebase:

- `SalesFacts`
- `SalesLineFacts`
- `ProductsDim`
- `SuppliersDim`
- `InventoryMovementFacts`
- `GlobalTrendScores`
- `TrendHistory`
- `trend_product_snapshots`
- `trend_product_momentum`

Legacy `prodaja_*` and `Artikli` tables remain the operational system of record, but the intelligence layer prefers analytics-native tables whenever possible.

## Signals

### Product Demand Signals

Object:

- `analytics_intel.vw_product_demand_signals_v1`

Grain:

- `article_id`, `store_id`, `date`

Signals:

- `sales_velocity`: rolling 7-day unit sum
- `demand_acceleration`: current 7-day velocity vs previous 7-day velocity
- `days_since_last_sale`: latest selling day distance
- `launch_age_days`: first-sale proxy for launch age, with `ProductsDim.Timestamp` fallback
- `store_coverage`: distinct stores with sales in trailing 30 days
- `source_rows`: trailing raw line-count signal support

### Inventory Risk Signals

Object:

- `analytics_intel.vw_inventory_risk_signals_v1`

Grain:

- `article_id`, `date`

Signals:

- `days_of_cover`
- `stock_turn`
- `stockout_days`
- `low_stock_days`
- `dead_stock_risk`

Important assumption:

- canonical observed daily stock is `analytics_intel.inventory_observed_daily_snapshot` (see `docs/architecture/OBSERVED_INVENTORY_DAILY_SNAPSHOT.md`)
- this risk view still reconstructs a **proxy** from current `ProductsDim.Kolicina`, trailing sales, and canonical movement types in `InventoryMovementFacts`
- reconstructed `stock_qty` is not observed warehouse history; use `vw_inventory_daily_stock_v1.provenance` to tell the difference

Movement handling used by the SQL:

- inflow: `Ulaz robe`, `Prenos ulaz`, `Povrat kupca`
- outflow: `Prodaja`, `Prenos izlaz`

Default thresholds:

- low stock threshold: `GREATEST(MinimalnaKolicina, 5)`
- dead stock threshold: stock remains but no sales in trailing 45 days

### Price Intelligence

Price snapshot dates are derived from `ProductsDim.Timestamp` in UTC. The source
column is `timestamp with time zone` and represents the UTC `Artikli.UpdatedAt`
snapshot, so a database session's time zone must not change `price_date` or the
page's `asOfDate` metadata.

Object:

- `analytics_intel.vw_price_intelligence_v1`

Canonical price mapping:

- `net_price` -> `ProductsDim.SalePrice`
- `list_price` -> `ProductsDim.FirstSalePrice`
- `cost` -> `ProductsDim.PurchasePriceRsd`, fallback `PurchasePrice`

Signals:

- `price_index_vs_category`
- `price_index_vs_brand`
- `discount_depth`
- `margin_pct`

Brand note:

- `ProductsDim.Brand` exists but is not consistently populated by all ingest paths
- when brand is blank, the SQL falls back to a supplier-based surrogate key so brand-relative pricing remains numerically stable

### Trend Momentum

Object:

- `analytics_intel.vw_trend_momentum_v1`

Signals:

- `external_trend_score`
- `local_sales_acceleration`
- `trend_entropy`

Details:

- `external_trend_score` prefers recent `TrendHistory` and falls back to a `GlobalTrendScores` row updated before the next UTC signal day; it is `NULL` when neither source has an eligible row, while a stored zero remains a measured zero
- `external_trend_source` and `external_trend_observed_at` identify the selected source and its recorded date/time; `GlobalTrendScores.UpdatedAt` has no time zone in the source schema and is returned as stored
- `local_sales_acceleration` is the 28-day regression slope of daily sales
- `trend_entropy` is normalized Shannon entropy over trailing 28-day gross positive sold-unit shares. Signed customer returns remain in the net-sales regression slope but are not probability mass. The value is `NULL` when the window has no positive sold-unit evidence; one active gross-sales day is a measured entropy of zero.

Future extension:

- because `pgvector` is enabled at schema bootstrap, a future `v2` signal can blend semantic product embeddings from `ProductImage` or `EuTrends` without changing the `v1` contract

## Refresh Strategy

### Startup

`DatabaseInitializer` creates the schema synchronously and then defers the heavier intelligence SQL build to a background task.

This keeps app startup responsive while still ensuring:

- missing intelligence objects are rebuilt automatically
- stale startup-history rows do not permanently mask missing views/materialized views
- long-running MV creation does not block core API boot

### Nightly

`NightlyAnalyticsRefreshWorker` refreshes intelligence caches after the existing core analytics materialized views.

Refresh order:

1. `analytics_intel.mv_product_demand_signals_v1_cache`
2. `analytics_intel.mv_inventory_risk_signals_v1_cache`
3. `analytics_intel.mv_price_intelligence_v1_cache`
4. `analytics_intel.mv_trend_momentum_v1_cache`

This ordering avoids downstream domains observing stale upstream demand state.

## Performance Model

The design follows three rules:

1. expensive rolling logic lives in SQL close to the data
2. dashboard reads target materialized caches whenever freshness allows
3. live views remain available for smoke tests, diagnostics and controlled ad-hoc access

Existing source indexes already cover the main join/filter paths:

- `SalesFacts(SaleTimestampUtc, StoreId)`
- `SalesLineFacts(ProductId, SaleId)`
- `InventoryMovementFacts(ArtikalId, Datum)`
- `GlobalTrendScores(LocalProductId)`
- `TrendHistory(LocalProductId, Date)`

New intelligence caches add unique keys for concurrent refresh and focused secondary indexes on common filter dimensions such as date and category.

## Schema Evolution

Every intelligence SQL file uses an explicit column contract check against `information_schema.columns`.

If the expected column layout changes:

- the dependent materialized cache is dropped
- the versioned view is dropped
- the script recreates both objects with the new contract

This avoids PostgreSQL `42P16` errors from unsafe `CREATE OR REPLACE VIEW` operations that reorder or remove columns.

## Dashboard Consumption

Recommended dashboard strategy:

- default user-facing dashboards query the `mv_*_cache` objects
- diagnostics and smoke tests query the `vw_*_v1` views
- API contracts should treat these objects as read-only semantic projections, not as mutable business tables

## API Surface

The API now exposes read-only intelligence routes under `/api/analytics/intelligence`:

- `/demand-signals`
- `/inventory-risk`
- `/price-intelligence`
- `/trend-momentum`

Endpoint design notes:

- demand and inventory default to the latest available snapshot date and support `historyDays` for short sparkline-style history windows
- all routes support pagination and whitelisted sort keys
- filters are applied against the intelligence caches plus latest product/supplier/store dimension snapshots
- the demand, inventory-risk, price-intelligence and trend-momentum routes return `503` while their startup cache is pending, failed, missing or hash-mismatched; they do not present stale rows as a successful empty result. A ready cache containing zero rows remains a valid empty result.

### Cache freshness operator check

The startup refresh is asynchronous and does not depend on `NightlyAnalyticsRefreshWorker`. For all four signal caches, `analytics_intel.cache_refresh_state` records startup state, expected startup SQL hash, last successful refresh time, as-of date, generation and refresh source, plus the latest attempt status and safe failure code. The app serves a cache only when it is `ready`, the materialized relation exists, and `expected_script_hash` matches `__StartupSqlScriptHistory.ScriptHash`.

Demand, inventory-risk and trend-momentum default reads also require a successful cache generation for the database's current UTC date. A stale but otherwise ready cache returns `503` for a current-day read. An explicit historical date remains available when covered by the last successful cache as-of date. This checks cache-generation freshness; source-data freshness is reported as `unknown` because there is no authoritative generation contract across every business-data mutation path. It does not establish a 24-hour SLA or certify production worker deployment.

Successful route responses include `cacheFreshness` with the metadata version, cache as-of date, last successful refresh time, successful generation/run identifiers, refresh source, latest attempt time/status/failure code, and `sourceFreshnessStatus: "unknown"`. `asOfDate` continues to describe the requested or returned signal period.

Run this read-only query against the analytics database when one of these endpoints returns `503`:

```sql
SELECT
    state.cache_name,
    state.build_state,
    state.expected_script_hash,
    history."ScriptHash" AS applied_script_hash,
    state.last_refreshed_at_utc,
    state.metadata_version,
    state.last_successful_as_of_date_utc,
    state.last_successful_refresh_at_utc,
    state.last_successful_generation_id,
    state.last_successful_run_id,
    state.last_successful_refresh_source,
    state.last_refresh_attempt_at_utc,
    state.last_refresh_status,
    state.last_refresh_failure_code,
    state.failure_code,
    to_regclass(state.cache_name) IS NOT NULL AS cache_exists
FROM analytics_intel.cache_refresh_state AS state
LEFT JOIN "__StartupSqlScriptHistory" AS history
  ON history."ScriptPath" = state.sql_file_path
WHERE state.cache_name IN (
    'analytics_intel.mv_product_demand_signals_v1_cache',
    'analytics_intel.mv_inventory_risk_signals_v1_cache',
    'analytics_intel.mv_price_intelligence_v1_cache',
    'analytics_intel.mv_trend_momentum_v1_cache'
)
ORDER BY state.cache_name;
```

`pending` means a refresh is queued or interrupted; `failed` in `build_state` means the latest background build did not complete. `last_refresh_status` records the latest startup/worker attempt separately, while the last successful generation is retained after a failed attempt. A daily cache whose `last_successful_as_of_date_utc` is earlier than the current UTC date is unavailable for a default current-day read; the attempt fields distinguish a recorded failure from a missed refresh. A hash mismatch or missing cache is also unavailable. Check the application startup log for a build failure, then let the next authorized application startup retry the bounded idempotent build. Do not clear startup history or mark a cache ready by hand. This runbook is a release-time diagnostic; it does not deploy the application or refresh a business database. A `ready` row with matching hashes and an empty cache remains a successful empty result when its refresh metadata covers the requested as-of date.

## Testing

`Api.Tests/AnalyticsIntelligenceSmokeTests.cs` verifies:

- SQL files are present in test output
- `analytics_intel.vw_product_demand_signals_v1` can be created/reapplied
- the view exists
- expected columns are returned
- the query executes successfully

## Planned Extensions

- demand forecast priors using `pgvector` similarity to global trend products
- supplier intelligence joins to `analytics_intel` demand and inventory signals
- multi-store stock-position models once a persistent daily stock snapshot fact table exists
- explicit launch calendar support instead of the current first-sale launch proxy
