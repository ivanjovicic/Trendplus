Task ID: review-unreviewed-20260908-commit-audit
Queue: direct-user-request
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/review-unreviewed-commits-20260908 / local merge
Main commit SHA: bb56a2a6cc625709356c63a031de399217d9ef4c
Main verification: passed - local main and origin/main both resolve to bb56a2a6cc625709356c63a031de399217d9ef4c; merge commit is an ancestor of origin/main
Evidence state: synchronized

## What was done
- Reviewed the functional commits from 2026-09-08 that were not covered by the earlier 2026-09-07 review evidence: RQ184, RQ185, RQ186 and RQ187, together with their related backend, React and focused-test changes.
- Confirmed that RQ184's elapsed-window velocity, RQ185's calendar-day semantics, RQ186's backend-owned modeled lost-sales exposure and RQ187's cache/source refresh provenance are aligned with their requirements and existing analytics trust contracts.
- Found and fixed a RQ185 calendar-boundary defect in `Api/Endpoints/CachedAnalyticsEndpoints.cs`: four relevant SQL queries used `<= @toDate` while the UI/calendar denominator represents the whole ending day. They now use a half-open next-day boundary (`< @toDate::date + INTERVAL '1 day'`), so events during the final minute/second of the selected day are included consistently.
- Kept the repair within the existing analytics backend owner; response shapes, frontend API types and scope predicates were not changed.

## Files changed
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `.ai/runs/2026-09-08-review-unreviewed-commit-audit-evidence.md`

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~CachedInventoryVelocityTests|FullyQualifiedName~ProductDecisionLostSalesTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~AnalyticsCacheFreshnessTests"` -> pass, 16/16.
- `npm run test -- --run src/utils/analyticsVelocitySemantics.spec.ts src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx` (from `Klijent/clientapp`) -> pass, 2 files / 5 tests.
- `npm run check:analytics-guardrails` (from `Klijent/clientapp`) -> pass, including encoding and analytics guardrails checks.
- `npm run build` (from `Klijent/clientapp`) -> pass; TypeScript and Vite production build completed. Existing chunk-size warnings remain.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --nologo` -> pass, 0 errors; existing warnings remain.
- `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 403 tasks.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 tasks.
- `git diff --check` -> pass; only normal Git LF/CRLF working-copy warnings were emitted.

## Validation not run
- Full backend/frontend test suites -> not run; focused tests and the relevant production build were sufficient for this narrow SQL-boundary repair.
- Live database/provider/browser verification -> not run; no safe live-data or browser session was required for the contract-level repair.
- Remote CI/checks -> not run; local validation was available and remote CI state was not part of this run.

## Documentation impact
- Added this durable run log. Existing RQ184-RQ187 evidence and canonical AI guidance were read and not rewritten; no owner documentation contract required an update.

## What was missed
- RQ186 intentionally retains the existing non-nullable modeled exposure behavior where invalid/non-positive inputs resolve to zero; recommendation/actionability gates prevent unsupported decisions. A contract rewrite was not justified by this review.
- Legacy cache entries without source refresh metadata remain explicitly unknown by design rather than being assigned a fabricated refresh time.
- An earlier RQ187 cached integration run had 15/16 passing with one unrelated Neon authentication failure; it is an environment/tooling residual, not a product failure, and was not retried unchanged.

## Risks
- SQL boundary behavior was proven by static contract inspection plus focused backend tests; no live provider query was executed.
- Release build reports existing warnings, including nullable/analyzer warnings outside this repair; no unrelated warning cleanup was included.
- Delivery completed through the local merge and push; the exact target SHA is recorded above.

## Next
- None for this review scope.
