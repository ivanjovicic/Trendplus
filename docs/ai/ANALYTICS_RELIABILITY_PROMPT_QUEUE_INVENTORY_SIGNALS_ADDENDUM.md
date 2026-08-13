# Analytics Reliability Prompt Queue - Inventory Signals Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum (`RQ89`/`RQ90`/`RQ99` DONE; post-BCI inventory foundations `RQ96`-`RQ98` remain WAITING)
Owner-promoted inventory test follow-up: `RQ101` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` (DONE; EOF-strict proofs landed with RQ101)
Historical routing snapshot: `RQ01` was once the main-queue READY pointer; use `MASTER_ROADMAP.md` and the current queue headers now.

Use with:

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- `docs/qa/ANALYTICS_INVENTORY_SIGNAL_RELIABILITY_AUDIT.md`

Purpose: queue follow-up fixes for inventory forecast/rebalance/alerts/size-curve signal trust after the inventory signal audit.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ64 | DONE | inventory-snapshot-null-evidence | Prevent snapshot nulls from becoming fake zero/info/false |
| RQ65 | DONE | inventory-signal-total-count | Distinguish returned rows from total matching rows/truncation |
| RQ66 | DONE | inventory-placeholder-zero | Stop synthetic detail placeholders from creating fake zero baseline |
| RQ67 | DONE | forecast-workflow-value-trust | Avoid zero-value forecast workflow suggestions when cost missing |
| RQ68 | DONE | inventory-signal-search-lineage | Align/search-label inventory signal panels with active filters |
| RQ69 | DONE | rebalance-store-filter-lineage | Apply/label selected store scope for rebalance suggestions |
| RQ70 | DONE | forecast-suggested-qty-semantics | Clarify forecast restock suggested quantity semantics |
| RQ71 | DONE | size-curve-boolean-evidence | Stop size-curve missing boolean evidence from becoming healthy false |
| RQ89 | DONE | inventory-list-route-contract | Preserve seeded rows and honest empty-success semantics in inventory lists |
| RQ96 | WAITING | observed-inventory-snapshot-foundation | Add canonical observed daily inventory snapshot foundation |
| RQ97 | WAITING | forecast-snapshot-provenance | Prove forecast snapshot ownership/materializer contract |
| RQ98 | WAITING | forecast-backtesting-baseline | Add deterministic forecast baseline and backtesting contract |
| RQ99 | DONE | inventory-signal-reader-regression | Add provider-strict reader-position regression tests for signal total counts |

---

## RQ64 - Inventory snapshot null evidence must not become fake zero/info/false

Status: DONE
Ready after: n/a
Priority: P0
Type: backend-contract/tests
Feature family: inventory-snapshot-null-evidence
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): preserve missing snapshot evidence`

### Why

Forecast, rebalance, alert and size-curve snapshot handlers coalesce many nullable metrics to `0`, `'info'` or `false`. Missing evidence must not look like true zero risk, normal alert severity, or healthy size curve.

### Evidence already found

- `GetInventoryForecastHandler.cs`: `coalesce(forecast_7d, 0)`, `coalesce(probability_of_oos_in_7d, 0)`, `coalesce(overstock_risk, 0)`, `coalesce(confidence_score, 0)`.
- `GetRebalanceSuggestionsHandler.cs`: `coalesce(recommended_qty, 0)`, `coalesce(confidence, 0)`, `coalesce(expected_saved_sales, 0)`, `coalesce(expected_capital_release, 0)`.
- `GetInventoryAlertsHandler.cs`: `coalesce(severity, 'info')`, `coalesce(confidence_score, 0)`.
- `GetInventorySizeCurveHandler.cs`: numeric nulls to 0, boolean nulls to false, confidence to 0.

Risk class: likely fake-confidence bug.

### Contract

