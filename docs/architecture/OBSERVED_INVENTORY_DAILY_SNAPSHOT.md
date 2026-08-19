# Observed daily inventory snapshot

Date: 2026-08-19
Owner prompt: `RQ96`
Status: first-slice foundation on current dedicated-deploy analytics database

## Decision

Trendplus now has a canonical **observed** SKU/store/day inventory snapshot table. Historical reconstruction remains a separate proxy and must stay labeled.

Internal storage stays PostgreSQL. This slice does not introduce tenant columns; dedicated-deploy scope is `n/a_dedicated`.

## Objects

| Object | Role |
|---|---|
| `analytics_intel.inventory_observed_daily_snapshot` | Durable observed on-hand. No row means unobserved, not zero. |
| `analytics_intel.capture_observed_inventory_daily(date)` | Upserts **one** calendar day from latest `ProductsDim.Kolicina` per article. Skips NULL quantity. Does not backfill history. |
| `analytics_intel.vw_inventory_daily_stock_v1` | Full outer join of observed rows and `vw_inventory_risk_signals_v1` reconstructed proxy with `provenance`. |
| `InventoryDailyStockProvenance` / `GetObservedInventoryDailySnapshot` | Backend contract: observed vs reconstructed vs mixed vs missing; no fake zero. |

`store_id = 0` means unspecified/company-level observation because `ProductsDim` is not store-grained. Store-level observed rows are allowed when a future capture path supplies a real store id.

## Provenance

| Label | Meaning | Quantity rule |
|---|---|---|
| `observed` | Snapshot row exists | `observed_qty` including `0` is true empty |
| `reconstructed` | 022 proxy only | proxy `0` is not observed empty |
| `mixed` | both sides exist and differ | `stock_qty` prefers observed |
| `missing` | neither side present | `stock_qty` is NULL, never `0` |

Date-only filters on the query handler use half-open bounds: `date >= from` and `date < to`.

## Non-goals of this slice

- no scheduled end-of-day worker (capture function exists; caller/ops must invoke it)
- no backfill of reconstructed history into the observed table
- no forecasting, GMROI, or warehouse rewrite
- no change to `vw_inventory_risk_signals_v1` output columns (that view remains reconstructed-only)

## Authoritative historical stock for later prompts

Later forecast/backtesting work (`RQ97`/`RQ98`) should treat **`provenance = observed`** as the only observed historical stock source. Reconstructed rows are auxiliary evidence.
