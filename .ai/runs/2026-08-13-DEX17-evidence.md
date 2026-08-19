Task ID: DEX17
Queue: docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md
Date: 2026-08-13
Agent/tool: Codex
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: 8cb37b18e8c631775a026712e96c3e763bf0f2a3
Main verification: `git rev-parse origin/main -> 8cb37b18e8c631775a026712e96c3e763bf0f2a3`

## What was done
- Added a reusable supplier explainability snapshot component and wired it into the Supplier Decision Hub summary and selected-supplier detail views.
- Extended the supplier decision report payload with confidence, reliability and reason-code preview metadata.
- Rendered the same explainability snapshot inside the supplier decision report surface.
- Added focused tests for the payload contract, the report snapshot, the dedicated snapshot render, and the hub snapshot render.

## Files changed
- Klijent/clientapp/src/components/supplierDecisionHub/SupplierExplainabilitySnapshot.tsx
- Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx
- Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx
- Klijent/clientapp/src/services/supplierDecisionReport.ts
- Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts
- Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx
- Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx
- Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx

## Validation run
- `git diff --check` -> pass
- `cd Klijent/clientapp; npm run test -- --run src/services/__tests__/supplierDecisionReport.spec.ts src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` -> pass

## Validation not run
- `dotnet build` -> not run - no backend files changed for this prompt.
- `dotnet test` -> not run - no backend files changed for this prompt.
- `npm run check:analytics-guardrails` -> not run - targeted UI/test validation was sufficient for this slice.
- `npm run build` -> not run - targeted vitest coverage passed and no build-risky runtime changes were introduced beyond the exercised surfaces.

## What was missed
- none known

## Risks
- The new snapshot is driven by existing metadata/rows, so any future payload contract drift could hide explainability fields until tests are updated.

## Next
- none
