# RQ162 evidence

Task ID: RQ162
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Cursor
Delivery target: main
Working branch / PR: main
Main commit SHA: (set after push)
Main verification: pending push
Evidence state: synchronized

## What was done

Blocked partial sell-through denominators in `InventorySignalCalculator`:

- Both `openingStockUnits` and `inboundUnits` required (aligned with `analyticsMetricDefinitions.sellThrough`).
- Either missing → null ratio, `insufficient_data`, `recommendationAllowed=false` (no `?? 0`).
- Negative inputs fail closed.
- Genuine zero sold units with positive denominator remains measured `0`.

## Files changed

- `Api/Endpoints/InventorySignalCalculator.cs`
- `Api.Tests/InventorySignalCalculatorTests.cs`
- queue + lock + this evidence

## Validation run

```text
dotnet test --filter InventorySignalCalculatorTests|InventoryListEndpointIntegrationTests
→ Passed (focused set)
```

## Validation not run

- Full Api.Tests suite
- Frontend (backend-owned contract; list/detail already consume calculator output)

## Documentation impact

Queue completion + run log. Metric definition already documented both inputs as required.

## What was missed

Call sites already pass inbound as non-null int from movement stats; partial-null is enforced at calculator boundary for all callers.

## Risks

More rows may show insufficient sell-through when opening stock cannot be derived — intentional fail-closed.

## Next

RQ163 READY (supplier post-observation null→zero).
