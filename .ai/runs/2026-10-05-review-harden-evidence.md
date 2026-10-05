Task ID: review-harden-2026-10-05
Queue: direct-user-request (acceptance review of today's C#/React analytics commits)
Date: 2026-10-05
Agent/tool: Grok Bot (executor, box worktree `/workspace/rh`)
Delivery target: main
Working branch / PR: local branch `review-harden-2026-10-05` from `origin/main` `848a9db (includes RQ571 same-day close)`
Main commit SHA: pending
Main verification: pending (owner PC fast-forward push via bundle)
Evidence state: pending

## What was done
- Refetched `origin/main` at start and again before commit; HEAD stayed `959a67e` (no newer commits during the review).
- Reviewed closed prompts RQ569–RQ584, RQ587, RQ479, RQ48, RQ88, RQ552/553, RQ589–RQ591 against code+tests; tried to falsify the known hunches.
- Fixed real residuals:
  1. RQ581 leftover mojibake in Insight Studio urgency/aging labels (broke FE filters).
  2. RQ590 V1 reorder-plan still turned missing selling price into 0 revenue.
  3. RQ591 FE `fmtRsd(0)` fallback and non-null V2 plan cost types.
  4. Removed dead InventoryEndpoints aging helpers that fell back to `updatedAt` (RQ576 regression risk).
- Hunches discarded or already covered: RQ587 Admin diagnostic exposes AutoMigrate/FailFast; RQ479 fixture quarantine works; RQ576 live aging uses receipt policy; RQ583 global banner is wired; RQ584 Daily half-open has follow-up tests.

## Files changed
- `Api/Endpoints/InsightStudioEndpoints.cs`
- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api.Tests/InsightStudioErrorResponseContractTests.cs`
- `Api.Tests/InsightStudioLegacyEndpointsContractTests.cs`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`
- `Klijent/clientapp/src/pages/insightStudioTrustPresentation.ts`
- `Klijent/clientapp/src/pages/__tests__/insightStudioTrustPresentation.spec.ts`
- `Klijent/clientapp/src/services/insightStudioApi.ts`
- `Klijent/clientapp/src/services/insightStudioV2Api.ts`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` (RQ581/RQ590/RQ591 addenda)
- `.ai/runs/2026-10-05-review-harden-evidence.md` (this file)

## Validation run
- `dotnet test --filter FullyQualifiedName~InsightStudioErrorResponseContractTests|FullyQualifiedName~InsightStudioLegacyEndpointsContractTests.ReorderPlan` → Passed 5/5
- `npm test -- --run src/pages/__tests__/insightStudioTrustPresentation.spec.ts` → Passed 7/7
- `git diff --check` → pass
- Doc validators if queue touched: pass

## Validation not run
- Full API suite / full frontend suite
- Live production re-probe

## Documentation impact
- Dated residual addenda on DONE RQ581/RQ590/RQ591; no status change.

## What was missed
- Broader Insight Studio comment-encoding cosmetics (box-drawing characters) left alone; not user-facing.

## Risks
- Insight Studio remains behind the Eksperimentalno flag (RQ582); the encoding/nullability fixes still matter for anyone who opens it.

## Post-close routing recovery
- not applicable (review/fix pass). No READY pointer change.

## Next
- Owner: push the bundle to main. Optional live re-check of Insight Studio urgency filters after deploy.
