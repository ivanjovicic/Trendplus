Task ID: direct-action-outcome-ux
Queue: direct-user-request
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: 0a29fb09e6622bd1915af324d199db3cb24273f3
Main verification: origin/main contains 0a29fb09e6622bd1915af324d199db3cb24273f3
Evidence state: synchronized

## What was done
- Traced missing action outcomes to the intentional `null` initial value, which the backend already treats as pending for aggregation but the table labelled as an unexplained missing outcome.
- Added an explicit backend `no_measured_outcomes` empty reason when rows exist but no outcome has been measured, without generating rates or fake zero impact.
- Reworked the action outcome empty state to distinguish no matching rows from rows awaiting measurement, explain the 90-day summary scope versus list filters, and provide safe next actions.
- Replaced the repeated row label with `Čeka unos ishoda`, added a concise next-step explanation, suppressed empty note placeholders, and added a data-quality explanation tooltip.

## Files changed
- Infrastructure/Services/Analytics/AnalyticsActionItemService.cs
- Api.Tests/AnalyticsActionItemServiceTests.cs
- Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx
- Klijent/clientapp/src/pages/AnalyticsActionsPage.css
- Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx
- Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx
- .ai/runs/2026-09-02-direct-action-outcome-ux-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsActionItemServiceTests --no-restore` -> pass (44/44)
- `npm run test:run -- src/pages/__tests__/AnalyticsActionsPage.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx --reporter=dot` -> pass (24/24)
- `npm run typecheck` -> pass
- `npm run check:analytics-guardrails` -> pass
- `npm run build` -> pass (existing chunk-size warning only)
- `dotnet build Api/Api.csproj --no-restore` -> pass (0 warnings, 0 errors)
- `git diff --check` -> pass

## Validation not run
- Browser visual smoke -> not run - no authenticated browser session was available; component tests and production build passed.
- Production API smoke after deploy -> not run - implementation is not yet deployed from this turn.

## Documentation impact
- Added this run log; no product documentation contract needed to change because the existing backend pending/measurement semantics were clarified and preserved.

## What was missed
- None known in the scoped action-outcome surface. Production confirmation remains pending delivery.

## Risks
- The action list and outcome summary intentionally have different filter scopes; the UI now states that explicitly, but a future shared period/filter control could reduce confusion further.
- Render production still has the previously recorded deploy blocker; this change cannot be called live until the final commit is activated and smoke-tested.

## Next
- Implementation committed and pushed to `main` as `0a29fb09`.
- Redeploy and production smoke-test remain the next operational step; the existing Render deploy blocker is recorded in the earlier migration run log.