- Missing numeric evidence must be nullable or carry `sourceStatus/evidenceStatus`.
- Missing severity must not become normal `info` without a warning/reason.
- Missing boolean evidence must not become healthy `false` without `evidenceStatus=missing`.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1` (HEAD at validation time)
- Changed files:
  - `Application/Analytics/Queries/DbDataReaderNullableExtensions.cs`
  - `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
  - `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastQuery.cs`
  - `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
  - `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsQuery.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveQuery.cs`
  - `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
  - `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsQuery.cs`
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx`
  - `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
  - `Klijent/clientapp/src/components/inventory/SizeCurveVisualization.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`
  - `Klijent/clientapp/src/types/analytics.ts`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --filter "InventorySnapshotContractTests"`: pass
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: remaining RQ65-RQ71 items still need follow-up; this task only resolves fake-zero / fake-green evidence handling.
- Next: RQ65 - Inventory signal total count and truncation semantics

### Scope only

- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
- `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
- `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
- DTOs/types only if needed
- targeted tests

### Do not touch

- snapshot materialization SQL
- Inventory page visual redesign
- action queue write behavior

### Test matrix

- trusted positive numeric values remain unchanged.
- true zero values remain zero with evidence loaded.
- null risk/confidence/value returns null or missing-evidence status.
- null alert severity is not silently `info`.
- null size-curve booleans are distinguishable from false.

### Acceptance

- No missing inventory signal evidence is silently converted into trusted zero/info/false.

---

## RQ65 - Inventory signal total count and truncation semantics

Status: DONE
Ready after: RQ64 or explicit unblocking
Priority: P1
Type: backend-contract/frontend-tests
Feature family: inventory-signal-total-count
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): expose signal result truncation`

### Why

Forecast/rebalance/alerts/size-curve handlers return `TotalCount = items.Count` after `limit @top`. UI can present this as the total number of matching signals.

### Evidence already found

- Forecast handler selects with `limit @top` and returns `TotalCount: items.Count`.
- Rebalance, alerts and size-curve follow the same pattern.
- Panels display labels such as `N SKU u prognozi` or `N predloga`.

Risk class: likely count/truncation semantics bug.

### Contract

- `returnedCount`: number of rows returned to the client.
- `totalMatchingCount`: total matching rows before limit, if cheaply available.
- `isTruncated`: true when returned rows are capped.
- If total matching count is expensive, label UI as “prikazano N” and include top limit.

### Scope only

- inventory signal query handlers
- inventory signal DTOs/types
- inventory panels count labels/tests

### Do not touch

- snapshot generation logic
- unrelated inventory balance/list pagination

### Test matrix

- fewer rows than top: `isTruncated=false`.
- exactly top rows but unknown total: UI says “prikazano do N”, not total certainty.
- more rows than top: `isTruncated=true` or total matching count is higher than returned count.

### Acceptance

- UI no longer implies limited result count is the total matching signal count.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1`
- Changed files:
  - `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
  - `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsQuery.cs`
  - `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
  - `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastQuery.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveQuery.cs`
  - `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
  - `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsQuery.cs`
  - `Api.Tests/InventorySnapshotContractTests.cs`
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx`
  - `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
  - `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`
  - `Klijent/clientapp/src/types/analytics.ts`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --filter "InventorySnapshotContractTests"`: pass
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastGuardrails.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: `TotalCount` is still a compatibility field; consumers should move to `ReturnedCount` and `TotalMatchingCount` for exact semantics.
- Next: RQ66 - Synthetic inventory detail placeholder fake-zero baseline

---

## RQ66 - Synthetic inventory detail placeholder fake-zero baseline

Status: DONE
Ready after: RQ60 or explicit unblocking
Priority: P1
Type: frontend-tests
Feature family: inventory-placeholder-zero
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): avoid zero placeholder detail rows`

### Why

`openDetailBySku` creates a placeholder `InventoryRow` with zero quantity, minimum, cost and estimated value when the SKU is not in current rows. This can make missing context look like true zero inventory while detail loads or if detail fails.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `SKUDetailModal` only if needed
- frontend tests

### Do not touch

- backend item detail endpoint
- inventory valuation formula

### Contract

- Placeholder rows must be labelled `contextMissing` / `loadingContext`, not zero baseline.
- Unknown quantity/cost/value must render as unknown/not available, not zero.
- Detail fetch failure must not leave a fabricated zero stock card.

### Test matrix

- SKU exists in current rows: normal detail opens.
- SKU missing from current rows: placeholder shows loading/unknown state.
- detail fetch fails: no fake zero quantity/value remains.

### Acceptance

- Synthetic detail placeholders cannot be mistaken for true zero inventory.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1`
- Changed files:
  - `Klijent/clientapp/src/components/inventory/SKUDetailModal.spec.tsx`
  - `Klijent/clientapp/src/components/inventory/SKUDetailModal.tsx`
  - `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
  - `Klijent/clientapp/src/components/inventory/types.ts`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
