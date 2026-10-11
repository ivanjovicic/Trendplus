Task ID: RQ610
Queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`
Date: 2026-10-11
Agent/tool: Codex
Delivery target: main
Working branch / PR: `main` / none
Main commit SHA: `93b1c3dcc3a6f74373ca68754c8af064642cede6`
Main verification: fresh fetch verified `origin/main` at `93b1c3dcc3a6f74373ca68754c8af064642cede6`
Evidence state: synchronized
Ownership transfer: Codex -> unassigned; authority/evidence: source-qualification outcome recorded in queue and this run log; no branch/PR artifacts existed

## What was done

Claimed RQ610 for bounded, read-only source qualification after refreshing `origin/main` to `3a6e442ecfef478996708d9642f37a38a1e27e6d`. Ran the queued probe against the exact local `trendplus` business database in a PostgreSQL session forced read-only. Cross-checked Access importer field mapping, transfer and manual-update writers, model lineage, source tables and analytics dimensions. The evidence supports structurally store-keyed `Artikli` rows but fails the required reliable freshness/generation contract, so the task is returned to BLOCKED and ownership released to unassigned. No capture adapter, worker/schema edit, source mutation, import or migration was performed.

## Files changed

- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`
- `.ai/runs/2026-10-11-RQ610-source-qualification-evidence.md`

## Validation run

- `psql -X --set=ON_ERROR_STOP=1 --host 127.0.0.1 --port 5434 --username postgres --dbname trendplus --file tools/rq610-store-stock-source-qualification.sql` with `PGOPTIONS=-c default_transaction_read_only=on` — pass; target identified as local `trendplus`, PostgreSQL 16.13, primary (not replica).
- Read-only catalog, store-scope and mutation-lineage queries — pass after correcting exploratory table/column identifier typos. `Artikli` has only a primary-key constraint and no user trigger. There are 12,428 rows total: 12,422 `access` / SourceBatchId 23 and 6 `existing`; no NULL `IDObjekat`; all rows have unique Article IDs. The six Access store IDs have 3,255, 129, 61, 1,523, 766 and 6,688 rows and each scope matches sales headers, stock movements and `StoresDim`. 1,179 normalized PLU groups span multiple stores (9,500 article rows). All 12,422 batch-23 quantities are non-NULL: 11,885 measured zero and 537 positive rows / 3,566 positive units; no negative rows. Their ProductsDim quantity parity is exact; all six non-batch legacy rows differ. `SourceUpdatedAtUtc` is NULL for all 12,428 rows.
- Source code inspection — pass: Access importer maps source `idobjekat/storeid` directly and sets `UpdatedAt` to importer UTC time; `SourceBatchId` is import lineage. `UpdateArtikalHandler` changes quantity and store without advancing `UpdatedAt` or source lineage. `TransferService` adjusts source/destination quantities and writes movement rows but does not advance an article generation/timestamp. `ProdajaRepository` delegates to `sp_prodaj_artikle_json`, whose definition is not in the repository or local `public` schema; POS inventory effects remain unverified.
- `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `git diff --check` — pass.

## Validation not run

- Disposable PostgreSQL capture/idempotency/rollback fixtures, worker scheduling tests and Release build — not run because no capture code was written; the source contract failed the prompt's precondition for an adapter.
- Production data writes, imports, migrations, scheduled capture and historical backfill — not run; prohibited by the prompt.
- POS database function behavior — unavailable in the local database/repository, so not inferred.

## Documentation impact

Updated the RQ610 status/owner and primary READY routing to reflect the verified blocker, recorded the evidence and exact unblock event, and updated the master roadmap. Existing company-grain capture stays untouched and is not labeled store-grain.

## What was missed

- No implementation was made because a durable stock mutation generation contract could not be established across imports, manual edits, transfers and POS.
- Historical batch-23 data was inspected read-only only; no historical per-day capture or certification was created.

## Risks

- `UpdatedAt`, `DataOrigin` and `SourceBatchId` can retain old Access provenance after non-Access quantity edits. Capturing these values as current source freshness would be misleading.
- POS inventory effects cannot be verified from source-controlled function code or the local database.
- Existing per-store row links look consistent in this local snapshot, but no FK enforces the `Artikli.IDObjekat` relationship.

## Post-close routing recovery

- Recovery base: post-delivery `origin/main` SHA `93b1c3dcc3a6f74373ca68754c8af064642cede6`.
- Scanned all 30 queue/addendum documents in `docs/ai`, including the full active RQ/SQL owner set, and `MASTER_ROADMAP.md`; refreshed task locks, branches and open PRs. RQ612 is independent read-only SQL/methodology work; no matching lock, branch or open PR was found. The unrelated active lock is P-UI-43.
- RQ610 returned to BLOCKED with owner unassigned. Exact unblock: define a durable stock-generation/provenance contract that advances atomically for Access imports, POS writes, manual stock/store edits and transfers; make the POS function definition inspectable/source-controlled; prove two-store attribution, retries/concurrency and atomic capture against a disposable PostgreSQL fixture.
- RQ612 is promoted to primary READY. There is no Zero-READY conclusion.
- Planning Governance run `38123766384` on the recovery SHA succeeded.

## Next

RQ612 — read-only shadow-control candidate feasibility, preserving the immutable RQ602 historical cohort. RQ610 can reopen after the source owner establishes the stock mutation generation and audit contract.
