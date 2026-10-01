Task ID: daily-sales-query-contract
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/daily-sales-contract
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Reproduced the live mismatch: `from` / `to` were ignored by `DailySalesStatsRequest`, so the endpoint returned the existing inclusive last-30-days default (`2026-09-02` through `2026-10-01`).
- Added `from` / `to` aliases while preserving `fromDate` / `toDate` precedence, UTC date normalization, the 365-day validation limit, inclusive boundaries and the default window.
- Confirmed the normalized cache key includes both period boundaries, store, data scope and `topN`; the response envelope contains the same resolved requested dates. Equivalent alias and canonical requests now use the same key and response.
- Confirmed the live canonical-parameter request returns 222 daily rows and 1,262 sold items for `2026-05-05` through `2026-12-12`, with data available through `2026-08-05`. The zero-filled suffix is a data-availability condition, not a query/cache error.
- Confirmed live supplier IDs include negative integers. Current `main` already fixes the Zod signed-ID constraint in commit `1ed9c9e5` and includes a regression fixture; no duplicate frontend change was needed.
- Repaired a current-main compile error in the same service by initializing attribution through its existing merge method. This preserves the accumulator semantics and allows the API project/tests to build.
- Updated the stale Daily Sales golden snapshot to match the existing `Nepoznat dobavljač` response contract, and documented the query aliases in the runbook.

## Files changed
- `Api/Endpoints/DailySalesStatsEndpoints.cs`
- `Api/Services/DailySalesStatsService.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/Golden/daily-sales-stats.contract.json`
- `Api/docs/daily-sales-stats-runbook.md`
- `.ai/runs/2026-10-01-daily-sales-query-contract-evidence.md`

## Validation run
- Live GET with `from=2026-05-05&to=2026-12-12&topN=15` -> returned `requestedFrom=2026-09-02`, `requestedTo=2026-10-01`, 30 rows, zero items.
- Live GET with `fromDate=2026-05-05&toDate=2026-12-12&topN=15` -> returned requested dates, 222 rows, 1,262 items; latest available sales date `2026-08-05`.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter FullyQualifiedName~DailySalesStatsIntegrationTests --logger "console;verbosity=minimal" --verbosity quiet -p:WarningLevel=0` -> pass, 16/16.
- `npm run test -- --run src/validation/__tests__/analyticsResponseSchemas.spec.ts` -> pass, 18/18; includes the signed supplier-ID regression.
- `git diff --check` -> pass.

## Validation not run
- Full API solution test suite -> not run; focused Daily Sales integration coverage passed.
- Full frontend build/guardrails and browser/device checks -> not run; frontend runtime schema was unchanged and its focused spec passed.
- Current-main Actions/deployment status -> pending delivery; inspect once after push if discoverable.

## Documentation impact
- Updated `Api/docs/daily-sales-stats-runbook.md` with the short query aliases and canonical-name precedence.
- Queue files were not changed because this was a direct user request; queue value is recorded as `direct-user-request`.

## What was missed
- Post-push main verification and any available current-main Actions/deployment classification remain pending.
- The live dataset still reports `maxAvailableDate=2026-08-05`; this request does not repair source freshness.

## Risks
- After the query fix, the requested range correctly includes available sales through `2026-08-05`; dates after that remain empty until source data is refreshed.
- Live API behavior cannot reflect the alias fix until the main deployment completes.

## Next
- Push the validated commit to `main`, verify the exact SHA, and synchronize this evidence.