- Checks:
  - `npm run test -- --run src/components/inventory/SKUDetailModal.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: placeholder rows still exist transiently while detail data loads, but the modal no longer renders them as a trusted zero baseline.
- Next: RQ67 - Forecast workflow value trust

---

## RQ67 - Forecast workflow value trust

Status: DONE
Ready after: RQ60/RQ64 or explicit unblocking
Priority: P1
Type: frontend/action-contract/tests
Feature family: forecast-workflow-value-trust
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): mark forecast workflow value reliability`

### Why

`queueForecastRestock` calculates `estimatedValue = row.unitCost * suggestedQty`. `row.unitCost` can be zero when cost is missing, creating a zero-value workflow suggestion.

### Scope only

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- action workflow DTO/types only if needed
- frontend tests

### Contract

- Forecast workflow value should be `estimatedValueRsd` only when cost is known or explicitly estimated.
- Missing cost should set `costMissing=true` or omit value.
- UI/action queue must distinguish zero value from unknown value.

### Test matrix

- known unit cost -> estimated value shown.
- missing unit cost -> no zero-value suggestion; warning/status preserved.
- action payload metadata includes value reliability.

### Acceptance

- Forecast workflow suggestions do not show missing cost as zero value.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1` (HEAD at validation time)
- Changed files:
  - `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.spec.tsx`
  - `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: only the forecast workflow path now distinguishes missing cost from known value; other workflow sources remain unchanged.
- Next: `RQ68 - Inventory signal search/filter lineage`

---

## RQ68 - Inventory signal search/filter lineage

Status: DONE
Ready after: RQ65 or explicit unblocking
Priority: P1
Type: frontend/API-contract/tests
Feature family: inventory-signal-search-lineage
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): clarify signal search filter lineage`

### Why

Inventory list and action workflow use search, but forecast/alerts/rebalance signal panels are loaded by store/supplier only. UI copy can imply they follow all current filters.

### Scope only

- `InventoryPage.tsx`
- signal query APIs only if adding search support
- inventory signal panels/tests

### Contract

Choose one:

- Add search support to signal APIs and pass `trimmedSearch`, or
- Clearly label signal panels as scoped only by store/supplier, not text search.

### Test matrix

- active search filter changes list.
- signal panels either refetch with search or show visible “search not applied” note.
- export/report metadata states signal panel filter scope.

### Acceptance

- User cannot believe signal panels are filtered by search when they are not.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1` (HEAD at validation time)
- Changed files:
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx`
  - `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: signal panels still scope data by store/supplier only; the UI now makes that scope explicit instead of implying text-search lineage.
- Next: `RQ69 - Rebalance store filter lineage`

---

## RQ69 - Rebalance selected-store filter lineage

Status: DONE
Ready after: RQ68 or explicit unblocking
Priority: P1
Type: frontend/API-contract/tests
Feature family: rebalance-store-filter-lineage
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): align rebalance store scope`

### Why

Inventory page calls rebalance suggestions with supplier/top only, while selected store filter can be active. The backend query supports `fromStoreId` and `toStoreId`.

### Contract

When selected store filter is active, choose and document one behavior:

- show suggestions where selected store is source or destination, or
- show global rebalance suggestions with explicit “all stores” label, or
- provide a separate rebalance store filter.

### Scope only

- `InventoryPage.tsx`
- rebalance query contract if needed
- `RebalancingTable.tsx` labels/tests

### Test matrix

- no selected store: global suggestions labelled global.
- selected store active: rebalance scope is constrained or clearly labelled.
- compare-store action still works.

### Acceptance

- Rebalance panel does not silently ignore selected inventory store filter.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1`
- Changed files:
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
  - `Klijent/clientapp/src/components/inventory/RebalancingTable.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.queueStatus.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/components/inventory/RebalancingTable.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventoryPage.queueStatus.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: rebalance suggestions now follow the selected store scope through `fromStoreId`; clear the store filter to see global suggestions.
- Next: `RQ70 - Forecast restock suggested quantity semantics`

---

## RQ70 - Forecast restock suggested quantity semantics

Status: DONE
Ready after: RQ67 or explicit unblocking
Priority: P2
Type: frontend-contract/tests
Feature family: forecast-suggested-qty-semantics
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `docs(inventory): clarify forecast suggested quantity semantics`

### Why

Forecast panel creates workflow suggestion with `suggestedQty = ceil(forecast7d)`. This may be a demand signal, not an operational reorder quantity, because it does not consider stock baseline/gap explicitly.

### Contract

- If keeping `ceil(forecast7d)`, label it as `forecastDemandQty`, not final reorder quantity.
- If producing reorder qty, incorporate current stock/gap/minimum and evidence status.

### Scope only

- `InventoryPage.tsx`
- `DemandForecastPanel.tsx`
- tests/docs

### Test matrix

- current stock sufficient but forecast high: not final reorder qty unless contract says so.
- low stock and forecast high: signal quantity visible with caveat.
- UI/action metadata labels quantity source.

### Acceptance

- Forecast suggested quantity cannot be mistaken for a confirmed purchase/replenishment order.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1`
- Changed files:
  - `Klijent/clientapp/src/types/analytics.ts`
  - `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
  - `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.spec.tsx`
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
  - `Klijent/clientapp/src/components/inventory/DemandForecastPanel.spec.tsx`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
- Checks:
  - `npm run test -- --run src/components/inventory/DemandForecastPanel.spec.tsx`: pass
  - `npm run test -- --run src/components/inventory/ActionWorkflowPanel.spec.tsx`: pass
  - `npm run test -- --run src/pages/__tests__/InventoryPage.forecastRestock.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: forecast restock suggestions are still demand signals, not operational reorder quantities; users should confirm stock baseline before purchase decisions.
