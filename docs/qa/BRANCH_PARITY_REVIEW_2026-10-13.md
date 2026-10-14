# Trendplus branch/queue/CI semantic review — 2026-10-13 17:04 CEST

> **ARCHIVED BASELINE / NOT A LIVE ROUTER — 2026-10-14 review.** This document was authored for main `a5604874` on 2026-10-13 and recovered from now-closed PR #147. The 97-ref graph, 23-divergent list, RQ645–RQ650 READY statements and CI/owner statuses below are *historical evidence only*. Later direct-main evidence (`.ai/runs/2026-10-13-direct-branch-reconciliation-evidence.md`) checked 112 non-main refs, with 29 unreachable branches; on 2026-10-14 the current GitHub branch listing had 109 non-main refs. The SQL029 prerequisite assertion is already salvaged and RQ658 handled real PostgreSQL convention defects. Do not re-open completed RQ650-like work or merge a stale branch. See current `MASTER_ROADMAP.md` + canonical RQ648 row.


Review base: `main a560487446af5e2ca3b778d79ae3068a271c6917`.
Scope: GitHub REST branch graph, branch-source file blobs, recent merges/PRs, canonical RQ/BCI queues, exact-main backend CI. GitHub-only: local uncommitted worktrees, agent locks outside GitHub and Render/Neon provider state are not visible. **No remote branches were merged, deleted, rebased or force-pushed in this review.**

## Latest source and execution truth

- PR #144 `test(ci): PostgreSQL tests report Skipped, not Passed, without required PostgreSQL` is merged at `a5604874`; latest exact-main Actions backend `38417745428` and Planning Governance `38417745460` are **SUCCESS**. Backend job `115366590795`: **2053 passed, 43 skipped, 0 failed**. A green suite remains neither a production Render/Neon certification nor proof that skipped tests ran.
- In the same backend log, **111** lines contained `Relational-specific methods can only be used when the context is using a relational database provider` from the Access-import test-host call chain. These are **log-line matches, not 111 failed tests**. RQ649 remains the precise non-import EF InMemory fixture-owner READY task; RQ636/637 remain DONE.
- At initial intake GitHub had no open PR. During this review **PR #145 opened** at `babd557d45bcedf4ec2db0f605fa4d9997aac93f` for **RQ645** (seven Supplier tests), touching `SupplierSalesRemainingContractsPostgresTests.cs`, Supplier parity tests, skip inventory and the shared RQ queue/roadmap. PR description reported 6/7 focused local checks before a corrected comparison test; exact-head PostgreSQL/backend CI is the acceptance gate. Do not double-claim RQ645 or overwrite its queue/roadmap ownership. RQ646–RQ650 are path-disjoint candidates subject to fresh locks.
- Historical RQ634–RQ644 are delivered and stay DONE. RQ601/RQ602 still PARTIAL on original purchase-cost evidence/business pilot; RQ610 BLOCKED on observed inventory mutation provenance; STAB16 BLOCKED on authorized provider deployment/refresh/cache evidence.

## Complete branch-graph census on current main

Enumerated both branch pages via GitHub and compared **all 97 non-main refs** against the exact review SHA `a5604874` (comparison errors: zero). Results: **74 ancestor/behind (ahead_by=0); 23 divergent/ahead (ahead_by>0); 0 identical**. Earlier snapshot listed 25 divergent at main `c4c20197`; that count is not current. `ahead_by>0` is commit ancestry and **does not establish missing functionality**, especially after squash and direct-main delivery.

The following 23 ref rows cover every current divergent branch; all remaining 74 branches have ahead_by=0 and therefore require no code merge. The disposition column distinguishes **exact blob parity** from **lineage/supersession** and unproven semantic parity.

