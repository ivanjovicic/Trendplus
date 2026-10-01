Task ID: daily-sales-query-contract
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/daily-sales-contract -> direct main push (no PR)
Main commit SHA: deb6db0419a60207bcf0790bd2d906ce6eac3d33
Main verification: passed - fresh fetch showed origin/main at the implementation SHA; merge-base --is-ancestor returned 0
Evidence state: synchronized

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
- Pushed implementation SHA `deb6db0419a60207bcf0790bd2d906ce6eac3d33`; a fresh fetch confirmed `origin/main` at that SHA and `git merge-base --is-ancestor` passed.
- GitHub Actions run `36879880379` for the implementation SHA was `in_progress` when inspected; no CI result is claimed.
- Post-push live GET still returned the old `2026-09-02`–`2026-10-01` default response, so the Render API had not rolled out this main change at check time.
- Prior red runs were classified: `36878713532` failed the backend build on the `CS0272` initializer fixed here; `36878713638` failed an unrelated Supplier Decision Hub filter test; `36878713530` and `36879126225` failed prompt-queue validation on earlier governance commits.

## Validation not run
- Full API solution test suite -> not run; focused Daily Sales integration coverage passed.
- Full frontend build/guardrails and browser/device checks -> not run; frontend runtime schema was unchanged and its focused spec passed.
- Live behavior after the Render rollout -> not run; the post-push read still served the previous API behavior and the deployment has not been confirmed.

## Documentation impact
- Updated `Api/docs/daily-sales-stats-runbook.md` with the short query aliases and canonical-name precedence.
- Queue files were not changed because this was a direct user request; queue value is recorded as `direct-user-request`.

## What was missed
- The Render API had not yet deployed the main change at the post-push check; a later live confirmation remains outstanding.
- The live dataset still reports `maxAvailableDate=2026-08-05`; this request does not repair source freshness.

## Risks
- After the query fix, the requested range correctly includes available sales through `2026-08-05`; dates after that remain empty until source data is refreshed.
- Live API behavior still reflects the old alias handling until the main deployment completes.

## Next
- Verify the live API after the Render deployment rolls out; source-data freshness remains a separate follow-up.