- Next: `RQ71 - Size-curve boolean evidence status`

---

## RQ71 - Size-curve boolean evidence status

Status: DONE
Ready after: RQ64 or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: size-curve-boolean-evidence
Parallel-safe: no
Owner: unassigned
Local lock: none
Commit suggestion: `fix(inventory): preserve size curve boolean evidence`

### Why

Size-curve handler coalesces boolean nulls to false. Missing evidence can look like healthy false for core-size-missing, dead-size and broken-run flags.

### Scope only

- `GetInventorySizeCurveHandler.cs`
- size-curve DTO/types/tests
- UI warning only if field shape changes

### Contract

- Boolean signal value and evidence status must be separate.
- Missing boolean evidence should be nullable or accompanied by `evidenceStatus=missing`.

### Test matrix

- true boolean flags remain true.
- false boolean flags remain false when evidence exists.
- null boolean flags are distinguishable from false.

### Acceptance

- Size-curve missing evidence cannot be rendered as healthy run structure.

### Note

- Date: 2026-08-10
- Commit SHA: `5db83e1`
- Changed files:
  - `Api.Tests/InventorySnapshotContractTests.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
  - `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveQuery.cs`
  - `Klijent/clientapp/src/components/inventory/SizeCurveVisualization.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --filter "InventorySnapshotContractTests"`: pass
  - `dotnet build`: pass
  - `npm run test -- --run src/pages/__tests__/InventorySignalNullEvidence.spec.tsx`: pass
  - `npm run check:analytics-guardrails`: pass
  - `npm run build`: pass
- Risk: chart rendering still depends on container measurement in test/runtime contexts, but missing-evidence status now stays explicit instead of collapsing into a healthy false.
- Next: `RQ89 - Inventory list route contract`

---

## RQ89 - Inventory list route contract

Status: DONE
Ready after: STAB09 DONE (satisfied 2026-08-06); promoted 2026-08-10 as the first remaining BCI04 assertion-repair root cause
Priority: P1
Type: backend/tests
Feature family: inventory-list-route-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ89-<agent>.lock.md`
Commit suggestion: `fix(inventory): preserve inventory list seeded rows`

### Why

`InventoryListEndpointIntegrationTests.InventoryList_ClampsInvalidPagingArguments` expected `totalCount=4` but the cached inventory list route returned `0`. Multiple inventory list tests failed in the same area, which points to a route-contract or count-lineage regression rather than a single paging assertion.

### Evidence already found

- `Api.Tests/InventoryListEndpointIntegrationTests.cs` seeds the in-memory `TrendplusDbContext` and exercises `/api/analytics/cached/inventory/list`.
- The failing test expected seeded rows to remain visible after invalid paging arguments were clamped.
- The cached route lives in `Api/Endpoints/CachedAnalyticsEndpoints.cs`.
- The uncached inventory list route lives in `Api/Endpoints/InventoryEndpoints.cs`.
- `BCI04` grouped this as a real assertion failure after backend restore/build became healthy.
- `STAB09`, `RQ77`, and `RQ78` are already DONE, leaving RQ89/RQ90 as the explicit unresolved BCI04 repair prompts.