| Divergent branch | Evidence checked on latest main | Review disposition |
|---|---|---|
| `audit/intelligence-reliability-followups-20261012` | RQ621–627 planning/evidence; current queue and roadmap contain later closures | Old planning branch, no code merge |
| `audit/rq616-null-stock-20261012` | `AllEndpoints.cs` blob **exact**, while inventory SQL 022 and tests have substantially newer/different main blobs | Superseded/evolved source; **not full SQL semantics certified**; targeted RQ648 diff only if unique assertion found |
| `audit/rq645-648-coverage-branch-reconciliation-20261013` | PR #143 squash-merged; later queue added RQ649 and branch census | Delivered planning lineage, do not remerge |
| `audit/2026-10-13-reliability-prompts` | PR #134 squash-merged; RQ634–636 later executed and closed | Delivered planning, obsolete branch history |
| `codex/product-value-plan-refinement` | `docs/product/PRODUCT_VISION.md` and `BUSINESS_ROADMAP.md` **exact** blobs; product value audit/queue on main are larger/newer | Product vision parity proven; historical audit detail **not fully semantically diffed**, never override newer owner decisions |
| `codex/rq626-ci-fixture-correction` | RQ626 oracle correction landed `560f4a91`; current test blob newer/different | Superseded test, no blind cherry-pick |
| `codex/rq628-purchase-provenance-bootstrap` | PR #123 + startup followups delivered; current `DatabaseInitializer.cs` has subsequent changes | Superseded by evolved initializer, verify assertions only if a gap is shown |
| `codex/rq628-readiness-rq453-followup` | `PurchaseCostProvenanceSchemaContract.cs` **exact** blob; readiness host newer; PR #125 shipped | Contract exact; newer startup behavior takes precedence |
| `codex/rq630-allowlist-clean-migration` | PR #131 merged; main drift test has newer blob | Already delivered successor |
| `codex/rq630-analytics-schema-drift` | PR #129 merged; main drift test has newer blob | Already delivered successor |
| `codex/rq630-pre-startup-drift-check` | PR #130 merged; main drift test has newer blob | Already delivered successor |
| `cursor/p-ui-52-close-61ea` | P-UI-52 marked DONE on current roadmap/queue; old evidence/status-only commits | Obsolete queue closure, no remerge |
| `cursor/p-ui-52-recovery-61ea` | Older post-close P-UI-52 queue recovery, current queue evolved | Obsolete evidence/status |
| `cursor/queue-idle-recovery-1156-61ea` | Older zero-READY evidence branch after RQ597; RQ645–649 now READY | Historical record, do not revive zero-READY |
| `cursor/rq637-access-import-readiness-gate-28d8` | `AccessImportBackgroundWorker.cs`, `AccessImportWorkerStartupGate.cs` and import lifecycle tests **all exact matching main blobs**; PR #136 was closed without merge but implementation shipped on main | Fully content-equivalent for its 3 code/test files, **do not merge** |
| `docs/owner-decisions-rq481-rq319-20261008` | Owner decision document `OWNER_DECISIONS_2026-10-08_PULSE_OPERATIONS_FILTERS.md` **exact matching blob** | Current decision preserved |
| `docs/rq628-main-sha` | RQ628 status/evidence subsequently closed on main | Historical SHA metadata only |
| `docs/2026-10-13-pg-ci-merge-guardrail` | `AGENTS.md` **exact** blob; evidence standard and queue protocol expanded on main | Superseded policy branch; prefer stricter latest standard |
| `docs/2026-10-13-rq635-ci-truth-rq642` | PR #142 merged, RQ635 true PG proof and RQ642 correction completed | Delivered historical close-out |
| `fix/rq481-invalid-filter-id-failclosed-20261008` | Main DecisionPulse page contains signed `parseEntityIdParam`, reject-multiple/bad and unknown-ID fail-closed logic; main tests retain invalid filter and negative Access ID cases. Main blobs differ because page/tests evolved | Specific security/identity assertions present; no stale branch merge; **not all UI diff lines individually certified** |
| `fix/rq635-real-postgres-ci-oracle-20261013` | Same six SQL029 lifecycle test method families exist on main, branch includes a slightly different/longer preflight helper with prerequisite catalog assertion; main uses nonnullable throw-on-no-PG and separate DB creation | Successor scenario parity strong, but **extra branch prerequisite assertion not proven equivalent**. RQ648 should decide whether to add a narrow additive assertion/test on main; never replace whole fixture |
| `fix/rq635-sql029-postgres-fixture` | `SupplierWindowedSql029LifecycleTests.cs` is **exact matching main blob**; `PostgresRequiredFactAttribute.cs` changed again in merged PR #144 | Main has source equivalent lifecycle test plus stricter current test gate |
| `fix/rq638-unicode-boundary-rebased-on-rq635-20261013` | `PerformanceLogColumnBounds.cs` and `PerformanceLoggingBehaviorColumnBoundsTests.cs` **exact matching main blobs**; PR #141 shipped successor after #139 abandoned | Fully content-equivalent for 2 examined code/test paths |

