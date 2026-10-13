# Trendplus — current-code reliability and next executable work (2026-10-13)

Audit base: `origin/main` / GitHub `6dec862e6449a18a24a26234215bcc9b30705b98`.
Scope: repository code, active canonical queues/roadmap, 2026-10-12 CI workflows and their job logs, supplied Render 16:50 UTC fragment, and public Render maintenance notice. No authorized Render/Neon/Vercel dashboard session, production database connection, source Access file, new import or browser deployment certification.

## 1. What is actually closed

| Item | Proof | Status |
|---|---|---|
| RQ625 historical TrendHistory DATE bound | PR #126 + targeted PostgreSQL proofs | DONE, do not reopen |
| RQ626 signed reconstructed stock / exact-zero stockout | PR #127 and corrected CI oracle | DONE; not a replacement for observed inventory |
| RQ629/RQ630 schema migration discoverability and EF catalog drift test | PR #129–#131 and exact-main run `38275050911` (1,983 passed, 42 classified skips) | DONE |
| RQ631 HTTP metric field bounds and persistence-failure counter | PR #132, `38296111833` backend and `38296111872` quality; merged `f8ae875a` | DONE; real production metric persistence remains unknown |
| RQ633 Vercel docs-only ignoreCommand source code | `scripts/vercel-should-build.sh`, self-test and green subsequent quality workflow | DONE for repository code; effective provider project root and historic failure classification not proven from Vercel logs |
| Render supplier MV web startup mitigation | PR #133 merged `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`; exact-merge backend `38301829902` passed 1,998 with 42 classified skips | Code/CI green; **production state unknown** |

The 42 skipped backend tests are tracked in `scripts/ci/backend-skip-inventory.json` under BCI17. They are **classified skips**, not 42 successes and not blanket certification of all scenarios.

## 2. Fresh findings from logs and source

**Render cold start (priority P0 operational / STAB16).** Supplied logs show SQL 029 forced after two supplier windowed MVs were reported MISSING_OBJECT, while multiple `/ready` and `/api/*` calls returned 503. The provided excerpt stops before the SQL 029 result, so causality and recovery are **unproven**. The merged web policy prevents automatic *supplier windowed* rebuild when effective Production config uses `DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup=false`; it does not prove whether other startup operations still contend for the Neon connection, whether the deploy is current, or whether required schema is healthy. No one should disable required readiness/fail-fast or heavy worker separation to make the monitor green.

**GitHub CI signal/noise (priority P2).** The latest passed backend run `38301829902` (merge SHA `1c9158d3`) reports 1,998 passed, 42 classified skips. It still emits exactly three `Failed to save performance log for GetInventoryStatusQuery` warnings from `CachedAnalyticsOperationalFallbackTests.MissingAnalyticsRelationContext` and repeated `Relational-specific methods can only be used when the context is using a relational database provider` errors from `AccessImportJobQueue.ClaimNextAsync` under EF InMemory test-host conditions. These are **test-host** observations, not evidence of a production DB fault. The earlier HTTP metric persistence warning class from RQ631 was absent (`Failed to persist HTTP request performance metric` count zero). RQ636 owns only the remaining fake-provider noise.

**CI historical failures are superseded.** PR #133 initially failed run `38281080703`: schema source-contract tests assumed the pre-wrapper direct call; updated source assertions subsequently passed in PR `38301683368` and exact merge `38301829902`. Vercel helper quality failure `38295445655` was a self-test referencing an unavailable abbreviated commit parent (`fatal: bad revision 'f35e5656^'`); later PR quality workflow `38296111872` and PR #133 quality workflow `38301683482` passed. Do not file fresh runtime bugs for those superseded failures.

**Provider freshness (STAB16).** Old read-only production audits show no usable durable refresh evidence through web process, but do not prove absence of a dedicated Render worker. Latest public /health and /ready were not retrievable from the assistant runtime; this is neither an HTTP 503 nor an HTTP 200 measurement. Actual worker deployment/config/runtime logs still require authorized provider access.

## 3. Business/data truth that must not be reset