---

## RQ99 - Inventory signal total-count reader-position regression

Status: DONE
Priority: P2
Type: backend-tests/test-infrastructure
Feature family: inventory-signal-reader-regression
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ99-<agent>.lock.md`
Commit suggestion: `test(inventory): harden signal reader total count regressions`

### Problem

The inventory forecast, rebalance, alert and size-curve handlers expose `TotalMatchingCount` from `count(*) over()`. A direct 2026-08-13 hotfix moved the count read inside the `ReadAsync` loop so runtime code no longer depends on reading the data reader after EOF, but the existing `InventorySnapshotContractTests` use a permissive reader path and did not prove that failure mode before the fix.

### Evidence

- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
- `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
- `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`
- `Api.Tests/InventorySnapshotContractTests.cs`
- Existing focused tests passed even when the handlers still read `total_matching_count` after the `while (ReadAsync)` loop, so they are not strict enough to protect the provider-position contract.

### Scope

- `Api.Tests/InventorySnapshotContractTests.cs`
- shared analytics test reader/helper only if needed
- the four inventory signal handlers only if the stricter tests expose a real regression

### Do Not Touch

- snapshot SQL/materializers
- DTO shape or API response semantics unless the stricter regression proves a contract bug
- React inventory panels
- unrelated inventory list/action route tests

### Do

1. Add the smallest reusable test double or provider-backed test path that throws when a column is read after EOF.
2. Prove forecast, rebalance, alert and size-curve handlers read `total_matching_count` only while the reader is positioned on a row.
3. Cover at least one empty-result case where `TotalMatchingCount` remains `0` without attempting an EOF read.
4. Keep existing `ReturnedCount`, `TotalMatchingCount` and `IsTruncated` semantics unchanged.
5. If the stricter test helper is too large for one pass, finish `PARTIAL` with the exact helper design and one implemented handler proof.

### Tests

```powershell
dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~InventorySnapshotContractTests"
```

### Acceptance

- The inventory signal total-count contract is protected against permissive reader-test false positives.
- Each signal family proves no post-EOF column access.
- Completion note references the exact durable run log path.

### Dependencies

- This is a hardening follow-up, not a release blocker if the 2026-08-13 hotfix is already present and focused contract tests pass.
- Promote only after higher-priority BCI/STAB/QDB work is not being displaced.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: EOF-strict reader already existed; RQ101 added empty-result TotalMatchingCount proofs for forecast, rebalance, alerts and size-curve and ran them
- Changed files: see RQ101 completion note
- Contract/runtime behavior changed: no
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~InventorySnapshotContractTests"` as part of the RQ101 combined filter - pass
- Checks not run: `--configuration Release` named in this prompt; Debug configuration was used by the RQ101 command
- Run log: .ai/runs/2026-08-13-RQ101-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 3244723ecc05e09718088e2d4df59de050b1f634
- Main verification: git rev-parse origin/main -> 8c667c3b52af0af4b0c2bbf271b305d6713cb397; work SHA 3244723ecc05e09718088e2d4df59de050b1f634 is an ancestor
- Missed: none known for the four-family empty EOF proofs
- Follow-up: none
- Residual risk: none known for reader-position on these handlers
- Prompt defect / scope repair: closed from RQ101 because that prompt required the EOF-strict assertions to be present and run
- Next: `RQ102`

---

## RQ96 - Canonical observed daily inventory snapshot foundation

Status: WAITING
Ready after: `BCI05` is green in GitHub Actions and Gate-1 connector work is no longer the higher-priority blocker
Priority: P1
Type: sql/backend-contract/tests/docs
Feature family: observed-inventory-snapshot-foundation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ96-<agent>.lock.md`
Commit suggestion: `feat(inventory): add observed daily snapshot foundation`

### Problem

Trendplus still lacks a canonical observed SKU/store/day inventory snapshot, which limits truthful historical inventory analytics and keeps later forecasting/backtesting work anchored to reconstruction instead of observed stock evidence.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` identifies a durable SKU/store/day inventory snapshot as the most important analytics data-foundation gap after release/connectors.
- `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql` explicitly states there is no persisted daily inventory snapshot table today and that historical stock is reconstructed backwards from current stock, sales and movements.
- The same SQL builds a stock proxy for analytics signals, which is useful but not equivalent to a durable observed snapshot foundation.

