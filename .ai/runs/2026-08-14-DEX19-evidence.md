Task ID: DEX19
Queue: docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
Date: 2026-08-14
Agent/tool: Codex
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending
Main verification: git rev-parse HEAD -> a7c642e3c7c7e2597e6b478765157525ddfcbb4c

## What was done
- Added backend-led `confidenceSource` and `RecommendationAllowed` to Executive Decision Board product cards.
- Kept the source label authoritative by mapping blocked / insufficient-data product recommendations to `workflow_status_only` and the rest to `signal`.
- Mirrored the same contract in the frontend fallback product helper so the legacy product path does not drop recommendation allowance or confidence source.
- Added backend and frontend regressions so the product explainability source label and recommendation allowance are visible and tested end-to-end.
- Synchronized the DEX19 queue/roadmap ledgers so the prompt is no longer listed as READY.

## Files changed
- Api/Endpoints/DecisionBoardEndpoints.cs
- Api.Tests/DecisionBoardEndpointsTests.cs
- Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx
- Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.reuse.spec.tsx
- Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts
- docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
- docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md
- MASTER_ROADMAP.md
- .ai/runs/2026-08-14-DEX19-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~DecisionBoardEndpointsTests` -> pass (33)
- `npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts src/pages/__tests__/ExecutiveDecisionBoardPage.reuse.spec.tsx` -> pass
- `git diff --check` -> pass

## Validation not run
- `dotnet build` -> not run, focused backend test already compiled the touched project
- `npm run build` -> not run, focused validation was sufficient

## What was missed
- No broader board-contract audit was needed beyond the product `confidenceSource` and `RecommendationAllowed` gaps.

## Risks
- `confidenceSource` and `RecommendationAllowed` are now explicit on product cards, but the board still relies on existing backend confidence-level semantics for the label mapping.

## Next
- none
