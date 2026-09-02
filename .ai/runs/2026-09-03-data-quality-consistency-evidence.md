Task ID: DQA-CONSISTENCY-20260903
Queue: direct-user-request
Date: 2026-09-03
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 6ce1591de81a26b57037c2460802d947db3c5f8f
Main verification: passed - local main and origin/main resolve to 6ce1591de81a26b57037c2460802d947db3c5f8f and the delivered SHA is an ancestor of origin/main
Evidence state: synchronized

## What was done
- Added backend regression coverage for null/non-positive cost, missing/invalid supplier mapping, insufficient signal, critical freshness, score wording and dashboard worst-status resolution.
- Corrected revenue health calculations so null and non-positive effective purchase costs are missing, and missing/invalid suppliers affect unknown-supplier revenue.
- Kept broken supplier references distinct from catalog rows with no supplier in the orphan count.
- Included insufficient signals and freshness in intake readiness scoring and blocked recommendations when readiness is critical.
- Made dashboard data-quality status use the worst completeness, freshness and advanced validation status instead of freshness alone.
- Added an explicit decision-set scope label to dashboard quality summaries.
- Clarified the UI distinction between the revenue-weighted health signal and recommendation readiness, improved Serbian labels and methodology text, and made score-card supporting text theme-visible.
- Updated a stale frontend test label so the existing summary-filter reset test matches the user-facing Serbian control.

## Files changed
- Api.Tests/AnalyticsDataQualityConsistencyTests.cs
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Api/Endpoints/DataQualityEndpoints.cs
- Api/Endpoints/DecisionBoardEndpoints.cs
- Infrastructure/Services/AnalyticsDataQualityHealthService.cs
- Klijent/clientapp/src/components/analytics/ExecutiveKpiRow.tsx
- Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx
- Klijent/clientapp/src/components/analytics/__tests__/AnalyticsMethodologyRegistry.spec.tsx
- Klijent/clientapp/src/components/analytics/__tests__/ExecutiveKpiRow.spec.tsx
- Klijent/clientapp/src/pages/AnalyticsDashboard.tsx
- Klijent/clientapp/src/pages/DataQualityPage.css
- Klijent/clientapp/src/pages/DataQualityPage.spec.tsx
- Klijent/clientapp/src/pages/DataQualityPage.tsx
- Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx
- Klijent/clientapp/src/types/analytics.ts
- Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts
- .ai/runs/2026-09-03-data-quality-consistency-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityConsistencyTests|FullyQualifiedName~AnalyticsDataQualityHealthServiceTests|FullyQualifiedName~DecisionBoardDataQualityHealthEvaluationTests|FullyQualifiedName~AnalyticsReportsContractTests" --no-build --no-restore --verbosity quiet` -> pass, 51/51.
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --verbosity quiet` -> pass, 0 errors and 0 warnings for the build command.
- `npm run test:run -- src/components/analytics/__tests__/ExecutiveKpiRow.spec.tsx src/components/analytics/__tests__/AnalyticsMethodologyRegistry.spec.tsx src/pages/DataQualityPage.spec.tsx src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx` -> pass, 35/35.
- `npm run check:analytics-guardrails` -> pass; encoding, analytics guardrails and typecheck passed.
- `npm run build` -> pass; production frontend build completed successfully.
- `npm run check:bundle-budget` -> pass.
- `git diff --check` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --no-restore --verbosity quiet` -> fail, 1081/1097 passed; failures are in pre-existing data-source/integration coverage and local Neon authentication/test-host setup, not the focused analytics changes.
- Production smoke checks for health, intake and dashboard -> endpoints returned HTTP 200, but the live Render instance still returned the pre-deployment contract: intake readiness `good` with 12,221 insufficient signals and no `scopeLabel`. This is deployment evidence, not a local test failure.

## Validation not run
- Render deployment confirmation through a Render connector/API -> not run - no Render deployment capability or authorized deployment token was available in this session.
- Full frontend suite final completion -> not run - the broad Vitest run produced no output for several minutes after starting; it was stopped after focused coverage was green. The only concrete failure observed before stopping was the stale summary-filter label, which was fixed and passed in isolation and in the focused suite.

## Documentation impact
- Added this durable run log. No product owner documentation required a contract change beyond the code-level metric and UI wording updates.

## What was missed
- The new behavior is on `main`, but the Render service had not redeployed it during verification. Production values will remain old until that deployment completes.
- The full frontend suite was not allowed to finish after the stale test assertion was corrected.

## Risks
- Production remains at risk of showing the old contradictory health/readiness behavior until Render deploys SHA `6ce1591de81a26b57037c2460802d947db3c5f8f`.
- The broad backend suite has environment-dependent failures caused by missing/invalid Neon credentials and relational test-host setup; focused analytics proof is green.

## Next
- Trigger or wait for Render to deploy the current `main`, then repeat the three production smoke checks and confirm intake becomes critical when the signal/freshness evidence is insufficient and dashboard includes the decision-set scope label.
