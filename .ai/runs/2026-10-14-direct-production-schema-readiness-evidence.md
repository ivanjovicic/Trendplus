# Production schema readiness repair evidence

Task ID: direct-production-schema-readiness-2026-10-14
Queue: direct-user-request
Date: 2026-10-14
Agent/tool: Codex; connected Neon read-only/schema-migration tools
Delivery target: main
Working branch / PR: main / none
Main commit SHA: b3e5cbaeb6bba774958315d6f0dfadd841c27dba
Main verification: passed - refreshed `origin/main` and verified it equals and contains the STAB16 evidence commit above
Evidence state: synchronized
Ownership transfer: none
Claim reservation: direct-user-request/not applicable

## What was done

- Interpreted the supplied worker/readiness 503 logs as a direct production incident; the owning repository contract is `PurchaseCostProvenanceSchemaContract` and the deployed acceptance owner remains STAB16.
- Public `GET https://trendplus-api.onrender.com/ready` returned HTTP 503, `db.ok=true`, `reason=schema_incompatible`, `databaseInitialization.state=failed`, `failureCategory=schema_contract_incompatible`, with process start `2026-10-14T16:53:11Z`. `/health` was HTTP 200 but `ready=false`; `/api/runtime/version` was gated by the same 503.
- Read-only Neon inspection selected existing project `Trendplus`, default branch `production`, database `trendplus`. `public.prodaja_stavke.purchase_cost_provenance` was absent; EF history lacked `20261010160000_AddPurchaseCostProvenance` and its latest Trendplus migration was `20260930074136_GeneralizeOperationsAnalyticsIntegrityEvidence`.
- Prepared the exact additive SQL `ALTER TABLE public.prodaja_stavke ADD COLUMN IF NOT EXISTS purchase_cost_provenance character varying(64);` on temporary Neon branch `br-empty-moon-ag388d5x` as migration `aa23640a-1faf-4285-ac5b-fbec963edd09`. The user explicitly approved production application by replying `Može` to the exact SQL approval request. Applied it to the `production` parent branch; the migration tool deleted the temporary branch.
- Post-application production metadata is nullable `character varying(64)`. `prodaja_stavke` row count stayed `67,144`; non-null `nabavna_cena` count stayed `67,092`. No import was replayed and no existing numeric cost was rewritten.
- Recorded the production mutation and still-open restart gate in the existing STAB16 queue owner document, delivered that evidence to `main`, and verified `origin/main` contains its exact commit.

## Files changed

- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-10-14-direct-production-schema-readiness-evidence.md`

## Validation run

- Neon `describe_table_schema` and read-only `run_sql` before migration -> pass; column and EF migration row absent.
- Neon `prepare_database_migration` on temporary branch -> pass.
- Neon `run_sql` on temporary branch -> pass; nullable `character varying(64)`, 67,144 sale lines, 67,092 rows with a numeric cost, zero populated provenance values.
- Neon `complete_database_migration` with `apply_changes=true` after the user's approval -> pass; parent branch updated and temporary branch deleted.
- Neon read-only `run_sql` on production after migration -> pass; exact column contract and unchanged counts.
- `node scripts/check-prompt-queues.mjs` -> pass (792 tasks).
- `node scripts/check-planning-architecture.mjs` -> pass (80 tasks).
- `git diff --cached --check` -> pass.
- `git fetch origin main; git rev-parse HEAD; git rev-parse origin/main` -> pass; both `b3e5cbaeb6bba774958315d6f0dfadd841c27dba` after the STAB16 evidence delivery.
- Public `/ready` after schema repair -> HTTP 503 with unchanged process start and failed-at timestamps; this is a confirmed pending restart, not a passing readiness check.

## Validation not run

- Render restart and post-restart `/ready`/analytics endpoint verification: not run because this workspace has no Render provider control. The user was asked to restart the existing `trendplus-api` service.
- Full EF migration and production worker/refresh certification: not run; the database history still has a later pending store-day-coverage migration and STAB16's provider/read-only reconciliation gates remain open.
- Product tests: not run because no product code changed; the exact production column metadata and row-count invariants were checked directly.

## Documentation impact

- Added one dated incident note to the existing STAB16 owner document. Its `BLOCKED` status and current routing were unchanged. No new queue task or duplicate audit was created.

## What was missed

- The existing Render process still reports the earlier terminal schema-check failure. The service has not yet been restarted and production API readiness has not been restored or certified.
- The effective Render `DefaultConnection` target and initialization flags were not accessible; the connected Neon database matches the observed missing-column contract, but exact runtime connection identity remains to be confirmed.

## Risks

- A manual additive column repair does not advance `__EFMigrationsHistory` or apply `20261011110000_AddAccessImportStoreDayCoverage`. The controlled startup/deployment migration path must reconcile remaining schema drift.
- Existing rows have null provenance; no historical cost provenance or pilot certification is inferred from the schema addition.
- The GitHub manual Render workflow deploys a new `main` commit and its latest historical runs failed at build; it was not used as a substitute for restarting the existing deployed process.

## Post-close routing recovery

- not applicable; this was a direct production incident request, no formal queue prompt was claimed or closed.

## Next

- Render operator: restart existing `trendplus-api` without initiating a new deploy; then provide the completion time and sanitized effective initialization flags/connection target. Codex can immediately verify fresh `/ready`, runtime SHA and affected analytics routes. Keep STAB16 `BLOCKED` until its separate worker/freshness and source-reconciliation acceptance passes.
