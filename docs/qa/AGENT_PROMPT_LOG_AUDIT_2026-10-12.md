# Agent prompt and log audit - 2026-10-12

Base: `origin/main` `d186bde97d212d5c668c28dc9d595b67292fc8c4` (fetched 2026-10-12 12:40 UTC+2). Read-only on databases; no smoke tests.
Scope: all `docs/ai/*QUEUE*.md` and prompt files, `.ai/task-locks`, `.ai/runs/2026-10-1*`, the last 300 main Actions runs (2026-09-28..2026-10-12) and the last 40 GitHub deployments (Vercel).

## Findings

| # | Finding | Evidence | Action |
|---|---|---|---|
| F1 | RQ608 header flipped DONE -> IN_PROGRESS; its completion note says DONE on `35aabd58` with green run `38116562952`. | Header `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md:25`. Flip introduced by squash `3a78e062` (PR #123 for RQ628, branched before RQ608 closed); `f35e5656` still had DONE. | Corrected to DONE. Pattern noted: long-lived PR branches re-apply stale queue headers on squash. |
| F2 | RQ598 and RQ600 still READY in the 2026-10-09 intake file, but DONE in their canonical addenda. | `docs/ai/ANALYTICS_NEXT_EXECUTION_PROMPTS_2026-10-09.md:15,33`; canonical `..._TEST_HARDENING_ADDENDUM.md:702` (RQ598), `..._CROSS_SURFACE_ADDENDUM.md:1281` (RQ600). | Corrected to DONE pointers. |
| F3 | RQ622 is IN_PROGRESS (Codex). Implementation `116df731` was red (Actions `38236578398`) and follow-up `d186bde9` CI was still running at audit time. Run log `.ai/runs/2026-10-12-RQ622-evidence.md` shows SHA/evidence pending. | as cited | Active lane - not touched. |
| F4 | Recurring red-then-fix on main: 11 of 12 Analytics Tests failures on 10-10..10-12 were implementation commits followed by a test-fix commit. | `f55717f2`, `116df731`, `b8e1c08f`, `b691f7d1`, `d7d4caec`, `968e8f30`, `e5a3f617`, `ca5f9ffc`, `408275c7`, `99a7487b`, `7a23c6d5` (runs 38225106959, 38236578398, 38177795288, 38176669519, 38173205711, 38090750465, 38088217597, 38084796528, 38074776140, 38073698519, 38072761598). | RQ632 WAITING (owner decision). |
| F5 | `Failed to persist HTTP request performance metric` logged 36x in one backend CI run, with EF command errors. Persistence failures are swallowed. | `Api/Middleware/RequestPerformanceLoggingMiddleware.cs:60-65`; run `38236578398`. | RQ631 READY. |
| F6 | Vercel production deploys failed on two docs-only commits; every commit triggers a production build (no `ignoreCommand`), and two `vercel.json` files overlap. | Deployments for `f35e5656` (dpl_HTdAhWqwZDDBRu6QxrnVQxgocGGT) and `3a78e062` (dpl_GWMNgzfNTGe5338RVEPQJH39C7m6); `vercel.json`, `Klijent/clientapp/vercel.json`. 35/40 recent deployments succeeded, 3 previews cancelled by hand. | RQ633 WAITING (needs Vercel log access). |
| F7 | Two `DatabaseMigrationOwnershipTests` failed on every Windows/CRLF checkout (anchors contain `\n`). 12 run logs list them as "known failures", which hides real regressions. | `Api.Tests/DatabaseMigrationOwnershipTests.cs:42,70,99` | Fixed: the file reader normalizes CRLF; 6/6 pass locally on CRLF. |
| F8 | EF migration discovery gap in the analytics context (missing indexes, narrow columns). | RQ629 (DONE `8c2d27d8`); follow-up RQ630 READY. | Already registered; no duplicate. |
| F9 | Non-fatal duplicate `pg_extension_name_index` during `025_AddTrendplusPerformanceIndexes.sql` batch 4 in CI. Recorded only as a residual. | RQ608 completion note; `.ai/runs/2026-10-11-RQ608-evidence.md` | Documented here; register only if it recurs in a failing run. |
| F10 | No stale locks: `.ai/task-locks` is empty on main. | directory listing | none |
| F11 | Planning Governance failures cluster on 10-07 (agent-instruction refactor) and 10-08/09 (missing completion-note fields). The validator caught them as designed. | runs 37616517973..37617206540, 37826180525, 37925923922 | none |
| F12 | Dev machine: Docker Desktop CLI unresponsive since 2026-10-11, so local Testcontainers runs are impossible and agents rely on CI proof. | local observation during RQ613/RQ629 | Owner: restart Docker Desktop. |

## Corrections made in this audit
- RQ608 header -> DONE (F1); RQ598/RQ600 intake copies -> DONE pointers (F2); CRLF-robust `DatabaseMigrationOwnershipTests` (F7).

## New prompts
- RQ631 READY P3 (F5), RQ632 WAITING P2 owner decision (F4), RQ633 WAITING P3 Vercel access (F6).

## Owner decisions
1. RQ632: direct-main with mandatory focused pre-push tests, or PRs with required checks for code.
2. RQ633: give an agent Vercel log access, or inspect the two deployment ids yourself.
3. Restart Docker Desktop on the dev machine (F12).