**Branch review decision:** all 97 refs have **commit-graph disposition**; a significant subset of the 23 divergent refs now has exact blob or clear successor evidence. However some newer/different files (notably old SQL 022/its fixture, product audit, SQL029 prerequisite assertion and RQ481 UI additions) were **not proved line-by-line semantically equivalent**. Thus **RQ648 must remain READY, not DONE**. No claim that all old branch content was merged, no cherry-pick/cleanup permission, and no open PR is not the same as no agent work. Final completion requires patch-level review on these explicitly enumerated residuals and a fresh base/owner check.

## Skip-test convention follow-up found in PR #144 (new RQ650)

The merged `Api.Tests/PostgresRequiredAttributeConventionTests.cs` uses a textual filter limited to test files containing the literal `IClassFixture<PostgresContainerFixture>` and `[Fact]`/`[Theory]` tokens. **This checker does not itself detect** tests obtaining PostgreSQL via `[Collection]`, a wrapped fixture, a helper, alternate attribute qualification, or methods outside those exact class declarations. That is a **source-confirmed enforcement blind spot**, not an assertion that such a hidden false-pass test was found on current main. New RQ650 adds negative self-tests to prove the checker catches those patterns and exercises CI true/false attribute semantics without turning missing Docker into a success.

## Execution / ownership

1. PR #145 is active for RQ645; require exact-head full PostgreSQL CI and queue-closeout before another agent claims RQ645. Avoid same-file collisions with its `MASTER_ROADMAP.md` and RQ post-import queue edits; merge these docs only after rechecking live PR status.
2. RQ646 (ShoeType P1) independent next-high-priority code, then RQ649 (fake-provider worker noise), RQ647 (non-gating performance proof), RQ648 (remaining branch semantics) and RQ650 (CI proof convention coverage) as distinct lanes. These are priorities, not a permanent serialization of disjoint work.
3. STAB16/provider, RQ601/RQ602, RQ610 remain as previously blocked or partial; do not replace source/owner evidence with CI or replay the Access import. No branch deletion, force push or production SQL.

## Verification limits

GitHub compare and content SHA checks above; main exact-backend CI and PR metadata. No local .NET/Node executions or manual private Git worktree audit here. Provider deployment/Neon objects, worker service and in-flight local uncommitted changes remain UNKNOWN.


## Post-snapshot repository update (same date)

- Since the graph baseline, direct-main commits `47feddc1` and `a51dbc37` registered **RQ651–RQ654** for actionable analytics frontend failures (data-quality fake zero, stale action-count/outcome summary, pilot counts under partial failure and Belgrade calendar-day presets). Preserve these new READY tasks and their owner boundaries in the current queue. Do not create duplicate tasks for them.
- **PR #145 has now merged** on main at `6ca11bf95b6c7eb0f948bce475a688d633c66dae`; its exact head `2f98416bdf20d1f9b6fd5508d37c07bfc7e69d42` passed backend and quality CI. RQ645 is closed by its owner after evidence sync, so next primary is RQ646 if still unclaimed. No other legacy branch was merged by this audit.
- Graph 74/23 count remains the *dated `a5604874` baseline*; do not imply it is an exhaustive newly computed census after the additional commits. Run RQ648's close-out diff review on fresh main.
