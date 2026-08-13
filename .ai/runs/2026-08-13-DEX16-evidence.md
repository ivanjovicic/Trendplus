Task ID: DEX16
Queue: docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Codex
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: 345b7e692b4b5c0eefea768746bb1020c08b10e0
Main verification: git rev-parse origin/main -> 345b7e692b4b5c0eefea768746bb1020c08b10e0

## What was done
- Wired the backend-led inventory explainability snapshot into inventory detail and insight runtime paths.
- Added shared frontend snapshot rendering for the detail modal and insight cards.
- Added backend integration coverage and frontend component tests for the new snapshot surfaces.
- Updated the DEX queue/router state to stage the next supplier explainability runtime prompt as DEX17.
- Reconciled the Premium UI queue/router pointer so the current READY prompt is consistent again.
- Re-ran governance checks on the final queue/router state.

## Files changed
- Api/Dtos/InventoryExperienceDtos.cs
- Api/Endpoints/InventoryEndpoints.cs
- Api.Tests/InventoryListEndpointIntegrationTests.cs
- Klijent/clientapp/src/components/inventory/InventoryExplainabilitySnapshot.tsx
- Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx
- Klijent/clientapp/src/components/inventory/InventoryInsightPanels.spec.tsx
- Klijent/clientapp/src/components/inventory/SKUDetailModal.tsx
- Klijent/clientapp/src/components/inventory/SKUDetailModal.spec.tsx
- Klijent/clientapp/src/components/inventory/inventoryUtils.ts
- Klijent/clientapp/src/types/analytics.ts
- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
- docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
- docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md
- docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md
- MASTER_ROADMAP.md

## Validation run
- `git diff --cached --check` -> pass
- `dotnet test Api.Tests/Api.Tests.csproj --filter InventoryListEndpointIntegrationTests` -> pass
- `cd Klijent/clientapp; npm run test -- --run src/components/inventory/SKUDetailModal.spec.tsx` -> pass
- `cd Klijent/clientapp; npm run test -- --run src/components/inventory/InventoryInsightPanels.spec.tsx` -> pass
- `cd Klijent/clientapp; npm run check:analytics-guardrails` -> pass
- `cd Klijent/clientapp; npm run build` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass
- `git diff --check` -> pass

## Validation not run
- none

## What was missed
- No broader inventory report/export follow-up was taken beyond the runtime snapshot surfaces.
- Supplier explainability runtime wiring remains staged as the next prompt, not yet implemented.

## Risks
- Detail and insight sell-through evidence can still surface insufficient-data reasons when the seed lacks denominator evidence.
- The current DEX17 supplier prompt still needs implementation work in a later session.

## Next
- DEX17 supplier explainability snapshot runtime slice
