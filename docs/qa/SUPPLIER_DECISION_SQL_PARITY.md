# Supplier Decision SQL Parity Matrix

Date: 2026-08-05

This note records the intended contract split between the two supplier-decision query paths:

- `BuildPrecomputedSupplierRowsSql(...)` in `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `BuildSupplierRowsSql(...)` in `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

## Parity Matrix

| Contract area | Precomputed path | Live path | Intent |
| --- | --- | --- | --- |
| Requested / effective dataset | Uses `ResolveRequestedDataset(...)` and `ResolveEffectiveDataset(...)` for trust metadata. | Uses the same requested/effective dataset helpers for trust metadata. | Shared contract. |
| 30d helper behavior | 30d requests resolve to the 90d helper dataset in trust metadata because there is no 30d MV. | The live CTE path still reports the same requested/effective helper contract, but it sources rows from raw signals when precomputed gating is not available. | Intentional helper contract, not a formula drift. |
| Date range filter | Applies `ds.period_to >= @fromDate AND ds.period_from <= @toDate`. | Applies `fs.first_markdown_date >= @fromDate` and `fs.first_markdown_date <= @toDate`. | Same requested range semantics, different source columns. |
| `storeId` filter | Not available on the precomputed path, so it is a gating condition for `CanUsePrecomputedSupplierRows(...)`. | Applies `a."IDObjekat" = @storeId`. | Intentional difference. |
| `category` filter | Not available on the precomputed path, so it is a gating condition for `CanUsePrecomputedSupplierRows(...)`. | Applies `COALESCE(fs.category, 'Uncategorized') ILIKE @category`. | Intentional difference. |
| `gender` filter | Not available on the precomputed path, so it is a gating condition for `CanUsePrecomputedSupplierRows(...)`. | Applies `COALESCE(a."Pol", '') ILIKE @gender`. | Intentional difference. |
| `seasonId` filter | Not available on the precomputed path, so it is a gating condition for `CanUsePrecomputedSupplierRows(...)`. | Applies `a."IDSezona" = @seasonId`. | Intentional difference. |
| `dataScope` filter | Only `all` is allowed on the precomputed path. | Supports `all`, `imported`, and `existing`. | Intentional difference. |
| `onlyHighConfidence` filter | Applies `ds.confidence_score * 100 >= @confidenceThreshold`. | Applies `sr.confidence_score >= @confidenceThreshold`. | Shared threshold semantics, source-scaled by path. |
| `minRevenue` filter | Applies `ds.revenue >= @minRevenue`. | Applies `sr.revenue >= @minRevenue`. | Shared contract. |
| Recommendation code | Returns `ds.recommendation_code` from the cache row. | Derives `recommendation_code` from `filtered_suppliers` CTE. | Intentional implementation difference, same output contract. |
| Confidence score | Returns `ROUND(ds.confidence_score * 100, 2)`. | Computes `confidence_score` from live CTE signals. | Shared output contract, different source. |
| ML explanation / features | Uses `vw_supplier_ml_latest_predictions` when available, otherwise empty strings. | Uses `supplier_ml_predictions` when available, otherwise empty strings. | Intentional difference, same output shape. |
| Null / zero handling | Reader helpers coerce missing numeric/string columns to `0` / `""` at the DTO boundary. | Same reader helpers and same boundary behavior. | Shared contract. |

## Notes

- The precomputed path is intentionally narrower because it is a cache-backed helper path.
- The live path is intentionally broader because it needs to support store, category, gender, season, and `dataScope` filters directly.
- Any future attempt to deduplicate these SQL blocks should start as a smaller follow-up prompt after this matrix is updated.
