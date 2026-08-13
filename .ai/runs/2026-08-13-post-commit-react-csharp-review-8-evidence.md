Task ID: post-commit-react-csharp-review-8
Queue: none (direct user request)
Date: 2026-08-13
Agent/tool: Cursor
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending-push
Main verification: pending-push

## What was done
- Reviewed leftover React/C# gaps after `7c756de`: Analytics Actions polish `8bfa7c3` still dumped raw `low_cover` / `stock_cover_days` and English `Freshness ulaza`; timeline Slice-2 `117dbda` still stored English gap `message` strings in C#.
- Actions details now map driver/warning codes and ledger freshness/source module to operator labels.
- Timeline projection gap messages are Serbian and locked by a focused C# test so API consumers do not get `No acceptance note was captured.`

## Files changed
- Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx
- Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx
- Infrastructure/Services/Analytics/AnalyticsActionTimelineProjection.cs
- Api.Tests/AnalyticsActionItemServiceTests.cs
- .ai/runs/2026-08-13-post-commit-react-csharp-review-8-evidence.md

## Validation run
- cd Klijent/clientapp && npm run test -- --run src/pages/AnalyticsActionsPage.spec.tsx -> pass (7/7)
- dotnet test .\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~ProjectTimeline -> pass (3/3)
- cd Klijent/clientapp && npm run check:analytics-guardrails -> pass

## Validation not run
- npm run build -> not run - types covered by analytics guardrails typecheck
- full dotnet test suite -> not run - filtered to ProjectTimeline owner

## What was missed
- Prodaja pre/post still uses local 70/40 bands for available reliability so `Nisko poverenje` keeps working.
- Backend `ScopeExplanation` still embeds raw `REPLENISH`; UI composes the family label itself.
- Actions ledger can still show English snapshot prose (`expectedImpactBasis`, `decisionReason`) when the backend stored it that way.
- Unused Daily Sales no-data-banner CSS remains.

## Risks
- Unknown action codes fall back to underscored-to-spaced text.
- Other API clients that parsed English gap messages will now see Serbian text; `gapReason` codes are unchanged.

## Next
- Push to origin/main.