- Access batch #23 is already source-fact reconciled: 5,530 source receipts / 5,530 imported SalesFacts and 67,092 source lines / 67,092 imported lines, with zero price/qty/amount mismatches on matched rows. Repeating the import is not a code fix for startup 503, supplier MVs, missing observed-stock generations, or purchase-cost authority.
- RQ601 stays PARTIAL: source-line known cost coverage on historical Access retail lines is 65,732/66,233 (99.24% of lines) and 501 lines with 1,711,050 RSD turnover have unknown cost; strong scale anomalies in legacy `NabavnaCena` exist, but business authority needs **20 real original calculations**. Do not infer currency factor, choose arbitrary cost fallback or replace NULL with zero.
- RQ602 stays PARTIAL: frozen 20 treated + 20 controls historical markdown dry-run is descriptive; daily source-coverage and trusted cost/original calculations and prospective business effect proof are absent. No causal uplift or pilot success claim.
- RQ610 remains BLOCKED: store-keyed `Artikli` alone does not prove an auditable per-store inventory mutation generation across imports, POS/manual changes and transfers. Do not backfill an August observed stock from today's balance; qualify original source and lineage first.

Reference: `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`, RQ610 completion notes, and `MASTER_ROADMAP.md`.

## 4. Prioritized actionable plan

| Order | Owner / prompt | Immediately executable? | Concrete exit gate |
|---|---|---|---|
| P0 live | **STAB16** | No in-repo substitute for provider access | Effective Render environment and deployed SHA; health/readiness JSON and startup SQL outcome; dedicated worker/refresh history; supplier catalog read-only proof and authorized follow-up |
| P1 repo | **RQ634 READY** | Yes | Disposable PG cold-start/no-SQL029 guard for false flag, supplier MVs fail closed; exact-SHA CI |
| P2 repo | **RQ635 READY** | Yes | Read-only SQL029 preflight + isolated repeat/partial/legacy schema oracle; maintenance plan without prod writes |
| P2 repo | **RQ636 READY** | Yes | Isolated InMemory test factory stops unintended Access worker and MediatR warnings; endpoint tests unchanged; CI log proof |
| P1 business | **RQ601 PARTIAL** | Only owner/source evidence intake | Twenty original purchase calculations; priced subset signoff; then recompute affected known/unknown margins |
| P1 business | **RQ610 BLOCKED** | Read-only source qualification only | Credible stock source generation on every writer, non-fanout store identity, idempotence and timestamp proof |
| P2 business | **RQ602 PARTIAL** | Dry-run review only | Daily completeness/cost evidence + signed business owner pilot criteria and measured prospective observations |
| P2 provider | **RQ633 deployment residual** | Requires Vercel project access | Docs-only commit actually skipped, frontend commit builds, root config verified; no rewrite of DONE source task |

**Do not create a generic re-audit loop** that only gathers duplicate screenshots/evidence. Prefer bounded code/test acceptance with explicit limitations and useful business decisions.

## 5. Anti-regression and task-routing rules

- Source `main` commits and GitHub CI prove **repository changes**, not the exact running Render/Vercel version or Neon tables.
- A missing materialized view is a **capability failure**, not permission to return unverified all-time supplier data or a false zero.
- Never treat a `/ready` 503 in the initial few startup seconds as permanent failure without `reason`, `failureCategory`, logs and a later probe; never mask a genuine required-schema failure by returning HTTP 200.
- Run focused tests before direct-main code push; prefer a PR and CI for PostgreSQL-only target tests when local Docker is absent. Record actual skip counts and nonexecuted proofs.
- Do not claim DONE for newly registered RQ634–RQ636 based on this planning-only change.
- Post-close routing must refresh the **post-delivery** `origin/main`, rescan active queue addenda and reconcile any new PR/lock/owner before promoting additional tasks; new prompts are path-disjoint now, not a permanent lock bypass.

## 6. Verification gaps

