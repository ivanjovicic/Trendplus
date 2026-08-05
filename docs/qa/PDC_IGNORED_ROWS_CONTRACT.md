# Product Decision Center — Ignored / Top Rows Contract

Date: 2026-08-05  
Related: RQ02 (denominators), RQ12, R12 in `ANALYTICS_DATA_RELIABILITY_AUDIT.md`

## Problem

Operators can misread `IgnoredRowsCount` as “bad data rows” or import rejects. In PDC it only means **rows hidden by the `top` limit**, not invalid/unreliable products.

## Response fields

| Field | Meaning | Formula |
|---|---|---|
| `totalRows` | Returned rows in `rows` | `rows.Count` after sort + `Take(top)` |
| `analyzedRows` | All product rows evaluated before top limit | count before truncation |
| `ignoredRowsCount` | Hidden by top limit only | `max(0, analyzedRows - totalRows)` |
| `ignoredRowsMeaning` | Semantic label for ignored count | always `hidden_by_top_limit` today |

## Summary denominators (do not mix)

| Summary field | Denominator | Scope constant |
|---|---|---|
| `replenishCount`, `markdownCount`, `highPotentialCount`, `badDataCount` | Returned/top rows only | `countDenominatorScope = returned_rows` |
| `lostSalesEstimate`, `slowStockCapital` | All analyzed rows (pre-top) | `moneyDenominatorScope = analyzed_rows` |

### Examples

**Top limit (top=1, 2 analyzed):**

- Returned: highest-priority row (often `FIX_DATA` when present).
- `badDataCount` counts FIX_DATA only if that row is **returned**.
- `ignoredRowsCount = 1` means one analyzed row was **not shown**, not that it was bad data.
- Money totals still include **all analyzed** lost sales / slow stock.

**No truncation (top ≥ analyzed):**

- `ignoredRowsCount = 0`
- `ignoredRowsMeaning` remains `hidden_by_top_limit` (explicit contract, not “none”).

## Not the same as

| Other surface | Meaning of “ignored” |
|---|---|
| Data quality intake / pilot import | Invalid/incomplete import rows |
| Supplier scorecard trust | Filtered supplier rows |
| PDC `badDataCount` | FIX_DATA recommendations in **returned** rows |

## Backward compatibility

- Numeric behavior unchanged since RQ02.
- Additive metadata: `ignoredRowsMeaning`, `countDenominatorScope`, `moneyDenominatorScope`.

## Tests

- `Api.Tests/ProductDecisionCenterSummaryDenominatorTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/ProductDecisionCenterIgnoredRowsContractTests.cs`
