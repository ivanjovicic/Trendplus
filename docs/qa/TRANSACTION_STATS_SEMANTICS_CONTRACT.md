# Transaction Stats Semantics Contract

Date: 2026-08-05  
Related: RQ11, R11 in `ANALYTICS_DATA_RELIABILITY_AUDIT.md`

## Endpoint

- `GET /api/analytics/cached/sales/transaction-stats`
- Also embedded in dashboard bootstrap via `BuildTransactionStatsSnapshotAsync`

## Definitions

The deployed `ProdajaZaglavlje` source has not been proven to represent one customer receipt. Treat it as a **sales aggregation document** until source-lineage evidence establishes receipt grain.

| Field | Meaning | Formula | UI label |
|---|---|---|---|
| `avgItemsPerTransaction` | Unavailable; a document is not a proven customer receipt | `null` with reason `receipt_grain_unavailable` | **Nije dostupno** until receipt lineage is proven |
| `avgUnitsPerTransaction` | Unavailable; a document is not a proven customer receipt | `null` with reason `receipt_grain_unavailable` | **Nije dostupno** until receipt lineage is proven |
| `avgBasketValue` | Unavailable; average document value is not a customer basket | `null` with reason `receipt_grain_unavailable` | **Prosečna korpa — nije dostupno** |
| `avgTransactionValue` | Average value per sales aggregation document; not a customer basket | `SUM(kolicina * cena)` per source document, then averaged | **Prosečna vrednost prodajnog dokumenta** |
| `totalTransactions` | Count of source sales aggregation documents in scope | distinct sale headers | **Prodajni dokumenti** |

Revenue, sold units, and each endpoint's existing certified sales-population rules are unchanged. The legacy `totalTransactions` wire key remains for compatibility and is accompanied by `salesUnit: "sales_document"`.

## Decision (RQ11)

RQ11 established that `avgItemsPerTransaction` counted **sale lines**, not units. That formula remains understood, but the field is no longer presented as a customer-receipt metric because the source grain is unproven.

When quantity on a line is greater than 1, lines and units diverge. Example fixture:

- Receipt A: 2 lines (qty 2 + qty 1) → 2 lines, 3 units
- Receipt B: 1 line (qty 3) → 1 line, 3 units
- Average lines = 1.5; average units = 3.0

## Owner decision (RQ577, 2026-10-04)

- Treat `ProdajaZaglavlje` as a **prodajni dokument**, not a proven customer receipt. Do not infer daily, shift, or customer grain without source-lineage evidence.
- Hide/disable average basket, items-per-transaction, basket affinity, and receipt/transaction heatmap counts with reason code `receipt_grain_unavailable`.
- Keep revenue, units, sales-document counts, and certified sales-population rules unchanged. The heatmap may continue to show revenue and units; it must not present its sale-line count as receipts.
- The decision is reversible only after source-lineage evidence proves customer-receipt grain.

## Backward compatibility

- `totalTransactions` remains as a legacy API field name, but carries a sales-document count; responses identify `salesUnit: "sales_document"`.
- The `avgItemsPerTransaction`, `avgUnitsPerTransaction` and `avgBasketValue` keys remain but are nullable and return `null` until receipt grain is proven. The reason code is `receipt_grain_unavailable`.
- Average document value remains available under the legacy `avgTransactionValue` key and must be labelled as a sales-document average, never as a receipt/basket value.

## Do not confuse with

- `totalUnits` elsewhere in analytics (period totals, not per-receipt average).
- Compatibility route `/api/analytics/sales/transaction-stats` in `AllEndpoints.cs` redirects to the cached transaction-stats endpoint and inherits this contract.