### Scope

- inventory snapshot SQL/materialization files under `Database/Analytics/`
- backend contracts/DTOs that must surface provenance or snapshot availability
- focused analytics tests proving observed-vs-reconstructed semantics
- one durable architecture/QA note if a new canonical snapshot contract is introduced

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`
- current inventory-risk/inventory-signal handlers that rely on reconstructed history

### Do

1. Introduce the smallest durable observed daily inventory snapshot foundation that does not fabricate history.
2. Preserve provenance explicitly: observed snapshot, reconstructed proxy, missing, and mixed evidence must stay distinguishable.
3. Keep true zero separate from missing or unobserved history.
4. Add focused tests proving that downstream analytics can tell whether a day came from observed stock or reconstruction.
5. Do not expand into a generic warehouse rewrite, multi-tenant storage model or broad forecasting implementation in this prompt.

### Tests

- `git diff --check`
- focused SQL/backend contract tests for observed snapshot provenance
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Inventory"`
- any focused SQL validation command already used by the affected snapshot family

### Acceptance

- Trendplus has a canonical observed daily inventory snapshot foundation or a bounded first slice of it with explicit provenance.
- Reconstructed history is no longer indistinguishable from observed stock.
- Later forecast/backtesting prompts can cite one authoritative historical stock source.

### Dependencies

- `BCI05`/`BCI01` green first; do not bypass the current backend-CI override.
- Gate-1 connector work remains higher priority until the owner explicitly promotes this foundation step.
- If source capture cannot yet produce observed snapshots reliably, finish `PARTIAL` with the exact missing source/runtime dependency.

---

## RQ97 - Forecast snapshot provenance and materializer ownership contract

Status: WAITING
Ready after: `BCI05` is green and `RQ96` is `DONE` or an explicit owner note says the forecast provenance contract may proceed independently
Priority: P1
Type: backend/docs/tests
Feature family: forecast-snapshot-provenance
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ97-<agent>.lock.md`
Commit suggestion: `fix(forecast): clarify snapshot ownership and provenance`

### Problem

The runtime exposes forecast snapshot reads, but the repository evidence does not yet prove who materializes `analytics_inventory_forecast_snapshot`, how freshness/ownership is guaranteed, or when the surface should be treated as unavailable rather than productized forecasting.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` states that repository inspection found a forecast snapshot read contract but no proven production materializer/model owner for `analytics_inventory_forecast_snapshot`.
- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs` reads directly from `analytics_inventory_forecast_snapshot` and falls back to `SnapshotAvailable: false` only when the relation is missing.
- The current warning text proves table absence, but not materializer ownership, freshness lineage or approved product use of the forecast surface.

### Scope

- `Application/Analytics/Queries/GetInventoryForecast/`
- any snapshot metadata contract or related SQL/materializer files actually found by evidence
- forecast UI/types only if they must surface provenance/freshness truth
- focused tests/docs for ownership and availability semantics

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
- any existing forecast snapshot SQL/materializer artifacts if present on current `main`

### Do

1. Prove the forecast snapshot owner/materializer path from current-main evidence, or make the contract explicitly say it is still a bounded signal surface.
2. Add the smallest metadata/provenance fields or warnings needed so stale/ownerless forecast data cannot look production-authoritative.
3. Keep missing-table, stale-generation and owner-unknown states distinguishable.
4. Add focused tests for the chosen provenance contract.
5. Do not introduce ML forecasting or broad model infrastructure in this prompt.

### Tests

- `git diff --check`
- focused forecast contract tests
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Forecast|FullyQualifiedName~Inventory"`
- frontend test only if forecast provenance messaging changes on the inventory page

### Acceptance

- Forecast snapshot availability now distinguishes missing relation, stale/unknown provenance, and trusted generated evidence.
- The repo documents or proves who owns/materializes the forecast snapshot.
- Operators cannot mistake an unproven snapshot table for a full production forecasting product.

### Dependencies

- `BCI05`/`BCI01` green first.
- `RQ96` is the preferred foundation order because forecast trust should not outrun historical stock truth without an explicit owner exception.
- If no materializer evidence exists, finish with a fail-closed contract rather than inventing ownership.

---

## RQ98 - Deterministic forecast baseline and backtesting contract

