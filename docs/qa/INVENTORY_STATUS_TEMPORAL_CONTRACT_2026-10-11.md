# InventoryStatus temporal contract — 2026-10-11

Status: **implementation delivered; remote CI verdict pending**.
Implementation commit: `968e8f30c5b567391bdb89b56e3fc3ba70eae4a0`.

## Owner decision

Existing InventoryStatus represents **latest available loaded stock**, not the historical inventory at a chosen sales date. A single `ProductsDim` row per product and the current `Artikli` row do not form an inventory time series. Their `Timestamp` / `UpdatedAt` fields represent refresh times, not stock as of that date.

Backward-compatible legacy `fromDate` / `toDate` query parameters remain accepted (and invalid reverse ranges remain rejected) but do **not** filter InventoryStatus. Store, supplier, `dataScope` and low-stock threshold continue to apply. In particular, `imported` maps to `DataOrigin='access'`; `existing` covers `existing`, NULL and empty string.

The cached endpoint reports:
- `inventoryTimeBasis = latest_available_snapshot`
- `periodFilterApplied = false`
- `historicalAsOfSupported = false`
- `meta.warningCode = inventory_status_current_snapshot_only` when a sales date range is supplied and snapshot data exists (or the operational fallback warning with an explicit current-state caveat).
- A true empty item/store/supplier scope is still an empty result; it is never presented as a verified zero historical stock position.

The dashboard's inventory DTO carries the same temporal fields and the frontend labels the card as the latest available state. The date-range-dependent **sales** API contracts are unchanged. The stock cache uses a new versioned, scope/filter-specific key independent of the ignored date range.

## Supplied local PostgreSQL evidence (read-only)

Connection reported by the operator: `trendplus` on 127.0.0.1:5434, PostgreSQL 16.13. Batch #23: 5,530 Access receipts, 67,092 Access sale lines; fact parity 1:1, no amount/quantity/price mismatch. No import, seed, migration or write was performed.

Access `ProductsDim`: 12,422 products, 3,566 positive on-hand units, 213 low-stock products, 11,885 measured-zero stock products. The source snapshot was refreshed at **2026-10-10 16:30:34 UTC**. A date-only request of `fromDate=2026-10-10&toDate=2026-10-10` formerly excluded every row because both parameters resolved to midnight; requesting August likewise returned zero even though there was no historical inventory evidence.

These local counts were supplied from the operator's SQL verification; **this GitHub change did not independently query the operator's Docker database**. The PostgreSQL integration fixture is synthetic and checks scope, current-snapshot semantics, legacy same-day and August ranges, cache isolation and HTTP metadata. The existing operational fallback test covers the same historical-date warning.

## Unresolved

- A true historical `asOf` endpoint requires persisted per-store, per-product inventory snapshots (or a certified replay of complete journal movements with known opening balances) and explicit freshness/completeness metadata. This change **does not** build or certify historical inventory.
- Business owners must not treat current on-hand as the stock level during any past markdown, promotion or sales period.
- Do not enable historical stock recommendations or automatic actions based on current snapshot values.
- Release acceptance requires the exact-commit GitHub Actions PostgreSQL/HTTP integration and frontend checks; until those finish, the implementation must not be described as remotely CI-certified.
