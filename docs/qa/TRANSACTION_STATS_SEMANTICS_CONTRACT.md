# Transaction Stats Semantics Contract

Date: 2026-08-05  
Related: RQ11, R11 in `ANALYTICS_DATA_RELIABILITY_AUDIT.md`

## Endpoint

- `GET /api/analytics/cached/sales/transaction-stats`
- Also embedded in dashboard bootstrap via `BuildTransactionStatsSnapshotAsync`

## Definitions

| Field | Meaning | Formula (per receipt, then averaged) | UI label |
|---|---|---|---|
| `avgItemsPerTransaction` | Average **sale lines** per receipt | `COUNT(prodaja_stavke)` grouped by `prodaja_zaglavlje` | Dashboard: **Stavki po transakciji** |
| `avgUnitsPerTransaction` | Average **sold units** per receipt | `SUM(kolicina)` grouped by `prodaja_zaglavlje` | (optional future KPI) |
| `avgTransactionValue` | Average receipt value | `SUM(kolicina * cena)` per receipt | **Vrednost transakcije** |
| `totalTransactions` | Receipt count in scope | distinct sale headers | — |

## Decision (RQ11)

Retail POS wording in Trendplus uses **stavka** for a sale line on a receipt, not a unit. The existing dashboard label *Stavki po transakciji* therefore maps to **line count**, not unit count.

When quantity on a line is greater than 1, lines and units diverge. Example fixture:

- Receipt A: 2 lines (qty 2 + qty 1) → 2 lines, 3 units
- Receipt B: 1 line (qty 3) → 1 line, 3 units
- Average lines = 1.5; average units = 3.0

## Backward compatibility

- `avgItemsPerTransaction` **keeps line-count semantics** (unchanged behavior).
- `avgUnitsPerTransaction` is an additive field for unit-based basket analysis.

## Do not confuse with

- `totalUnits` elsewhere in analytics (period totals, not per-receipt average).
- Legacy `/api/analytics/sales/transaction-stats` in `Program.cs` (same line-count semantics; out of RQ11 scope unless separately aligned).
