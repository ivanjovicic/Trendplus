Task ID: 2026-09-20-todays-commits-audit
Queue: direct-user-request
Date: 2026-09-20
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no PR
Main commit SHA: de454c02
Main verification: passed - `origin/main` contains implementation SHA `de454c02` after fresh push verification
Evidence state: synchronized

## What was done
- Reviewed all commits dated 2026-09-20, including the newly fetched RQ333-RQ344 deliveries, and the local working tree against the repository instructions and owning contracts.
- Reviewed the local `codex/rq291-local-duplicate` branch. It is stale and superseded by the current `main`; merging it would remove newer Pre/Post analytics coverage and evidence, so it was intentionally not merged or pushed.
- Hardened the worker-runtime migration so its prerequisite index removal is also idempotent with `DROP INDEX IF EXISTS`.
- Fixed RQ337's UTC/local-calendar off-by-one in preset ranges by using UTC calendar arithmetic.
- Fixed RQ344's backend test compile defects (`using Xunit` and the correct `Trendplus2.Endpoints` namespace) and synchronized its pending main SHA evidence.

## Files changed
- Infrastructure/Migrations/20260507132430_AddWorkerRuntimeSettings.cs
- Klijent/clientapp/src/utils/analyticsPeriodPresets.ts
- Api.Tests/PreNivelacijaFilterFacetsTests.cs
- .ai/runs/2026-09-20-RQ344-evidence.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- .ai/runs/2026-09-20-todays-commits-audit-evidence.md

## Validation run
- `git diff --check` -> pass.
- `npm run test:run -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx` -> pass, 27 tests.
- Initial broad analytics focus run -> fail, 187/190 passed; RQ337 preset tests exposed the timezone off-by-one.
- `npm run test:run -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/DailySalesStatsPage.numericState.spec.ts src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/utils/__tests__/prePostNivelacijaTrust.spec.ts src/utils/__tests__/dailySalesPreviousPeriodComparison.spec.ts src/utils/__tests__/analyticsPeriodPresets.spec.ts` -> pass, 190/190.
- `dotnet test .\\Api.Tests\\Api.Tests.csproj --configuration Debug --no-restore --nologo --filter "FullyQualifiedName~PreNivelacijaFilterFacetsTests"` -> initially failed because the new test lacked its Xunit import/namespace; after the same-owner repair -> pass, 2/2.
- `npm run check:analytics-guardrails` -> pass.
- `dotnet build .\\Api\\Api.csproj --configuration Debug --no-restore --nologo` -> pass, 0 warnings and 0 errors.
- `dotnet ef migrations list` for `TrendplusDbContext` and `AnalyticsDbContext` against local PostgreSQL -> pass; expected migrations are discoverable, including `AddAnalyticsDimensionsAndMovements`.
- `dotnet ef database update` for both contexts against local PostgreSQL -> pass; no pending migrations.
- `node scripts/check-agent-instructions.mjs --self-test` and validator -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and validator -> pass.
- `node scripts/check-planning-architecture.mjs --self-test` and validator -> pass.

## Validation not run
- Full frontend/backend suites -> not run; focused proofs cover the changed migration and all newly fetched RQ333-RQ344 surfaces, while wider suites are unnecessary for this bounded patch.
- Remote CI status -> not inspected; repository policy does not require waiting for CI before main delivery.

## Documentation impact
- Added this durable direct-task evidence log and repaired the RQ344 queue/run-log delivery SHA evidence. No roadmap routing change was needed because RQ291 is already DONE and the stale local branch is not a new prompt.

## What was missed
- No known missed defect within the reviewed today's-commit/setup scope; full suites and remote CI remain outside this run.

## Risks
- The local `codex/rq291-local-duplicate` branch remains available as an intentionally unmerged stale branch; its content is already superseded on `main`.
- Optional local `pgvector` training support remains unavailable; it is unrelated to this migration hardening.

## Next
- None for this bounded audit; future work should use the current queue router rather than the stale local RQ291 branch.
