Task ID: DEX19
Queue: docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
Date: 2026-08-14
Agent/tool: Codex
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending
Main verification: pending

## What was done
- Added backend-led `confidenceSource` to Executive Decision Board product cards.
- Kept the source label authoritative by mapping blocked / insufficient-data product recommendations to `workflow_status_only` and the rest to `signal`.
- Added backend and frontend regressions so the product explainability source label is visible and tested end-to-end.

## Files changed
- Api/Endpoints/DecisionBoardEndpoints.cs
- Api.Tests/DecisionBoardEndpointsTests.cs
- Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.reuse.spec.tsx
- `.ai/runs/2026-08-14-DEX19-evidence.md`

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~DecisionBoardEndpointsTests` -> pass (33)
- `npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.reuse.spec.tsx` -> pass
- `git diff --check` -> pass

## Validation not run
- `dotnet build` -> not run, focused backend test already compiled the touched project
- `npm run build` -> not run, focused validation was sufficient

## What was missed
- No broader board-contract audit was needed beyond the product `confidenceSource` gap.

## Risks
- `confidenceSource` is now explicit on product cards, but the board still relies on existing backend confidence-level semantics for the label mapping.

## Next
- DEX19 remains current READY in `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` until the queue owner closes or replaces it.
