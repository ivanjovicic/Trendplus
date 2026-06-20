# Analytics Demo Reset Runbook

Date: 2026-06-17
Scope: safe demo-only analytics reset and reseed procedure

## Purpose

This runbook defines the safest repeatable way to prepare a demo environment for analytics sales or pilot presentations.

It does not introduce:

- new destructive reset code
- a production cleanup shortcut
- a mixed customer/demo data mode

If the environment cannot prove that it is demo-only, stop and do not run any destructive step.

## Current Confirmed Capability

| Capability | Current state | Safe for shared demo reset? | Notes |
|---|---|---|---|
| `scripts/seed_local_db.ps1` | local Docker seed helper for developer test data | No | Seeds local dev migrations only; not a pilot/demo reset tool |
| `Database/SeedData/001_seed_analytics_shoes.sql` | open-training shoe marketplace seed data | No | Useful for training/demo-adjacent exploration, not for pilot analytics dataset reset |
| Access import run | Exists via `/api/access-import/*` | Yes, with caution | Suitable for loading a known demo Access file into a dedicated demo environment |
| Access import batch delete | Exists via `DELETE /api/access-import/batches/{id}` | Yes, with caution | Safe only for known demo batches in a dedicated demo environment |
| Cleanup preview | Exists via `/api/access-import/cleanup/preview` | Yes | Use to inspect blast radius before any cleanup |
| Cleanup execute | Exists via `/api/access-import/cleanup/execute` | No by default | Too risky for mixed environments; use only in a dedicated demo environment with backup and explicit approval |
| Demo environment verification | Exists via `/api/admin/demo-verification` | Yes, with admin access | Returns `demoSafe`, `reasons`, `environment`, `checkedAtUtc`, and `warnings`; use this before any destructive demo reset |
| Worker/manual refresh control | Exists via worker/admin endpoints | Yes, with admin access | Can trigger refresh and data-quality worker runs after reseed |
| Pilot readiness and smoke verification | Exists | Yes | Use `/analytics/pilot-readiness` and [ANALYTICS_PILOT_SMOKE_TEST.md](c:/Users/Ivan/source/repos/Trendplus2/docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md) as final proof |

## Safety Rules

These rules are mandatory.

1. Use a dedicated demo deployment or dedicated demo database only.
2. Never run this procedure against production or a customer environment.
3. Require one explicit environment proof before any destructive step.
4. Take or confirm a backup before reset, cleanup or batch deletion.
5. Use clearly labeled demo names in stores, suppliers, products and reports.
6. Keep the demo import file, backup reference and smoke evidence together.
7. If any step is ambiguous, stop and do not improvise a reset.

## Required Environment Proof

At least one of the following environment proofs must be true before destructive steps:

- deployment/environment name clearly contains `demo`
- explicit `AnalyticsDemo__Enabled=true` flag is set and verified operationally
- database instance is dedicated to demo use and the connection marker proves it
- explicit operator proof is recorded in this runbook

Machine-checkable proof:

- call `GET /api/admin/demo-verification`
- send the same admin credential header used by other `/api/admin` endpoints (`X-Admin-Key` compatibility path)
- require `demoSafe=true`
- require at least one reason code:
  - `environment_name_contains_demo`
  - `analytics_demo_flag_enabled`
  - `analytics_connection_database_contains_demo`
  - `analytics_connection_host_contains_demo`
  - `analytics_connection_application_name_contains_demo`
- treat `warnings` as informational only; they must not contain secrets or raw connection strings

Operator proof template:

- date and time
- operator name
- environment name or deployment id
- backup or snapshot reference
- explicit statement that this environment is demo-only and safe to reset
- approval to proceed only after the API proof returns `demoSafe=true`

Current repo note:

- this runbook requires a demo-only proof, but it does not claim that the application already enforces such a flag in code
- the machine-checkable proof is `GET /api/admin/demo-verification` with admin access; proceed only when `demoSafe=true`
- the response includes `environment` and `warnings` for operator context, but those fields do not replace the API proof
- the operator note documents human approval and does not replace the API proof

## Naming Rules For Demo Data

Use obvious demo labels so the dataset cannot be mistaken for a customer:

- store: `DEMO Beograd`, `DEMO Novi Sad`
- supplier: `DEMO Nike`, `DEMO Adidas`, `DEMO Dobavljac Bez Cene`
- products: prefix with `DEMO-` or include `Demo`
- reports: include `Demo` in title or export filename when possible
- batches: source file name should include `demo`, for example `trendplus-demo-pilot.accdb`

If the environment has no tenant isolation, these naming rules are not enough by themselves. A dedicated demo database is still required.

## Minimal Demo Dataset Shape

The dataset should be intentionally small but rich enough to prove analytics trust and decision value.

### Products

- `50-200` products
- category/brand/shoe-type coverage
- a few intentionally incomplete records

### Suppliers

- `5-10` suppliers
- at least one supplier with healthy margin/performance
- at least one supplier with warning signal
- at least one supplier with missing or weak mapping

### Sales History

- `90` or `180` days of sales
- at least one strong seller
- at least one declining product
- at least one low-signal or sparse history product

### Inventory

- current stock snapshot
- at least one OOS-risk example
- at least one dead-stock or overstock example
- at least one transfer/rebalance-worthy example if supported by the environment

### Data Quality Issues

- some missing supplier mappings
- some missing cost prices
- some low-signal / insufficient-history items
- optional controlled freshness warning if it can be shown safely

### Actions And Outcomes

- at least one new action candidate
- at least one accepted or deferred action if realistic
- at least one completed action with outcome note if available

