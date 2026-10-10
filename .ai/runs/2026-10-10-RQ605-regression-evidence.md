Task ID: RQ605-final-regression
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md | direct-user-request regression follow-up
Date: 2026-10-10
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no PR
Main commit SHA: 408275c73ce8b5838f38355442d23b0e0d6f67a1
Main verification: passed - fresh fetch shows HEAD == origin/main == 408275c73ce8b5838f38355442d23b0e0d6f67a1 and the implementation commit is present
Evidence state: synchronized
Ownership transfer: none

## What was done

- Refreshed `origin/main` before implementation and confirmed `HEAD == origin/main == cb76d60b64c73d98ef001509bb896ecaddc67244` at the start of this run.
- Reviewed the canonical RQ queue, RQ605/RQ606 status, active task locks, active-agent/task state, and the named endpoint/service/policy/test owners. RQ605 was already DONE and had no active owner collision; no duplicate successor was registered.
- Found and corrected three confirmed RQ605 regression gaps in the same owner scope:
  - legacy cached summary/daily and comparison used inclusive upper bounds while canonical Daily Sales is `[fromUtc, toUtc)`;
  - cached summary/daily and comparison could reuse pre-fix semantic entries because their cache namespaces were not fully versioned for the population/boundary change;
  - inner `Artikli` joins could silently omit a legitimate historical sale line when the article master row is missing/archived.
- Reused `OperationsDateRange.NormalizeUtc`, `SalesDataScopePolicy` and `SalesReceiptPopulationPolicy`; preserved signed returns, DUG/KOREKCIJA exclusion and all formula definitions.
- Made the RQ605 PostgreSQL certification fail closed when its PostgreSQL fixture is unavailable; it no longer silently returns a green test with zero executed tests.
- Extended the disposable PostgreSQL fixture with exact midnight, `23:59:59`, next-day, missing-article, second-store and supplier-filter populations. The API output is compared with an independent operational SQL oracle.
- No Access import, cache rebuild, production mutation, formula change or business-data deletion was performed.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Services/DailySalesStatsService.cs`
- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `Api.Tests/Rq605LegacySalesPostgresIntegrationTests.cs`
- `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`
- `.ai/runs/2026-10-10-RQ605-regression-evidence.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

Pre-existing untracked paths `.codex-remote-attachments/`, `Klijent/clientapp/%TEMP%/` and `Klijent/clientapp/tmp/` were preserved and are not part of this delivery.

## Validation run

- `git fetch origin main` -> pass before implementation; `HEAD == origin/main == cb76d60b64c73d98ef001509bb896ecaddc67244` at the start.
- RQ605 PostgreSQL certification:
  `dotnet test .\Api.Tests\Api.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Rq605LegacySalesPostgresIntegrationTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=rq605-regression-final.trx"`
  -> pass, `total=1 executed=1 passed=1 failed=0 skipped/notExecuted=0`. The test used an isolated PostgreSQL database and exercised the real HTTP endpoints, independent SQL oracle, scope separation, signed return, DUG exclusion, missing article, exact date boundaries, UTC/date-only normalization, store/supplier filters and cache hit/miss.
- Focused regression suite:
  `dotnet test .\Api.Tests\Api.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests|FullyQualifiedName~DailySalesStatsIntegrationTests|FullyQualifiedName~AnalyticsScreenCacheKeyContractTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=rq605-regression-focused-final.trx"`
  -> pass, `total=67 executed=67 passed=67 failed=0 skipped/notExecuted=0`.
- Backend Release build:
  `dotnet build .\Api\Api.csproj -c Release --no-restore -v:minimal`
  -> pass, `0 warnings / 0 errors`.
- PostgreSQL read-only performance comparison on the local `trendplus` database for `2026-01-01 <= datum_prodaje < 2026-08-06`, using `EXPLAIN (ANALYZE, BUFFERS)`: warm repeat old inner-`Artikli` path `9.290 ms`, new left-join/no-master-loss path `7.807 ms`; no measured regression in the representative population query. No data was changed.
- `git diff --check` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (`727` tasks).
- `node scripts/check-agent-instructions.mjs` -> pass (`18` canonical files).

## Validation not run

- Full solution Release build did not complete because the local machine had approximately `0.05 GB` free and the Vite frontend build failed with environment `ENOSPC` while copying `Trendplus.POS.Ui/public/vite.svg` into `Trendplus.POS.Ui/dist`; backend compilation completed successfully before that tooling failure.
- Remote CI was not awaited; no named RQ605 acceptance requires waiting for Actions before direct-main delivery.
- No production or local business-data repair was run.

## Documentation impact

- RQ605's existing DONE entry is being supplemented, not reopened or duplicated, with the final regression findings, exact test counters, cache namespace change and remaining tooling residual.
- `MASTER_ROADMAP.md` is updated with the final regression follow-up and delivery verification.

## What was missed

- No known miss within the RQ605 legacy summary/daily/comparison scope.
- Other legacy cached category/gender/top-product paths retain separate historical date/master-population contracts and were not silently broadened into this RQ605 repair; they remain outside this task's named scope.
- Exact batch-23 MDB certification remains RQ606's historical residual and was not claimed here.

## Risks

- The full solution build still needs a rerun after freeing local disk space; this is an environment/tooling residual, not a backend compile or focused-test failure.
- Old cache entries remain stored but are unreachable through the new RQ605 semantic namespaces; no destructive cache deletion was performed.
- The final documentation-sync commit may advance `HEAD` beyond the implementation SHA; the implementation SHA above is the code delivery proof and remains an ancestor of `origin/main`.

## Post-close routing recovery

- Recovery base before delivery: `cb76d60b64c73d98ef001509bb896ecaddc67244`.
- Active owner queue/addendum checked: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `MASTER_ROADMAP.md`, `.ai/task-locks/`; RQ605 is DONE, RQ606 is PARTIAL, and no RQ607 or other duplicate RQ605 successor exists.
- Completed/changed task searched: RQ605 final regression.
- Newly satisfied dependencies: none.
- Promoted successor: none; all confirmed defects were same-owner RQ605 close-out fixes. RQ606 remains the separate transfer-identity/historical-MDB residual.

## Next

- RQ605 remains DONE with no duplicate successor; implementation delivery and fresh `origin/main` verification are complete.
- Rerun the full solution Release build only after the local disk-space/tooling condition is repaired.
