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

The schema is `analytics_intel`.

Each signal domain provides:

- a live view: `analytics_intel.vw_*_v1`
- a materialized cache: `analytics_intel.mv_*_v1_cache`
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

- there is no canonical persisted daily stock snapshot table today
- daily stock is therefore reconstructed as a proxy from current `ProductsDim.Kolicina`, trailing sales, and canonical movement types in `InventoryMovementFacts`

Movement handling used by the SQL:

- inflow: `Ulaz robe`, `Prenos ulaz`, `Povrat kupca`
- outflow: `Prodaja`, `Prenos izlaz`

Default thresholds:

- low stock threshold: `GREATEST(MinimalnaKolicina, 5)`
- dead stock threshold: stock remains but no sales in trailing 45 days

### Price Intelligence

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

- `external_trend_score` prefers recent `TrendHistory` and falls back to `GlobalTrendScores`
- `local_sales_acceleration` is the 28-day regression slope of daily sales
- `trend_entropy` is normalized Shannon entropy over trailing 28-day daily sales shares

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
- when intelligence objects are still building during startup, the API returns empty paged payloads instead of failing over to slow ad-hoc SQL

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
