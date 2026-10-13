# RQ635 CI correction and RQ642 targeted coverage routing — 2026-10-13

Task: DIRECT-RQ635-RECOVERY-2026-10-13 (repository/documentation sync; RQ642 creation is NOT implementation)
Initial branch base: main `e8ca4b7da3f8d3aa8b6fee694b2f5d0fec880c83`; rebased file content onto owner-updated main `4982529b5c6e9d200b49cae3282ffe5a44a4acec`
Branch: `docs/2026-10-13-rq635-ci-truth-rq642`
Delivery: docs-only PR #142; no direct PostgreSQL/provider mutation
Evidence state: pending until reviewed delivery

## Reproduced CI cause / confirmed correction

RQ635's original `ca5b6c4129ae72cddad9ed4407992e56870ad31c` tests returned early in the cloud agent without Testcontainers. Subsequent main backend runs (including `38393842455`) failed four PostgreSQL SQL029 lifecycle assertions on missing `__StartupSqlScriptHistory` or `vw_supplier_fullprice_signals` due to unified DB fixture config.

Independent agent PR #137 `test(sql): provision real SQL 029 prerequisites and fail closed without PostgreSQL` was merged on `main` SHA `bae9b0b691efe94f3c1e2f2e6ed4ad3a340a345b`, after **green exact PR-head backend** run `38397641969` and frontend quality run `38397641816`. Reviewed actual backend job log `115296850111`: all six `SupplierWindowedSql029LifecycleTests` show Pass, including the four PostgreSQL scenarios. Complete suite: 2,039 passed, 45 classified skips, zero failures. Main exact-merge backend run `38398763696` subsequently completed **success**. The RQ635 owner separately synchronized the DONE status to current main at `4982529b5c6e9d200b49cae3282ffe5a44a4acec`; this PR preserves that owner's evidence and only adds RQ642 routing.

The correction is code/CI-only proof. No production Neon repair, rendered supplier MVs, worker freshness or live `/ready` certification is implied; STAB16 remains provider owner.

## Additional discovered skip gap

RQ639 made three `AnalyticsDbInfrastructureTests` skips transparent, raising BCI17 inventory to 45. Two (schema columns and concurrent route calls) have deterministic semantics suitable for disposable PostgreSQL/HTTP reproof; the third time budget check cannot be made a stable shared-runner gating test without separate performance infrastructure. New **RQ642 READY** owns only the first two; it does not reopen RQ639 classification or touch SQL029.

## Validation and constraints

- Inspected `main` code and exact PR137 head/merge, GitHub CI run outcomes and backend job logs with real test names; reviewed existing RQ635 and RQ639 queue/evidence.
- `node scripts/check-prompt-queues.mjs`, planning architecture and agent instruction validators: pending PR Planning Governance.
- No local dotnet/PostgreSQL/Docker runtime in connector-only environment; no claim of local test execution.
- No production database DDL, new Access import, Vercel/Render deployment or provider config edit.
- Run after merge: refreshed `origin/main`, verify the queue and roadmap agree on RQ642 primary READY, and preserve independent unfinished business/provider gates.
