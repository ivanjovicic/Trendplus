Task ID: post-commit-react-csharp-review-9
Queue: none (direct user request)
Date: 2026-08-13
Agent/tool: Cursor
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending-push
Main verification: pending-push

## What was done
- Reviewed leftover React/C# gaps after `d0614c6`: timeline Slice-2 `117dbda` still embedded raw `REPLENISH` in backend `ScopeExplanation`; Prodaja pre/post `d6eadf4` still invented Visoko/Srednje/Nisko bands for available reliability; Daily Sales empty-state leftover CSS from `7c756de`.
- `ScopeExplanation` now uses `ProductDecisionReasoningHelper.RecommendationLabel` (`Porodica: Dopuni`) while structured `RecommendationType` stays `REPLENISH`.
- Prodaja pre/post shows backend `fmtPct` on the reliability pill; local 70/40 thresholds remain only for the existing `Nisko poverenje` filter tone.
- Removed unused `.daily-sales-no-data-banner` CSS.

## Files changed
- Application/Analytics/ProductDecisionReasoningHelper.cs
- Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs
- Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs
- Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs
- Api.Tests/ProductDecisionReasoningHelperTests.cs
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx
- Klijent/clientapp/src/pages/DailySalesStatsPage.css
- .ai/runs/2026-08-13-post-commit-react-csharp-review-9-evidence.md

## Validation run
- cd Klijent/clientapp && npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx -> pass (5/5)
- dotnet test .\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~AnalyticsActionTimelineFilterProjectionTests|FullyQualifiedName~ProductDecisionReasoningHelperTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests -> pass (12/12)
- cd Klijent/clientapp && npm run check:analytics-guardrails -> pass

## Validation not run
- npm run build -> not run - types covered by analytics guardrails typecheck
- full dotnet test suite -> not run - filtered to owner tests

## What was missed
- Prodaja `Nisko poverenje` filter still uses local 70/40 tone thresholds because backend does not expose a reliability level.
- Actions ledger can still show English snapshot prose (`expectedImpactBasis`, `decisionReason`) when the backend stored it that way.
- CachedAnalyticsEndpoints still has its own private `RecommendationLabel` copy.

## Risks
- Unknown recommendation families pass through unchanged into `ScopeExplanation`.
- Filter chip counts for `Nisko poverenje` still depend on frontend banding.

## Next
- Push to origin/main.
