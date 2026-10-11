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
| `analytics_intel.capture_observed_inventory_daily(uuid)` | Captures one previously started run at its exact observation time, serialized with a transaction-scoped lock. |
| `analytics_intel.capture_observed_inventory_daily(date)` | Compatibility wrapper for current-session business date only; rejects a date that does not match its observation instant. |
| `analytics_intel.inventory_observed_daily_capture_run` | Business date/time zone, exact observation instant, source-row update watermark, row count and run state. |
| `analytics_intel.vw_inventory_daily_stock_v1` | Full outer join of observed rows and `vw_inventory_risk_signals_v1` reconstructed proxy with `provenance`. |
| `InventoryDailyStockProvenance` / `GetObservedInventoryDailySnapshot` | Backend contract: observed vs reconstructed vs mixed vs missing; no fake zero. |

`store_id = 0` means unspecified/company-level observation because `ProductsDim` is not store-grained. Store-level observed rows are allowed when a future capture path supplies a real store id.

## Prospective scheduled capture

`ObservedInventoryDailyCaptureWorker` is registered only in the worker process. It is opt-in through `ObservedInventoryDailyCapture:Enabled` (default `false`), runs after the configured local business time, and uses the configured time zone. A successful/empty run suppresses duplicate captures until a newer `ProductsDim.Timestamp` appears before that same local business date ends; this permits a same-day capture after an observable late source update. An explicit same-day SQL retry remains idempotent.

`analytics_intel.inventory_observed_daily_capture_run` records the exact observation instant, business date and configured time zone, source system, source-row update watermark, run state, count and completion time. SQL rejects a business date that does not match that instant in the configured time zone, preventing callers from backdating today's current stock. `ProductsDim` does not carry an import batch id, so `source_batch_id` remains NULL. `ProductsDim.Timestamp` is mapped from source `Artikli.UpdatedAt`; the maximum eligible value is recorded as an update watermark, not as a batch id or proof that the upstream source is fresh. Rows timestamped after the observation instant are excluded. The worker detects late same-day updates only when their source timestamp exceeds the saved watermark; batch-level detection remains unavailable. An `empty` run means no non-NULL quantities were available; it does not prove all stock is zero. A failed transaction leaves the run incomplete until the worker records `failed`; a process interruption can leave `running`, which must be treated as incomplete, not fresh.

Current `ProductsDim` stock is company-level only. The capture writes it under `store_id = 0` and does not copy it to `StoresDim` locations. Two stores in a dimension are not evidence of per-store stock. Per-store capture remains unavailable until an authoritative store-grained source is wired. A missing snapshot row remains unobserved; only a stored `on_hand_qty = 0` is verified empty. Run coverage/state is available from the capture-run table and must accompany any freshness interpretation.

The worker is code-ready but remains disabled by default. Enabling it in a deployed environment is a separate operator action; no historical `asOf` API or automatic August backfill is introduced.

## Provenance

| Label | Meaning | Quantity rule |
|---|---|---|
| `observed` | Snapshot row exists | `observed_qty` including `0` is true empty |
| `reconstructed` | 022 proxy only | proxy `0` is not observed empty |
| `mixed` | both sides exist and differ | `stock_qty` prefers observed |
| `missing` | neither side present | `stock_qty` is NULL, never `0` |

Date-only filters on the query handler use half-open bounds: `date >= from` and `date < to`.

## Non-goals of this slice

- no store-grained source: `ProductsDim.Kolicina` can only be captured at store_id 0
- no backfill of reconstructed history into the observed table
- no forecasting, GMROI, or warehouse rewrite
- no change to `vw_inventory_risk_signals_v1` output columns (that view remains reconstructed-only)

## Authoritative historical stock for later prompts

Later forecast/backtesting work (`RQ97`/`RQ98`) should treat **`provenance = observed`** as the only observed historical stock source. Reconstructed rows are auxiliary evidence.
