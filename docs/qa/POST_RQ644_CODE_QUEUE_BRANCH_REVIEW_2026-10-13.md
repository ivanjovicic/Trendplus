# Trendplus post-RQ644 code, CI, queue and branch review — 2026-10-13

Baseline GitHub main: `c4c20197cf029f36de8623e2de8cb7e9df708a57` (2026-10-13 15:56 CEST owner request).
Scope: current main commits/PRs/branches, RQ post-import queue, BCI17 skip inventory and exact-main backend CI log; no live Render, Vercel, Neon or local worktrees inspected.

## Verified state (do not reuse older zero-READY conclusions)

- Open PRs **0** at baseline. RQ634–RQ644 are DONE in the canonical RQ queue; latest RQ644 delivery `4bd4b9b2` plus queue evidence tip `c4c20197`.
- Actions backend `38407849758`, job `115332057225`, is **SUCCESS**: 2,052 passed; 43 classified skips; 0 failed. Planning Governance on `c4c20197` run `38407894290` is **SUCCESS**. Backend suite green means *tested repository behavior*, not deployment parity.
- BCI17 JSON lists 43 expected skipped tests: 13 separately isolated-PostgreSQL runnable, 18 legacy-superseded, **12 genuinely-uncovered**. None of the 12 is now a generic task: 7 Supplier business/golden/trust (RQ645), 3 ShoeType detail/golden/deterministic (RQ646), 2 environment-dependent benchmark contracts (RQ647). Exact FQNs and acceptance are in canonical queue.
- Source/prior evidence: RQ634 supplier web cold-start and 018/029 safeguards; RQ635 SQL029 preflight+corrective PR #137; RQ636 one operational-fallback in-memory fixture; RQ637 web import readiness gate; RQ638 MediatR varchar and Unicode; RQ639/640 integration skip/host gate; RQ641 prevent main CI cancellation; RQ642 isolated PG infrastructure; RQ643 schema-incompatible status failed; RQ644 invalid-input in-memory parity. Do not open or remerge these implementations without a reproducible regression.

## CI logs: source-confirmed new residual

Latest complete backend log `115332057225` still includes **113 lines matching** "Relational-specific methods can only be used when the context is using a relational database provider", in stacks calling `Api.Services.Access.AccessImportJobQueue.ClaimNextAsync` and worker retry loops within *test-host* EF InMemory contexts. It also emits recurring worker started messages (2-second polling). These are **line matches, NOT 113 failed tests**, and do not prove a Render failure. RQ636's narrow CachedAnalyticsOperationalFallbackTests fixture was corrected, but other web test factories remain suspect. RQ649 requires naming the owning hosts and narrowing opt-out **only** to non-import test hosts. Do not turn off production worker or suppress all logs.

The same CI log has zero matches for the previously fixed MediatR "Failed to save performance log for" and HTTP "Failed to persist HTTP request performance metric" messages. Treat RQ631/RQ636/RQ638 as DONE.

The log also contains 14 `SqlFailed` messages from SQL029 transactional execution around the SQL029 lifecycle tests and three retry warnings in migration bootstrap (including duplicate pg_extension and deadlock). These **did not fail the suite**. Determine intentional negative-fixture SQLSTATE/expected assertions before treating any as product defect; do not create duplicate SQL029 repair prompts without evidence.

## Remote branch review — no bulk merge authorized

GitHub branch listing at intake: **98 refs including main**, 97 non-main, zero open PRs. This is an upper-level branch inventory, not proof that 97 changes are pending. Commit-graph `compare(main, branch)` on sampled historical branches returns `diverged` and ahead commits for many old Cursor/Codex branches because of squash/direct-main delivery, even when current main has identical file content. Therefore do not describe *all branches merged as Git ancestry* and do not cherry-pick automatically.

| Sample remote branch / PR | Verified status | Safe action |
|---|---|---|
| `cursor/rq644-invalid-input-skip-reconcile-28d8` | compare identical to main at baseline | None |
| `cursor/rq635-sql029-preflight-28d8`, RQ639/640/641/642/643 | compare behind main, ahead=0 in sampled review | None |
| `cursor/rq637-access-import-readiness-gate-28d8`, PR #136 closed unmerged | compare diverged 1/42, **all three changed code/test blob SHAs exactly identical** between branch and current main | Never forward-merge |
| `fix/rq638-unicode-truncation-boundary-20261013`, PR #139 closed unmerged; later #141 merged | compare diverged; **both inspected file blobs identical** on main | Never forward-merge |
| `fix/rq635-real-postgres-ci-oracle-20261013`, PR #138 closed unmerged; corrected PR #137 merged | compare diverged; main lifecycle file has same seven named facts and a further private initializer helper; content differs | Check exact assertion/data parity if later needed; no blind cherry-pick |
| `codex/product-value-plan-refinement` | compare diverged, 2 ahead / ~390 behind; historical product-planning docs | Review owner intent and present-day decisions before salvaging any unique material |
| `fix/2026-10-13-supplier-diagnostics-reliability` PR #135 merged squash | commits diverge but diagnostics were delivered on main | No merge |

