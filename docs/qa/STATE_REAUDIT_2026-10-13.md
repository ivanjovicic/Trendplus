# State re-audit - 2026-10-13

Base: `origin/main` `59c2fbe0` (fetched 2026-10-13 ~12:57 UTC+2). Delta covered: everything after the 2026-10-12 audit (`319d24f6`) and the readiness-gate test fix (`304223ff`). That is 40 commits from Grok Bot, Codex, Cursor Agent and PRs #126-#135, plus the CI history over the same window.
Companion: `docs/qa/TRENDPLUS_RELIABILITY_NEXT_WORK_2026-10-13.md` (Codex, base `6dec862e`) already covers Render/STAB16, the business-truth items (RQ601/RQ602/RQ610) and the RQ634-RQ638 rationale. This report records only what that document does not, and does not repeat it.

Times are UTC+2 unless marked UTC.

## 1. Key findings

| # | Finding | Evidence | Disposition |
|---|---|---|---|
| F1 | **`main` is red.** RQ634 implementation `59c2fbe0` (12:48) failed backend CI | Run `38385926269`: `SupplierDecisionSchemaReadinessIntegrationTests.SupplierDecisionRepair_SeedsPostgres_RepeatsIdempotently_AndPreservesRefreshPrerequisites` throws `DatabaseInitializationLockTimeoutException: Could not acquire advisory startup lock 987654324 within 120 seconds` (`DatabaseInitializer.cs:493` via `:274`/`:349`). `SupplierDecisionWindowedColdStartRegressionTests.MissingOptionalSupplierCaches_DoNotGateReadiness_AndScorecardFailsClosed` also fails. Each test uses its own container, so this points at a re-entrant or leaked acquisition of the supplier schema lock inside one initializer run (unverified hypothesis). | RQ634 is under an active local lock (`RQ634-codex`, started 11:37) and has uncommitted queue edits. **Not touched.** RQ634 must not close until this run is green. |
| F2 | The RQ632 owner rule (recorded 2026-10-12 15:13) was broken **five** times after it was recorded | Red-then-fix pushes on `main`: `fd2d0fb0` -> `e96f9329` (AnalyticsIntelligenceCacheConvergenceTests); `a3e621f7` -> `560f4a91` (RQ626 oracle); `713cae82` (#130) -> `1a1322f8` (#131, catalog drift test); `33c17219` -> `56432564` (Vercel self-test, Quality Gates); `59c2fbe0` (F1, still open). | The rule exists; enforcement is the gap. See F3, RQ641 and owner decision D1. |
| F3 | Main CI cancellation leaves code commits with no completed proof | `cancel-in-progress: true` on `${{ github.ref }}` for all three workflows. `50f196d4` (#129) was cancelled by `713cae82`, which then failed. `092a9707` and six docs commits on 10-13 between 10:04 and 10:08 were also cancelled. | New **RQ641** (WAITING, owner decision). |
| F4 | `AnalyticsFrontendRouteSmokeTests` passed vacuously while gated | It asserts only `!= 404`. With the gate on (CI `Testing`), the middleware answers 503 before routing, so a missing route was undetectable. | **Fixed here** (gate off in the class). |
| F5 | `AnalyticsDbInfrastructureTests` reports 3 Passed that never execute | Each test body starts with `if (!IsIntegrationEnabled()) return;`. The unit job lacks the env var, and the integration job filters do not include the class. The tests are not in the BCI17 skip inventory. | Gate override added here (so enabling them cannot race). New **RQ639**. |
| F6 | Nothing prevents the next factory from omitting the gate setting | Two custom-fixture classes (`SupplierShoeType*Oracle/Seam`) still lack an explicit value. | New **RQ640** (convention guard). |
| F7 | Stale local lock | `.ai/task-locks/P-UI-43-codex.lock.md` (2026-10-06) remains, but P-UI-43 is DONE (`ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md:2918`). | Not removed (other agent's lock); owner may delete it. |
| F8 | The `Trendplus2-grok` worktree had been deleted and pruned since the last run | `git worktree list` (10-13). | Recreated detached at `origin/main` for this audit. |

Checked and found **not** to be a problem:
- RQ631 DONE (PR #132, `f8ae875a`, green).
- RQ633 DONE for repository code (`33c17219` + self-test repair `56432564`, Quality Gates green). The provider-side proof (that a docs-only commit really skips the Vercel build, and classification of the two historic deploy failures) still needs Vercel access. It stays a residual in the companion report and is not reopened.
- RQ626/RQ629/RQ630 closes are on `main` with green runs.
- Duplicate-looking commits `c41ec692`/`a5566d60` touch different files (Rq605 test vs OperationalFallback test) and are not duplicates.
- The PR-branch failures `38272748798`, `38273551705`, `38281080703` and `38351027909` were superseded by later green runs.

## 2. Queue tidy-up in this commit

- **RQ636**: evidence updated. `CachedAnalyticsOperationalFallbackTests` already disables the HTTP metric writer (`a5566d60`) and the gate (`304223ff`). The remaining scope (MediatR `PerformanceLoggingBehavior` warning, Access-import worker) is unchanged and still READY.
- **RQ634**: deliberately not edited (active lock and uncommitted edits by its owner). Its status should become DONE only after F1 is fixed and the exact-SHA CI is green.
- No DONE item was found missing from `main`. No status flip or duplicate needed merging in the RQ631-RQ638 range. Older WAITING/PARTIAL items (RQ128, RQ137-RQ140, RQ472, RQ592, RQ601, RQ602, RQ606, RQ610, STAB16, MT*, GAI*) still have valid external blockers. None can be unblocked without owner, provider or source evidence.

## 3. CI and log summary (2026-10-12 15:28 -> 2026-10-13 12:58)

| Main push | Workflow | Result |
|---|---|---|
| `fd2d0fb0` | Analytics Tests | failure -> fixed by `e96f9329` |
| `a3e621f7` | Analytics Tests | failure (x2) -> `560f4a91` |
| `50f196d4` | Analytics Tests | cancelled |
| `713cae82` | Analytics Tests | failure -> `1a1322f8` |
| `33c17219` | Quality Gates | failure -> `56432564` |
| `092a9707` | Tests + Quality | cancelled |
| `f8ae875a`, `1c9158d3`, `99ba3bae` | Analytics Tests | success |
| `59c2fbe0` | Analytics Tests | **failure (open, F1)** |

## 4. What is left (ordered)

1. **F1 / RQ634 owner**: fix the advisory-lock regression and get a green exact-SHA run, then close RQ634.
2. **RQ637** (P1), **RQ636** (P2), **RQ638** (P2), **RQ635** (P2): READY, path-disjoint (see the companion report).
3. **RQ640** (P2) and **RQ639** (P3): new, READY.
4. **RQ641**: WAITING on owner decision D2.
5. External: STAB16 (Render), RQ633 provider residual (Vercel), RQ601/RQ602/RQ610 (business/source evidence).

## 5. New prompts

`RQ639`, `RQ640`, `RQ641` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`.

## 6. Owner decisions needed

- **D1**: should a backend change whose only meaningful proof is PostgreSQL/Testcontainers (not runnable locally) be required to go through a PR with green CI before merging? Today the RQ632 text allows "first CI run is the proof"; F2 shows that path still produces red main.
- **D2** (RQ641): never cancel main push runs (more CI minutes), or keep cancellation and name the alternative proof.
- **D3**: delete the stale `P-UI-43-codex.lock.md`.
