# Analytics Inventory Signal Reliability Audit

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Status: mixed audit; RQ64 resolved in queue task

## Scope

This pass focuses on inventory analytics signal surfaces that were not deeply covered in earlier audits:

- forecast snapshot handler and forecast panel
- rebalance snapshot handler and rebalance panel
- alert snapshot handler
- size-curve snapshot handler
- inventory page signal filtering, placeholder rows and screen CSV/export behavior

Reviewed files:

- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
- `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
- `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
- `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
- `Klijent/clientapp/src/types/analytics.ts`

## Resolution note

RQ64 from this audit was resolved in queue task `RQ64` on 2026-08-10.

Validation completed:

- `dotnet test Api.Tests/Api.Tests.csproj --filter "InventorySnapshotContractTests"`: pass
- `npm run test -- --run src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`: pass
- `npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`: pass
- `npm run test -- --run src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`: pass
- `npm run check:analytics-guardrails`: pass
- `npm run build`: pass

## Fixed in this pass

### F01 - Forecast panel matched SKU by `skuId` only, not `skuId + storeId`

Files changed:

- `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`

Observed before:

- Forecast panel resolved display row with `rows.find((row) => row.id === item.skuId)`.
- In a multi-store row set, the first matching SKU row could come from another store.

Fix:

- Added `findForecastRow` helper that prefers exact `skuId + storeId`, then aggregate/all-store row, then final SKU fallback.
- Added a test proving the panel shows the row from the correct store when the same SKU appears in multiple stores.

Commit(s):

- `60523759c354339e79d58b389916cce7e9e765f9`
- `0db9d78409652a9d3eab68f2b98f478d9a82f6fb`

Tests were not run in this environment.

## New findings

### R64 - Inventory snapshot handlers convert missing numeric evidence to zero

Files:

- `GetInventoryForecastHandler.cs`
- `GetRebalanceSuggestionsHandler.cs`
- `GetInventoryAlertsHandler.cs`
- `GetInventorySizeCurveHandler.cs`

Observed:

- Forecast uses `coalesce(forecast_7d, 0)`, `coalesce(probability_of_oos_in_7d, 0)`, `coalesce(overstock_risk, 0)`, `coalesce(confidence_score, 0)`.
- Rebalance uses `coalesce(recommended_qty, 0)`, `coalesce(confidence, 0)`, `coalesce(expected_saved_sales, 0)`, `coalesce(expected_capital_release, 0)`.
- Alerts uses `coalesce(confidence_score, 0)` and `coalesce(severity, 'info')`.
- Size curve uses `coalesce(actual_size_share, 0)`, `coalesce(ideal_size_share, 0)`, `coalesce(deviation_pct, 0)`, boolean flags to false, and confidence to zero.

Risk:

- Missing evidence can look like true zero risk, true zero confidence, normal info alert, no broken run, or no expected impact.
- This violates the no-fake-zero/no-fake-green rule.

Classification: likely high-impact fake-confidence bug.

Recommended prompt: RQ64.

### R65 - Forecast/rebalance/alerts/size-curve `totalCount` is returned-row count after `limit`, not total matching rows

Files:

- `GetInventoryForecastHandler.cs`
- `GetRebalanceSuggestionsHandler.cs`
- `GetInventoryAlertsHandler.cs`
- `GetInventorySizeCurveHandler.cs`

Observed:

- Each handler selects rows with `limit @top`, builds `items`, and returns `TotalCount = items.Count`.
- UI labels can read this as total available signal count.

Risk:

- “50 SKU u prognozi” can mean “50 returned rows due to limit”, not all matching forecast rows.
- It can hide truncation and make export/report/signals look complete.

Classification: likely count/truncation semantics bug.

Recommended prompt: RQ65.

### R66 - Inventory synthetic placeholder rows can introduce fake zero baseline

File:

- `InventoryPage.tsx`

Observed:

- `openDetailBySku` creates a synthetic `InventoryRow` when the SKU is not in the current page rows.
- The placeholder uses `kolicina: 0`, `minimalnaKolicina: 0`, `nabavnaCena: 0`, `estimatedValue: 0`.

Risk:

- While detail is loading or if detail fails, the UI can show a fabricated zero stock/value baseline for a SKU opened from alerts/forecast.
- This can make unavailable row context look like true zero inventory.

Classification: likely fake-zero UI/detail bug.

Recommended prompt: RQ66.

### R67 - Forecast restock workflow estimate uses row unit cost even when cost is fallback zero

File:

- `InventoryPage.tsx`

Observed:

- `queueForecastRestock` builds a workflow suggestion with `estimatedValue: row.unitCost * Math.max(1, Math.ceil(item.forecast7d))`.
- `row.unitCost` can be zero when cost is missing, based on existing inventory row construction.

Risk:

- Forecast restock suggestion can carry value `0` while the issue may be missing cost evidence, not zero value.
- This overlaps the broader cost/value vocabulary contract.

Classification: likely fake-zero workflow value bug.

Recommended prompt: RQ67.

### R68 - Inventory signal panels do not follow search filter lineage

File:

- `InventoryPage.tsx`

Observed:

- Main inventory list and action workflow use `search`.
- Forecast, alerts and rebalance signal tasks use store/supplier filters but do not pass search.
- UI copy often says signals are for current filters.

Risk:

- User can search for a SKU/name and still see forecast/alerts/rebalance signals for the broader store/supplier dataset.
- This is a filter-lineage mismatch.

Classification: suspicious/likely cross-surface filter bug.

Recommended prompt: RQ68.

### R69 - Rebalance suggestions ignore selected store filter

File:

- `InventoryPage.tsx`

Observed:

- `getRebalanceSuggestions` is called with `supplierId` and `top`, but not selected store.
- The rebalance handler supports `fromStoreId` and `toStoreId`, but the page does not use selected store to constrain or label suggestions.

Risk:

- When the user filters inventory by one store, the rebalance panel can still show suggestions for unrelated store pairs.

Classification: likely filter-lineage bug.

Recommended prompt: RQ69.

### R70 - Forecast panel OOS restock quantity uses forecast demand, not gap/stock-aware recommendation

Files:

- `InventoryPage.tsx`
- `DemandForecastPanel.tsx`

Observed:

- OOS forecast button says `Predlog dopune (signal)`.
- `queueForecastRestock` sets `suggestedQty = Math.max(1, Math.ceil(item.forecast7d))`.

Risk:

- It may be acceptable as a signal, but can be read as replenishment quantity.
- Without stock baseline/gap reasoning, this can under/overstate operational reorder quantity.

Classification: suspicious action-quantity semantics gap.

Recommended prompt: RQ70.

### R71 - Size-curve missing boolean evidence defaults to `false`

File:

- `GetInventorySizeCurveHandler.cs`

Observed:

- `is_core_size_missing`, `is_dead_size`, `broken_run` default to false via COALESCE.

Risk:

- Missing size-curve evidence can look like “not missing core size”, “not dead size”, “not broken run”.
- This can hide data-quality gaps as healthy run structure.

Classification: likely fake-good size-curve bug.

Recommended prompt: RQ71.

## Recommended order

1. RQ64 - null/missing snapshot evidence must not become zero/info/false.
2. RQ65 - distinguish returned count from total matching count and show truncation.
3. RQ66/RQ67 - prevent fake zero placeholder/value in inventory UI/workflow.
4. RQ68/RQ69 - align filter lineage for signal panels.
5. RQ70 - clarify forecast suggested quantity semantics.
6. RQ71 - explicit size-curve boolean evidence status.

## Note

F01 is fixed, but RQ64-RQ71 remain queue candidates. The small fix does not solve broader inventory signal trust issues.
