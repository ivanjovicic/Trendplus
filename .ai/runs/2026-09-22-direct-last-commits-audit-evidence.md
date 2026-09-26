Task ID: direct-last-commits-audit
Queue: direct-user-request
Date: 2026-09-22
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 3b635fe813e0e55f843236d55025c52c030e853d
Main verification: passed - local `main` and `origin/main` both resolved to `3b635fe813e0e55f843236d55025c52c030e853d` before documentation closure.
Evidence state: synchronized

## What was done
- Audited the latest Pre-Nivelacija and Color runtime commits against RQ388-RQ393 contracts and the repository analytics invariants.
- Closed the confirmed RQ393 gap: Color recommendations now use a covered-revenue-weighted margin baseline instead of a simple row average.
- Kept unknown-color share nullable and fail-closed through the shared recommendation engine; no missing denominator is converted to zero.
- Exposed the weighted baseline and covered-revenue denominator in Color metadata/totals, and separated historical, fallback and truly unavailable cost coverage.
- Replaced confirmed English user-facing labels in the affected Color and Pre-Nivelacija decision surfaces with Serbian wording.
- Added backend/frontend regression coverage and synchronized queue/roadmap status.

## Files changed
- Application/Analytics/AnalyticsDecisionRecommendationEngine.cs
- Application/Analytics/ColorSignedEvidencePolicy.cs
- Api/Endpoints/AllEndpoints.cs
- Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs
- Api.Tests/AnalyticsMarginPolicyTests.cs
- Klijent/clientapp/src/services/colorSalesStatsApi.ts
- Klijent/clientapp/src/validation/analyticsResponseSchemas.ts
- Klijent/clientapp/src/validation/__tests__/analyticsResponseSchemas.spec.ts
- Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx
- Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx
- Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.spec.tsx
- Klijent/clientapp/scripts/known-guardrail-baseline.json
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-22-direct-last-commits-audit-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~AnalyticsDecisionRecommendationEngineTests|FullyQualifiedName~AnalyticsMarginPolicyTests"` -> pass, 23/23.
- `npm run test:run -- src/validation/__tests__/analyticsResponseSchemas.spec.ts src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` -> pass, 73/73.
- `npm run typecheck` -> pass.
- `npm run check:analytics-guardrails` -> initial fail on a shifted known baseline line, then pass after baseline synchronization; final result 0 new violations, 51 reviewed findings.
- `node ./scripts/check-encoding.mjs` -> pass.
- `git diff --check` -> pass.
- `git push origin main` -> pass for implementation SHA `3b635fe813e0e55f843236d55025c52c030e853d`.

## Validation not run
- Full backend/frontend suites -> not run; focused tests cover the changed contracts.
- Live deployed API/provider/database replay -> not run.
- Remote CI result -> not inspected.

## Documentation impact
- Marked RQ393 DONE in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.
- Updated `MASTER_ROADMAP.md` to reflect the delivered weighted-margin contract and remaining waiting follow-ups.

## What was missed
- RQ394-RQ400 remain WAITING and were not silently expanded into this audit.
- Shared cross-screen localization remains owned by RQ306/RQ325.

## Risks
- No live dataset replay was available to verify weighted margin behavior against production-shaped Color rows.
- Existing unrelated compiler/analyzer warnings and remote CI state remain outside this run's proof.

## Next
- RQ394 is the next Color follow-up for comparable-cohort parity; RQ395-RQ400 remain waiting under their named dependencies.
