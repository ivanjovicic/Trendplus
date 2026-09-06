# RQ158 evidence

Task ID: RQ158
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Cursor
Delivery target: main
Working branch / PR: main
Main commit SHA: (set after push)
Main verification: pending push
Evidence state: synchronized

## What was done

Preserved null inventory quantity/minimum as unavailable evidence instead of coalescing to measured zero OOS or stable stock.

1. `InventoryStockEvidence` helper — OOS/low-stock/estimated-value contracts.
2. `GetInventoryStatusHandler` — OOS only `Kolicina == 0`; low stock requires known positive quantity.
3. `InventoryEndpoints` balance — null qty not OOS; low stock requires known minimum; estimated value only when cost+qty known.
4. List/detail — preserve nullable fields; missing quantity → insufficient signal (no fake OOS).
5. Frontend `inventoryUtils` / types / InventoryPage sort — unknown stock state distinct from measured zero.

## Files changed

- `Application/Analytics/InventoryStockEvidence.cs` (new)
- `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusHandler.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Dtos/InventoryListItemDto.cs`
- `Api/Dtos/InventoryExperienceDtos.cs`
- `Api.Tests/InventoryStockEvidenceTests.cs` (new)
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- `Klijent/clientapp/src/components/inventory/types.ts`
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.fakeZeroValue.spec.ts`
- queue + lock + this evidence

## Validation run

```text
dotnet test --filter InventoryStockEvidenceTests|InventoryListEndpointIntegrationTests|InventorySignalCalculatorTests
→ Passed: 21

npm run test -- --run InventoryPage.fakeZeroValue.spec.ts InventoryItemsTable.spec.tsx
→ Passed: 16

npm run typecheck → pass
```

## Validation not run

- Full Api.Tests / full frontend suite
- CachedAnalyticsEndpoints inventory balance duplicate path (out of scoped InventoryEndpoints owner; residual risk)
- Live browser / STAB16

## Documentation impact

Queue completion + run log only.

## What was missed

- Cached dashboard inventory balance still uses null→0 coalescing (same bug pattern; separate follow-up if needed).
- Insights dataset still maps null quantity to 0 for internal ABC ranking when building InventoryDatasetItem.

## Risks

- More SKUs may show unknown/insufficient instead of false OOS/stable — intentional.
- Estimated inventory value aggregate may drop when cost missing (no longer multiplies by 0 cost).

## Next

RQ159 READY (decision summary count arithmetic).
