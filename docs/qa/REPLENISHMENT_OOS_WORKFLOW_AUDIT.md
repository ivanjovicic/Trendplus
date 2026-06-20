# Replenishment / OOS Workflow Audit

Updated: 2026-06-19
HEAD SHA: `72a1db59edfa332d66f31f2c930ecea69f0824a4`

## Scope Reviewed

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.signalActions.spec.ts`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx`
- `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
- `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.tsx`
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `docs/analytics/RETAIL_ANALYTICS_KPI_ROADMAP.md`
- `docs/Analytics/INVENTORY_UX_AUDIT.md`

## Current Behavior

- Inventory keeps replenish, OOS risk, slow stock, transfer, and workflow actions on the same decision surface.
- `buildInventorySignalActionSpec()` routes `insufficient_data` and `recommendationAllowed=false` rows to `SIGNAL_REVIEW` instead of a final replenish action.
- Missing cost or quantity baseline keeps expected impact nullable in the helper path that builds signal actions.
- The Inventory page shows a warning banner when response meta is stale, partial, or fallback.
- Forecast-driven restock suggestions now require a loaded stock baseline before the page will queue a workflow action.

## Findings

### SAFE

- `buildInventorySignalActionSpec()` does not promote `insufficient_data` into a final replenish recommendation.
- The helper keeps the expected impact nullable when the baseline is missing.
- Inventory stale/partial meta remains visible in the UI instead of looking green.
- `ActionWorkflowPanel` shows action value from the workflow item and does not invent a separate money metric.

### WATCH

- Forecast sorting still uses a `0` fallback for items that do not have a matching loaded row, but that value is only used for sort order.
- `DemandForecastPanel` can surface high OOS risk items that are outside the current loaded table page; those items should not be queued unless a baseline row is present.

### BUG

- Forecast restock actions previously could be queued from a forecast item even when the matching inventory baseline row was missing, which created a fake zero-value fallback row.

## Fixes Made

- `InventoryPage` now refuses to queue a forecast restock action if the loaded baseline row is missing.
- The page surfaces a clear message instead of fabricating a zero-cost/zero-value fallback item.
- Added a focused test that proves the forecast restock action is blocked when no baseline row is loaded.
- Added a stale-meta regression test for the inventory page warning state.
- Expanded the signal-action helper test coverage for missing baseline and nullable impact behavior.

## Test Coverage

- `InventoryPage.signalActions.spec.ts`
  - insufficient-data signals stay in `SIGNAL_REVIEW`
  - missing baseline keeps expected impact nullable
- `InventoryPage.forecastRestock.spec.tsx`
  - forecast restock is blocked when the baseline row is absent
- `AnalyticsSalesReadinessRegression.spec.tsx`
  - stale inventory meta shows warning copy instead of looking healthy

## Known Limitations

- The forecast panel still highlights items from the forecast snapshot even when the corresponding current page row is not loaded.
- This pass keeps the workflow conservative by blocking queueing in that case instead of trying to synthesize an artificial row.
- No new replenishment model or forecasting engine was added.

## Follow-Ups

- If the UX should support cross-page forecast queueing later, add an explicit non-zero baseline contract before enabling it.
- Keep replenishment/OOS actions labeled as signal-driven or estimated whenever the backend contract is partial.
- Revisit the forecast sort fallback if a future report needs a richer missing-baseline state than plain zero ordering.