### Reports

- pilot intake report route must load from the seeded dataset
- supplier decision report route must load from the seeded dataset
- reports must show freshness/generated context where available

## Recommended Reset Strategy

Use the least risky reset method that matches the environment.

### Preferred: Restore A Known Good Demo Snapshot

Use this when:

- a dedicated demo DB snapshot exists
- you need the fastest, most repeatable reset

Why it is safest:

- avoids guessing which rows to delete
- restores a previously verified demo state
- keeps reset blast radius clear

After restore:

1. run analytics refresh
2. run data-quality refresh
3. verify Pilot Readiness
4. run smoke checklist

### Fallback: Delete Known Demo Import Batch And Re-import Demo File

Use this when:

- no full DB snapshot exists
- the demo environment was populated through one or a small number of known Access import batches

Requirements:

- the batch IDs are known demo batches only
- the source file is retained
- backup exists

Avoid this if:

- batch origin is unclear
- the environment contains mixed customer/demo data

### Last Resort: Cleanup Execute In Dedicated Demo Environment Only

Use this only when all are true:

- environment is dedicated demo-only
- backup is confirmed
- cleanup preview has been reviewed
- an operator explicitly approves the blast radius

This path is intentionally not the default because it is the easiest way to make a destructive mistake.

## Run Sequence

### Phase 0: Pre-flight

1. Confirm demo-only environment proof.
2. Call `GET /api/admin/demo-verification` and store the response.
3. Confirm admin access for import and worker control endpoints.
4. Record current date/time, operator and reason for reset.
5. Save current backend health and refresh status.
6. Confirm backup or snapshot reference.
7. Save current batch list and any existing report URLs if they matter for comparison.

Recommended checks:

- `/health`
- `/ready`
- `/api/analytics/refresh-status?dataScope=all`
- `/api/access-import/batches`

### Phase 1: Reset Existing Demo State

Choose one path only.

#### Path A: Restore Known Good Demo Snapshot

1. Pause or avoid concurrent worker activity if your operations process requires it.
2. Restore the dedicated demo operational DB and analytics DB snapshot.
3. Confirm restore succeeded before any refresh.

#### Path B: Delete Known Demo Import Batch

1. Open the Access import batch list.
2. Identify the exact demo batch by file name and timestamp.
3. Verify it is not customer data.
4. Delete only the confirmed demo batch with `includeAnalytics=true`.
5. Re-check batch status and data state.

#### Path C: Preview Cleanup Before Any Cleanup Execute

1. Call cleanup preview first.
2. Review returned row counts.
3. Stop if any unexpected non-demo scope appears.
4. Only then consider cleanup execute, and only in a dedicated demo environment.

## Phase 2: Seed Demo Data

Preferred seed source:

- a known demo `.accdb` or `.mdb` file loaded through the existing Access import flow

Seed steps:

1. Verify the source file name includes `demo`.
2. Start Access import with analytics included.
3. Watch batch status until completion.
4. If the batch fails, stop and do not continue to refresh.

Important note:

- existing repo seed helpers are developer-oriented and are not a substitute for a pilot analytics demo dataset

## Phase 3: Refresh Analytics

After the demo dataset is loaded:

1. run or request `NightlyAnalyticsRefreshWorker`
2. run or request `AnalyticsDataQualityHealthWorker`
3. verify refresh status endpoint
4. verify there is no hidden stale state

Use existing admin/worker control surfaces only.

Do not:

- add a special demo refresh endpoint
- fake a successful refresh
- skip refresh verification

## Phase 4: Verify Pilot Readiness

Open:

- `/analytics/pilot-readiness`

Confirm:

1. `Podaci učitani` is not blocked
2. `Kvalitet podataka proveren` shows honest trust state
3. `Analytics osvežen` reflects the latest run
4. report-related items link to stable report routes
5. unknown states are not green

If any critical step is `blocked` or misleadingly `unknown`, stop the demo prep and fix the state first.

## Phase 5: Run Analytics Smoke Test

Run the checklist in [ANALYTICS_PILOT_SMOKE_TEST.md](c:/Users/Ivan/source/repos/Trendplus2/docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md).

Minimum required proof:

1. backend critical routes are reachable
2. dashboard loads without fake zeros
3. product, supplier, inventory and data-quality screens render honestly
4. pilot intake report route works
5. supplier decision report route works

## Evidence To Save Per Reset Run

Save these artifacts:

- environment proof
- backup or snapshot reference
- demo source file name
- import batch ID
- refresh timestamps
- Pilot Readiness screenshot
- smoke checklist result
- any failure screenshot plus correlation ID

## Fail / Stop Conditions

Stop immediately if any of these happen:

- environment cannot be proven demo-only
- operator proof is missing from this runbook
- `GET /api/admin/demo-verification` returns `demoSafe=false`
- backup is missing
- import source file origin is unclear
- batch to delete is not clearly demo-only
- cleanup preview shows unexpected non-demo rows
- refresh fails and Pilot Readiness becomes misleading
- smoke test shows `404`, fake healthy green, or fake `0 RSD`

## Operational Recommendation

Until a dedicated demo-mode reset tool exists, the safest repeatable process is:

1. dedicated demo environment
2. known good DB snapshot or known demo import batch rollback
3. Access re-import of the demo file
4. worker-driven refresh
5. Pilot Readiness verification
6. smoke checklist sign-off

## Explicit Non-Goals

- no one-click demo reset
- no production cleanup shortcut
- no mixed customer/demo reset path
- no new seed backend contract in this task