No authorized provider/production logs after 16:50 UTC, no exact runtime fingerprint or DB read-only session, no Vercel dashboard, no local .NET/Node toolchain in this connector-only edit, while PR #134 Planning Governance passed (`38351086583`) and exact-merge Planning Governance also passed (`38351175549`). The optional full backend/frontend PR runs were not yet complete at evidence synchronization. Therefore this document does **not** certify the production API, current supplier windowed caches, all test coverage, all code quality, or an actual future deployment outcome.


## 7. Second-pass code findings — 2026-10-13 09:53 CEST

The post-PR134 follow-up confirmed that final PR-head backend `38351086653`, frontend `38351086632` and Planning Governance `38351086583` all succeeded; exact-merge governance `38351175549` also succeeded. Earlier `not complete` CI caveats are superseded **for PR #134**.

### Three under-audited runtime boundaries

1. **SQL 018 remains a potential expensive web cold-start path (RQ634 extended).** `DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync` invokes `BuildSupplierDecisionHubObjectsAsync(...allowHeavyRefresh:false)`. This disables explicit `REFRESH`, but **does not disable** the missing-cache branch that executes `018_AddSupplierDecisionHubViews.sql` with `commandTimeoutSeconds:0` (unbounded), and may build populated materialized caches. The PR133 switch only disables SQL029. Need a disposable PostgreSQL missing-018/029 cache matrix, a bounded/isolated rebuild design, and fail-closed affected endpoints; no automatic blanket disabling of required schema.
2. **Web Access import worker executes before readiness (new RQ637).** `Api/appsettings.Production.json` opts into `AccessImport:RegisterWorkerInWebProcess=true` and 2-second polling. `AccessImportBackgroundWorker.ExecuteAsync` calls `RecoverStaleBatchesAtStartupAsync` before its main loop, with no check against `StartupReadinessState`. Pending recovery and `ClaimNextAsync` also run independently of API readiness. The user-provided Render log shows `access-import-queue` SQL every ~2s while SQL029 starts and /ready 503s; this is real concurrency, not proof that import polling alone caused the outage.
3. **Windowed cache diagnostics were blind (source repair in direct-request branch).** `AnalyticsConnectionDiagnosticsHostedService` previously queried/logged only core/all-time objects; missing 90d/180d windows could be omitted from that diagnostic's success/retry decision. The correction uses the canonical `PostgresMaterializedViewCapabilityReader.InspectAsync` contract for both periods, including presence, required columns and populated flags. Windowed failures remain **diagnostic only** and do not initiate web rebuild or block core readiness. This is an additional read-only diagnostic improvement, not production evidence.

### Revised execution order and boundaries

| Task | Repo owner | Priority | Claim |
|---|---|---|---|
| RQ634 | SQL018 **and** SQL029 web-startup cache gating / PostgreSQL regression | P1 | READY |
| RQ637 | Access import **web** worker wait until required DB readiness (stale recovery and claims) | P1 | READY, different files |
| RQ635 | SQL029 maintenance preflight + disposable PostgreSQL replay matrix | P2 | READY |
| RQ636 | EF InMemory test-only worker/telemetry isolation | P2 | READY |
| RQ638 | MediatR PerformanceLogs exact varchar caps, including exception/serialization fallback | P2 | READY |
| STAB16 | Exact Render SHA, effective env, /ready body and DB/worker/catalog proof | P0 operational | BLOCKED on authorized provider evidence |

No existing RQ601/602/610 outcome changes. No new import, fake historical stock or autonomous production SQL execution is justified. All CI and code proof must be distinguished from live provider proof.


### Fourth missed code path: MediatR persistence bounds

The existing SQL and EF schema cap `PerformanceLogs.RequestType` at 200 chars, `RequestName` at 500, `RequestData`/`ResponseData` at 4000, and `ExceptionMessage` at 2000. `Application/Behaviors/PerformanceLoggingBehavior.cs` does not cap RequestType or exception text, and `SerializeObject` returns unbounded `obj.ToString()` when JSON serialization fails. RQ631 fixed the separate HTTP middleware, not this writer. This is a **source-verifiable risk**; current CI `42P01` fake-provider errors do not prove a varchar overflow. RQ638 owns a narrow, tested MediatR fix. RQ636 owns only test fixture noise.