Status: WAITING
Ready after: `RQ97` is `DONE` and a trustworthy historical stock/forecast comparison window exists
Priority: P1
Type: sql/backend/docs/tests
Feature family: forecast-backtesting-baseline
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ98-<agent>.lock.md`
Commit suggestion: `feat(forecast): add baseline backtesting contract`

### Problem

Trendplus cannot yet prove predictive value because there is no canonical baseline/backtesting contract showing how forecast quality is measured against observed outcomes.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` lists deterministic baseline models and backtesting as the first serious forecasting requirements after the current signal surface.
- The same audit explicitly warns against jumping straight to ML before historical stock and backtesting exist.
- Current forecast handlers expose snapshot values and risk/confidence, but no backtesting scorecard or model-versus-baseline evidence was identified in the inspected repository.

### Scope

- forecast evaluation SQL/contracts
- backend/report surfaces that need bounded baseline/backtesting metadata
- focused tests proving error-metric and cohort-window semantics
- one durable architecture/QA note describing allowed metrics and usage limits

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- current forecast snapshot handlers/contracts
- any historical stock foundation landed by `RQ96`

### Do

1. Define the first deterministic baseline and backtesting contract before any broad model upgrade.
2. Use retail-appropriate, bounded error metrics and explicit evaluation windows.
3. Keep sparse/new-item/no-history cohorts explicit rather than hiding them in aggregate scores.
4. Surface backtesting results as evidence only; do not auto-promote them into user-facing certainty without provenance.
5. Avoid ML/platform sprawl, scenario planning and recommendation write-back in this prompt.

### Tests

- `git diff --check`
- focused backtesting contract tests
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Forecast|FullyQualifiedName~Backtest"`
- any targeted SQL validation used by the chosen evaluation artifacts

### Acceptance

- Trendplus has a deterministic baseline/backtesting contract with explicit windows, cohorts and error semantics.
- Forecast quality can be evaluated against observed outcomes without pretending coverage where evidence is missing.
- Later forecast improvements must compare against this baseline instead of bypassing measurement.

### Dependencies

- `RQ97` DONE first so the forecast snapshot provenance/owner contract is settled.
- `RQ96` historical stock foundation must exist, or the owner must explicitly document the limited comparison basis.
- If trustworthy observed outcomes are unavailable, finish `BLOCKED` with the exact missing evidence window.

### Contract

- Seeded inventory rows must survive through both cached and uncached list routes.
- `totalCount` must reflect the full filtered match count before paging.
- Empty successful responses must remain explicit and must not masquerade as errors.
- Error fallback must not fake an empty success if the backend is actually failing.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- shared test fixture/helper only if needed

### Do not touch

- unrelated inventory signal panels
- forecast/rebalance signal handlers
- production refresh scheduling
- analytics actions list (`RQ90` owns that root cause)

### Test matrix

- exact failing test from BCI04 first
- seeded non-empty dataset returns matching `totalCount`
- empty search returns explicit empty-success meta
- invalid paging clamps page and pageSize but still returns seeded rows
- store/supplier/search combinations stay deterministic
- cached and uncached paths preserve the same row/count contract
- fallback/error path does not fake an empty success
- full `InventoryListEndpointIntegrationTests` class passes after the focused fix

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests.InventoryList_ClampsInvalidPagingArguments"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests"`
- Do not mark RQ89 DONE from a narrowed assertion change alone if the class still fails for the same root cause.

### Acceptance

- Inventory list regression no longer collapses seeded rows to zero on the cached route.
- Empty-success behavior stays honest and explicit.
- Focused inventory-list tests are green without weakening assertions or hiding errors.
- On completion, promote `RQ90` next; do not mark `BCI01` DONE until RQ90 and the full backend suite are green.

### Notes

- Date: 2026-08-10
- Files changed:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api/Endpoints/InventoryEndpoints.cs`
  - `Api.Tests/InventoryListEndpointIntegrationTests.cs`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
  - `.ai/task-locks/RQ89-codex.lock.md`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests.InventoryList_ClampsInvalidPagingArguments"` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests"` - pass
  - `git diff --check` - pass with CRLF normalization warnings only
- Risk:
  - other inventory endpoints still use their own empty-success/meta conventions; this task only aligned the inventory list contract and its cached/uncached paths.
- Next:
  - `RQ90 - Analytics actions canonical filter/search/paging contract`
