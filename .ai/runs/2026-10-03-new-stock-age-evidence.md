Task ID: direct-new-stock-age
Queue: direct-user-request (follow-up to RQ539 acceptance)
Date: 2026-10-03
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: e5402f89314488f917df2bbc5e4dea503891dfd2
Main verification: fresh `origin/main` fetch plus successful `git merge-base --is-ancestor e5402f89314488f917df2bbc5e4dea503891dfd2 origin/main`
Evidence state: synchronized

## What was done
- Applied the user-confirmed 30-day default as a configurable business threshold (`Analytics:PreNivelacija:MinimumNewStockAgeDays`) with one backend fallback constant.
- Age uses the first actual `Ulaz robe` for the article/store and falls back to first positive sale; it does not use `CreatedAt` or import timestamps.
- New stock is separated into the existing `newStock` queue and tagged `stockAgeStatus: new_stock`; older evidenced stock is `established`; absent receipt and sale evidence is `unknown`.
- Unknown age no longer receives synthetic 180-day recency or no-sale filter age. `daysSinceLastSale` remains nullable, and its statuses remain `never_sold`, `no_sale_in_window`, and `sold`.
- Removed `999` from the pre-nivelacija golden oracle and made the oracle nullable.

## Files changed
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Api/Models/PreNivelacijaPriorityModels.cs`
- `Api.Tests/PreNivelacijaPriorityOracleIntegrationTests.cs`
- `Api.Tests/Fixtures/pre-nivelacija-prioriteti-golden.json`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `Klijent/clientapp/src/types/preNivelacija.ts`
- `Klijent/clientapp/src/validation/analyticsResponseSchemas.ts`
- `MASTER_ROADMAP.md`

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~PreNivelacijaPriorityOracleIntegrationTests` -> pass, 7 passed, 0 skipped.
- `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` -> pass, 55 passed.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~PreNivelacijaPriorityOracleIntegrationTests` -> pass, 7 passed, 0 skipped (rerun after receipt-date reliability filter).
- `npm run check:analytics-guardrails` -> pass (encoding check, guardrail self-test, guardrails, and typecheck).
- `npm run build` -> pass; Vite reported the existing >500 kB chunk warning, which is explicitly non-blocking and out of scope.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass (14 canonical files).
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (671 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass (79 planning tasks).
- `git diff --check` -> pass after source, roadmap, and run-log edits.

## Validation not run
- Full backend/frontend suites -> not run; focused tests cover the scoped contract.
- `gh run list --commit e5402f89314488f917df2bbc5e4dea503891dfd2 --limit 10 --json databaseId,status,conclusion,workflowName,url,event,headSha` -> no current-main Actions runs discoverable for the implementation SHA.

## Documentation impact
- Added this run log and a direct follow-up note in `MASTER_ROADMAP.md`; RQ539 remains historically DONE and no queue prompt was claimed or reopened.

## What was missed
- None known.

## Risks
- The passing frontend build retains the existing Vite >500 kB chunk warning; the user explicitly scoped bundle work out of this task.

## Next
- None; implementation and synchronized evidence are on `main`.