A first ten-branch sampled compare (not the full branch census) returned ten `diverged` branches, illustrating why ahead counts are insufficient. RQ648 is the **exhaustive** task: enumerate all 97 refs including pagination, compare actual current file semantics/PR history and report any truly unique useful change. No branch deletion, force push, worktree cleanup, or old-branch merge is authorized.

## New READY selection (path-disjoint)

- **RQ645 P1 primary:** seven exact Supplier trust/cohort/weighted/golden/deterministic/missing-schema contracts, disposable PostgreSQL. Own Supplier-specific test paths.
- **RQ646 P1 independent:** three ShoeType detail trust/unknown/golden/deterministic contracts. Own ShoeType-only test paths.
- **RQ647 P2 independent:** two non-gating performance benchmark cases with fixed fixture and comparable medians/p95; avoid arbitrary shared-runner SLA gate. Own new benchmark paths.
- **RQ648 P2 independent:** exhaustive remote branch patch/content equivalence and safe salvage report. Read-only GitHub; unique report path only until queue closeout.
- **RQ649 P2 independent:** isolate remaining unrelated EF InMemory test-host Access import background work without changing production registrations. Own non-import host fixtures, preserve import worker test path.

New prompts must run target tests **for real** in CI and never reclassify BCI17 skip entries on source-only or local no-Docker evidence. `expectedKnownSkippedCount=43` changes only when the TRX proves the count differs. A replaced skip can stay skipped but become legacy-superseded with a named running and asserted proof.

## Existing blockers remain unchanged

- **STAB16 BLOCKED**: Render exact deployed SHA/config, health/readiness SQL018/029 outcomes, dedicated refresh worker, supplier 90d/180d populated catalog and safe authorized production maintenance. No equivalent from GitHub CI.
- **RQ610 BLOCKED**: trustworthy SKU x store stock mutation generation; do not backfill observed history from current stock.
- **RQ601/RQ602 PARTIAL**: 20 original purchase calculations; documented cost-unknown sales; source daily coverage/business-signoff for markdown impact. Existing batch #23 is reconciled; no automatic reimport.
- **RQ633 DONE code, provider residual**: Vercel project root, true ignoreCommand deployment and historical failed-deploy classification require provider access.

## Confidence and remaining limits

Review reads the exact latest GitHub main, queue source, BCI17 classifications, recent Actions jobs/logs, and sampled branch compares and blob hashes. It **does not** inspect all 97 branch patches in this turn, local uncommitted worktrees, provider logs, or production DB. RQ648 tracks exhaustive branch accounting; absence of an open PR does not imply all legacy branch code was delivered. Re-check current main, locks and PRs before claim or merge.

## Delivery and complete commit-graph census (post-PR #143)

- PR #143 was squash-merged to main on `8de58c80440ca6eb87d343024dea260215927a77`. Its exact-head `48ac7e29829e682958532702d3915504e8e307a3` passed Planning Governance `38412272800` (771 tasks), Analytics Quality Gates `38412272013`, and backend `38412271797` (2,052 passed, 43 classified skips, zero failed; RQ453 certification succeeded). This validates the new queue and docs, **not** production Render/Neon deployment.
- The read-only GitHub commit-graph comparison was subsequently completed for **all 97 pre-audit non-main branch refs** against main baseline `c4c20197`: **71 behind (ahead=0), 1 identical, 25 diverged (ahead>0)**. Thus every baseline branch has now been graph-classified, but **only selected divergent branches have content/patch equivalence proof**. RQ648 should focus on the 25 divergent branches' substantive patch equivalence; refresh the list first because branches can change.
- Some diverged branches are known already delivered despite ahead commits: RQ637's three changed code/test blobs and RQ638's inspected Unicode code/test blobs are identical on main. Other old branches (including `fix/rq481-invalid-filter-id-failclosed-20261008`) have source files differing from main because main has subsequent work; do not use an old branch as the preferred version.
- No stale branch was merged, deleted, rebased or force-pushed. No automatic code salvage conclusion is made for the remaining divergent set. RQ645/RQ646/RQ647/RQ648/RQ649 continue as READY; DONE tasks are not reopened.
