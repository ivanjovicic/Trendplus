# Daily Sales live query and runtime contract evidence — 2026-10-04

Task ID: daily-sales-live-contract-verification
Queue: direct-user-request
Date: 2026-10-04
Agent/tool: Codex / PowerShell HTTP, dotnet, Vitest, Git
Delivery target: main
Working branch / PR: `codex/queue-next-2026-10-04` / evidence and routing pushed directly, no PR
Main commit SHA: `deb6db0419a60207bcf0790bd2d906ce6eac3d33`
Main verification: passed — fresh `origin/main` contains this alias fix and the signed supplier-ID fix `1ed9c9e57603f0534d81b79b6594774190a82824`; current deployed API and frontend bundle also expose the corrected contracts.
Delivery mode: direct-main (evidence/routing update; implementation fixes were already on main).
Evidence state: synchronized

## What was done

- Closed the post-deployment follow-up for the reported Daily Sales issue. The implementation was already present on current `main`, so no duplicate code or business-semantic change was needed.
- Confirmed the original range mismatch came from the then-deployed endpoint ignoring short `from`/`to` query names and falling back to the inclusive last-30-days default. The old response `2026-09-02` through `2026-10-01` matches that default. The fix accepts `from`/`to` while preserving `fromDate`/`toDate` precedence, date normalization, 365-day limit and default behavior.
- Confirmed the supplier validation mismatch was that older frontend validation rejected legitimate negative Access AutoNumber IDs. Current Zod uses nullable signed Int32 IDs; the deployed Vercel bundle contains that schema and current API supplier IDs fit the contract.
- Verified current cache keys are built from resolved date bounds, store, data scope and `topN`; the root response object carries those resolved dates. A live alias request and its canonical `fromDate`/`toDate` equivalent returned identical serialized responses.
- The current live request returns `requestedFrom=2026-05-05`, `requestedTo=2026-12-12`, `topN=15`, 222 daily rows and 1,262 sold items. Its latest non-zero day is `2026-08-05`; later empty rows reflect the current data horizon, not the query range being truncated.

## Files changed

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-04-daily-sales-live-contract-evidence.md`

## Validation run

- Live GET `/api/analytics/daily-sales?from=2026-05-05&to=2026-12-12&topN=15` -> pass; exact requested range, 222 rows, 1,262 sold items, 15 supplier headers.
- Live GET using canonical `fromDate`/`toDate` -> pass; the entire alias response equals the canonical response.
- Live supplier-ID scan -> pass; all non-null IDs are integer values within signed Int32 bounds.
- Deployed frontend bundle inspection -> pass; `https://trendplus.vercel.app/analytics/daily-sales` serves `index-B1_wAdrw.js`, whose Daily Sales schema uses `supplierId: Z`, with `Z = finite integer constrained to Int32 and nullable`.
- Initial temporary Vitest probe that fetched the live API directly -> fail at the test harness boundary because repository MSW rejects unhandled network requests; product response was not evaluated in that attempt.
- `npm run test -- --run src/validation/__tests__/dailySalesLivePayloadProbe.spec.ts` -> pass, 1/1 using a temporary snapshot of the live API payload; the probe and snapshot were removed after execution.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~DailySalesStatsIntegrationTests" -v:q` -> pass, 16/16, zero skipped.
- `npm run test -- --run src/validation/__tests__/analyticsResponseSchemas.spec.ts` -> pass, 19/19; includes negative, zero and null supplier-ID coverage.
- `git diff --check` -> pass.

## Validation not run

- Full API/frontend suites, authenticated interactive browser session and GitHub Actions status — not needed to verify the already-landed focused contracts; no CI status is claimed.

## Documentation impact

- Added a current live verification record to the Operations queue history and linked it from the RQ568 follow-up.
- Updated `MASTER_ROADMAP.md` so Daily Sales is no longer listed as the immediate follow-up; RQ567 remains READY.
- Existing implementation evidence remains in `.ai/runs/2026-10-01-daily-sales-query-contract-evidence.md` and `.ai/runs/2026-10-01-daily-sales-negative-supplier-id-evidence.md`.

## What was missed

- No further implementation gap was reproduced against current `main`, live API or deployed frontend bundle. No authenticated user browser tab was inspected.

## Risks

- Current sales data in the requested range has a latest non-zero day of 2026-08-05; the rest of the period is zero-filled where source sales are absent. Source freshness and shift timezone remain separate operational concerns.
- A browser tab that retained a pre-fix bundle could still show the old Zod error until reloaded; the currently served Vercel bundle contains the signed-ID fix.

## Next

- RQ567 is the current READY prompt. The Daily Sales source-freshness/shift-timezone concerns remain separate and are not required to resolve the reported range/schema failure.
